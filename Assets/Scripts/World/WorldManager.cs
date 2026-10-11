using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도 씬 — 한 턴이 여기서 열리고 닫힌다(전투 씬 BattleManager의 짝). 씬이 뜰 때 RunState.Phase를 보고:
    // 지도 단계면 턴을 열고(BeginTurn — 씬을 오가도 턴당 한 번) 명령을 받는다: 땅을 눌러 고르고 「침략」(전투 씬으로) · 「상점」(전투 씬의 상점으로) · 「쉬기」.
    // 전투가 끝나 돌아왔거나(Aftermath) 쉬었으면 AI 영주들의 턴을 돌리고(EndTurn) 그 사건들을 지도에서 차례로 재생한 뒤 요약 → 「다음 턴」.
    // 통일 · 게임 오버도 여기서 띄운다 → 「새 런」. 규칙은 RunState에, 그림은 UI/WorldMapView · UI/WorldSidePanel에 있다
    public class WorldManager : MonoBehaviour
    {
        public const string SceneName = "World";
        public const string BattleSceneName = "Battle";

        // AI 턴 재생 — 판도가 바뀌는 사건 하나를 보여 주는 시간(초)과 전체 상한(사건이 많으면 한 건이 짧아진다)
        private const float EventSeconds = 0.15f;
        private const float ReplaySeconds = 3f;
        // 못 누르는 버튼의 글 색(상점과 같다)
        private static readonly Color DisabledText = new Color(0.6f, 0.6f, 0.66f);

        private enum Stage { Command, Replay, Summary }

        private static WorldManager _instance;

        [SerializeField] private BattleConfig _config;
        [SerializeField] private WorldMapView _map;
        [SerializeField] private WorldSidePanel _panel;
        [Tooltip("위 줄 — 턴 · 골드 · 행복도 · 땅")]
        [SerializeField] private Text _topBar;
        [SerializeField] private Button _invade;
        [SerializeField] private Text _invadeLabel;
        [SerializeField] private Button _rest;
        [SerializeField] private Button _shop;
        [Tooltip("AI 턴을 재생하는 동안 화면 전체를 덮는 투명한 판 — 누르면 재생을 건너뛴다")]
        [SerializeField] private Button _skip;
        [Tooltip("왼쪽 패널의 알림(고른 땅 자리) — AI 턴 · 그 요약 · 통일 · 게임 오버와 「다음 턴」 · 「새 런」")]
        [SerializeField] private GameObject _notice;
        [SerializeField] private Text _noticeTitle;
        [SerializeField] private Text _noticeBody;
        [SerializeField] private Button _next;
        [SerializeField] private Text _nextLabel;

        private Stage _stage;
        private int _selected = WorldMapView.None;
        // 화면에 보이는 턴 번호 — AI 턴을 재생하는 동안에는 방금 끝난 턴
        private int _turn;
        // 재생 중 지도에 보이는 주인 표(턴을 닫기 전 표에 사건을 하나씩 적용한다) — 재생이 끝나면 null(실제 판도를 그린다)
        private int[] _shown;
        private List<WorldEvent> _events;
        private int _eventAt;
        private float _eventTimer, _eventStep;

        // 명령을 받는 중인가(치트 패널이 읽는다)
        public static bool Commanding => _instance != null && _instance._stage == Stage.Command && !RunState.IsOver;

        private void Awake()
        {
            _instance = this;
            // 전투 결과 창에서 멈춘 채 넘어왔을 수 있다
            Time.timeScale = 1f;
            // 이 씬만 직접 플레이해도 런이 시작된다
            if (!RunState.Started)
                RunState.StartNew(_config);
            _turn = RunState.Round + 1;
            _map.Build(RunState.World.Map);
            _map.Clicked += Select;
            _invade.onClick.AddListener(Invade);
            _rest.onClick.AddListener(Rest);
            _shop.onClick.AddListener(Shop);
            _skip.onClick.AddListener(FinishReplay);
            _next.onClick.AddListener(Next);
            _skip.gameObject.SetActive(false);
            _notice.SetActive(false);

            if (RunState.Phase == RunState.RunPhase.Aftermath)
                PlayAiTurn();
            else if (RunState.IsOver)
                ShowEnd();
            else
            {
                // 상점에서 돌아왔으면 고른 땅이 남아 있다
                _selected = RunState.PreviewRegion;
                BeginTurn();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void BeginTurn()
        {
            RunState.BeginTurn();
            RunState.OpenMap();
            _turn = RunState.Round + 1;
            _stage = Stage.Command;
            _shown = null;
            _notice.SetActive(false);
        }

        // 땅을 눌렀다 — 고른다(바다 · 지도 밖을 누르면 푼다). 남의 땅이면 그 영주의 탑을 미리 본다(상점에 가도 남는다)
        private void Select(int region)
        {
            if (_stage == Stage.Command)
                _selected = region;
        }

        private bool Foreign(int region) => region >= 0 && RunState.World.State.OwnerOf(region) != RunState.World.State.Player;

        private void Invade()
        {
            if (_stage != Stage.Command || !RunState.InvadableRegions().Contains(_selected))
                return;
            RunState.Invade(_selected);
            SceneManager.LoadScene(BattleSceneName);
        }

        private void Shop()
        {
            if (_stage != Stage.Command)
                return;
            RunState.OpenShop();
            SceneManager.LoadScene(BattleSceneName);
        }

        private void Rest()
        {
            if (_stage != Stage.Command)
                return;
            RunState.Rest();
            PlayAiTurn();
        }

        // 턴을 닫고(판도는 바로 끝 상태가 된다) 그 사이의 사건을 지도에서 차례로 보여 준다
        private void PlayAiTurn()
        {
            _selected = WorldMapView.None;
            _turn = RunState.Round + 1;
            _shown = (int[])RunState.World.State.Owner.Clone();
            _events = RunState.EndTurn();
            if (RunState.IsOver)
            {
                ShowEnd();
                return;
            }
            int changes = 0;
            foreach (WorldEvent e in _events)
                if (Changes(e))
                    changes++;
            _eventAt = 0;
            _eventStep = Mathf.Min(EventSeconds, ReplaySeconds / Mathf.Max(1, changes));
            _eventTimer = _eventStep;
            _stage = Stage.Replay;
            _skip.gameObject.SetActive(true);
            ShowNotice("영주들의 차례", "화면을 누르면 건너뜁니다.", null);
        }

        // 왼쪽 패널의 알림 — 제목 · 읽는 글 · 버튼(next가 null이면 버튼 없이)
        private void ShowNotice(string title, string body, string next)
        {
            _noticeTitle.text = title;
            _noticeBody.text = BuildingTooltip.Highlight(body);
            _next.gameObject.SetActive(next != null);
            if (next != null)
                _nextLabel.text = next;
            _notice.SetActive(true);
        }

        // 지도의 색이 바뀌는 사건인가 — 진 침략은 아무것도 바꾸지 않는다
        private static bool Changes(WorldEvent e) => e.Rebellion || e.Won;

        // 재생을 끝낸다(건너뛰기 포함) — 실제 판도를 그리고 요약을 띄운다
        private void FinishReplay()
        {
            _shown = null;
            _stage = Stage.Summary;
            _skip.gameObject.SetActive(false);
            ShowNotice("영주들의 차례", RunState.TurnSummary, "다음 턴");
        }

        private void ShowEnd()
        {
            _shown = null;
            _stage = Stage.Summary;
            _selected = WorldMapView.None;
            _skip.gameObject.SetActive(false);
            ShowNotice(RunState.Cleared ? "통일!" : "게임 오버",
                RunState.Cleared ? $"턴 {_turn}에 모든 땅을 차지했습니다." : $"턴 {_turn}에 행복도가 0이 되어 반란으로 무너졌습니다.", "새 런");
        }

        private void Next()
        {
            if (RunState.IsOver)
            {
                RunState.StartNew(_config);
                SceneManager.LoadScene(SceneName);
            }
            else
                BeginTurn();
        }

        private void Update()
        {
            WorldState state = RunState.World.State;
            if (_stage == Stage.Replay)
            {
                // 한 프레임이 길어도(씬을 막 불러온 프레임 등) 사건이 한꺼번에 넘어가지 않게 프레임당 진행을 묶는다
                _eventTimer -= Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                while (_eventTimer <= 0f && _eventAt < _events.Count)
                {
                    WorldEvent e = _events[_eventAt++];
                    e.Apply(_shown);
                    if (Changes(e))
                        _eventTimer += _eventStep;
                }
                if (_eventAt >= _events.Count && _eventTimer <= 0f)
                    FinishReplay();
            }

            bool command = _stage == Stage.Command;
            // 고른 땅이 남의 땅일 때만 미리 본다 — 치트로 그 땅이 내 것이 되면 풀린다
            if (command)
                RunState.Preview(Foreign(_selected) ? _selected : WorldMapView.None);
            List<int> invadable = command ? RunState.InvadableRegions() : null;
            bool[] open = null;
            if (invadable != null)
            {
                open = new bool[state.Owner.Length];
                foreach (int region in invadable)
                    open[region] = true;
            }
            _map.Show(_shown ?? state.Owner, lord => RunState.ColorOf(state.Lords[lord]), state.Player.Id, open, _selected);
            // 명령을 받는 동안에만 고른 땅 · 명령 버튼이 보인다 — 그 밖에는 그 자리에 알림(AI 턴 · 요약 · 끝)
            _panel.Show(_selected, command);
            foreach (Button button in new[] { _invade, _rest, _shop })
                if (button.gameObject.activeSelf != command)
                    button.gameObject.SetActive(command);
            _invade.interactable = command && invadable.Contains(_selected);
            _invadeLabel.color = _invade.interactable ? Color.white : DisabledText;
            _topBar.text = $"턴 {_turn}    골드 <color={BuildingTooltip.NumberColor}>{RunState.Gold}</color>    행복도 {RunState.Happiness}/{RunState.HappinessMax}    땅 {RunState.Lands}/{RunState.RegionCount}";
        }
    }
}

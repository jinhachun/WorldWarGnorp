using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도 씬 — 한 턴이 여기서 열리고 닫힌다(전투 씬 BattleManager의 짝). 씬이 뜰 때 RunState.Phase를 보고:
    // 지도 단계면 턴을 열고(BeginTurn — 씬을 오가도 턴당 한 번) 명령을 받는다: 땅을 눌러 고르고 「침략」(전투 씬으로) · 「상점」(지도 위 팝업 — 턴이 열릴 때 저절로 한 번 내려온다) · 「쉬기」.
    // 전투가 끝나 돌아왔거나(Aftermath) 쉬었으면 AI 영주들의 턴을 돌리고(EndTurn) 그 사건들을 지도에서 차례로 재생한 뒤 요약 → 「다음 턴」.
    // 통일 · 게임 오버도 여기서 띄운다 → 「새 런」. 규칙은 RunState에, 그림은 UI/WorldMapView · UI/WorldSidePanel에 있다
    public class WorldManager : MonoBehaviour
    {
        public const string SceneName = "World";
        public const string BattleSceneName = "Battle";

        // AI 턴 재생 — 판도가 바뀌는 사건 하나를 보여 주는 시간(초)과 전체 상한(사건이 많으면 한 건이 짧아진다)
        private const float EventSeconds = 0.15f;
        private const float ReplaySeconds = 3f;
        // 상점 팝업이 내려오는 · 올라가는 시간(초)
        private const float ShopOpenSeconds = 0.3f, ShopCloseSeconds = 0.2f;
        // 못 누르는 버튼의 글 색(상점과 같다)
        private static readonly Color DisabledText = new Color(0.6f, 0.6f, 0.66f);

        // Choose = 런의 첫 화면 — 내 국가를 고르는 중(고르면 첫 턴이 열린다)
        private enum Stage { Choose, Command, Replay, Summary }

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
        [Tooltip("상점 팝업(UI/ShopPanel) — 지도 영역을 덮는다. 세 번째 버튼(「상점」)으로 열고 판 안의 큰 「지도로」로 닫는다")]
        [SerializeField] private ShopPanel _shopPopup;
        [Tooltip("AI 턴을 재생하는 동안 화면 전체를 덮는 투명한 판 — 누르면 재생을 건너뛴다")]
        [SerializeField] private Button _skip;
        [Tooltip("왼쪽 패널의 알림(고른 땅 자리) — AI 턴 · 그 요약 · 통일 · 게임 오버와 「다음 턴」 · 「새 런」")]
        [SerializeField] private GameObject _notice;
        [SerializeField] private Text _noticeTitle;
        [SerializeField] private Text _noticeBody;
        [SerializeField] private Button _next;
        [SerializeField] private Text _nextLabel;
        [Tooltip("런을 시작할 때 내 국가를 고르는 판 — 국가마다 버튼 하나와 그 버튼의 아이콘(칸 순서 = RunState.NationChoices)")]
        [SerializeField] private GameObject _nationPanel;
        [SerializeField] private Button[] _nationButtons;
        [SerializeField] private Image[] _nationIcons;

        private Stage _stage;
        private bool _shopOpen;
        // 0 = 다 올라갔다(꺼짐) · 1 = 다 내려왔다
        private float _shopSlide;
        private RectTransform _shopRect;
        private Vector2 _shopHome;
        private CanvasGroup _shopGroup;
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
            _nationPanel.SetActive(false);
            _shopPopup.MapRequested += () => SetShop(false);
            _shopRect = (RectTransform)_shopPopup.transform;
            _shopHome = _shopRect.anchoredPosition;
            _shopGroup = _shopPopup.GetComponent<CanvasGroup>();
            _shopGroup.blocksRaycasts = false;
            _shopPopup.gameObject.SetActive(false);

            if (RunState.Phase == RunState.RunPhase.Aftermath)
                PlayAiTurn();
            else if (RunState.IsOver)
                ShowEnd();
            else
            {
                // 씬만 다시 불러왔으면(치트) 고른 땅이 남아 있다
                _selected = RunState.PreviewRegion;
                if (RunState.NationChosen)
                    BeginTurn();
                else
                    ShowNations();
            }
        }

        // 내 국가 고르기 — 국가마다 아이콘과 이름이 든 버튼 하나. 고르면 첫 턴이 열린다
        private void ShowNations()
        {
            _stage = Stage.Choose;
            _turn = RunState.Round + 1;
            BuildingNation[] nations = RunState.NationChoices;
            for (int i = 0; i < _nationButtons.Length; i++)
            {
                bool used = i < nations.Length;
                _nationButtons[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                BuildingNation nation = nations[i];
                _nationButtons[i].GetComponentInChildren<Text>(true).text = nation.DisplayName();
                _nationIcons[i].sprite = _config.World.NationIcon(nation);
                _nationIcons[i].SetNativeSize();
                _nationButtons[i].onClick.AddListener(() =>
                {
                    RunState.ChooseNation(nation);
                    _nationPanel.SetActive(false);
                    BeginTurn();
                });
            }
            _nationPanel.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void BeginTurn()
        {
            RunState.BeginTurn();
            _turn = RunState.Round + 1;
            _stage = Stage.Command;
            _shown = null;
            _notice.SetActive(false);
            // 턴이 열리면 상점이 저절로 내려온다 — 턴마다 한 번(씬만 다시 불러온 것이면 안 내려온다)
            if (RunState.TakeShopCue())
                SetShop(true);
        }

        // 땅을 눌렀다 — 고른다(바다 · 지도 밖을 누르면 푼다)
        private void Select(int region)
        {
            if (_stage == Stage.Command && !_shopOpen && _shopSlide == 0f)
                _selected = region;
        }

        private void Invade()
        {
            if (_stage != Stage.Command || !RunState.InvadableRegions().Contains(_selected))
                return;
            RunState.Invade(_selected);
            SceneManager.LoadScene(BattleSceneName);
        }

        // 왼쪽 아래 세 번째 버튼(「상점」) — 닫혀 있는 상점 팝업을 다시 연다
        private void Shop()
        {
            if (_stage == Stage.Command)
                SetShop(true);
        }

        // 상점 팝업 — 지도 영역을 덮는다. 열린 동안에는 지도를 누를 수 없고 왼쪽 아래 명령 버튼이 숨는다.
        // 위에서 내려오고 위로 올라간다(SlideShop) — 다 올라간 뒤에 꺼진다
        private void SetShop(bool open)
        {
            _shopOpen = open;
            if (open && !_shopPopup.gameObject.activeSelf)
            {
                PlaceShop();
                _shopPopup.gameObject.SetActive(true);
            }
        }

        // 한 프레임이 길어도(씬을 막 불러온 프레임 등) 움직임이 건너뛰지 않게 프레임당 진행을 묶는다. 움직이는 동안에는 상점을 누를 수 없다
        private void SlideShop()
        {
            float target = _shopOpen ? 1f : 0f;
            if (_shopSlide == target)
                return;
            float seconds = _shopOpen ? ShopOpenSeconds : ShopCloseSeconds;
            _shopSlide = Mathf.MoveTowards(_shopSlide, target, Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) / seconds);
            PlaceShop();
            _shopGroup.blocksRaycasts = _shopSlide == target;
            if (_shopSlide == 0f)
                _shopPopup.gameObject.SetActive(false);
        }

        // 다 내려온 자리(_shopHome)에서 위로 — 내려올수록 느려진다
        private void PlaceShop()
        {
            float up = 1f - _shopSlide;
            _shopRect.anchoredPosition = _shopHome + new Vector2(0f, up * up * up * ((RectTransform)_shopRect.parent).rect.height);
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
        private static bool Changes(WorldEvent e) => e.Rebellion || e.Won || e.Coalition;

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
            SlideShop();
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
            // 고른 땅은 런 상태에 둔다 — 상점에 다녀와도 남고, 상점 패널이 같은 땅을 보여 준다
            if (command)
                RunState.Preview(_selected);
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
            _panel.Show(_selected, command, _shopOpen);
            if (!command && _shopOpen)
                SetShop(false);
            // 상점이 열린 동안에는 명령 버튼 셋을 다 숨긴다 — 닫는 길은 팝업 안의 큰 「지도로」 하나(사용자 10/11), 닫아야 지도에서 고른다
            foreach (Button button in new[] { _invade, _rest, _shop })
            {
                bool shown = command && !_shopOpen;
                if (button.gameObject.activeSelf != shown)
                    button.gameObject.SetActive(shown);
            }
            _invade.interactable = command && invadable.Contains(_selected);
            _invadeLabel.color = _invade.interactable ? Color.white : DisabledText;
            _panel.ShowStatus(_topBar, _turn);
        }
    }
}

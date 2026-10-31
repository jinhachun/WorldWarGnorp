using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도 씬 — 한 턴이 여기서 열리고 닫힌다(전투 씬 BattleManager의 짝). 씬이 뜰 때 RunState.Phase를 보고:
    // 지도 단계면 턴을 열고(BeginTurn — 씬을 오가도 턴당 한 번) 명령을 받는다: 땅을 눌러 고르고 「침략」(전투 씬으로) · 「상점」(지도 위 팝업 — 턴이 열릴 때 저절로 한 번 내려온다) · 「쉬기」.
    // 전투가 끝나 돌아왔거나(Aftermath) 쉬었으면 AI 영주들의 턴을 돌리고(EndTurn) 그 사건들을 지도에서 차례로 재생한 뒤 요약 → 「다음 턴」.
    // 통일 · 게임 오버도 여기서 띄운다(이벤트 장면 UI/EventVisitPopup.OpenEnd) → 「새 런」. 규칙은 RunState에, 그림은 UI/WorldMapView · UI/WorldSidePanel에 있다
    public class WorldManager : MonoBehaviour
    {
        public const string SceneName = "World";
        public const string BattleSceneName = "Battle";

        // AI 턴 재생 — 반란 · 연합 하나를 보여 주는 시간(초) · 침략 걸음들이 다 출발하기까지의 상한(초, 침략이 많으면 출발 간격이 짧아진다)
        private const float EventSeconds = 0.15f;
        private const float ReplaySeconds = 3f;
        // 반란 — 그 턴의 반란은 모두 한꺼번에 이 시간(초) 동안 벌어진다(사용자 11/1). 무리가 솟은 뒤 땅 색이 바뀌는 때(초, 그 안에서)
        private const float RebellionSeconds = 0.6f;
        private const float RebellionApply = 0.25f;
        // 침략 걸음이 잇달아 출발하는 간격(초) · 땅 색이 도착보다 얼마나 먼저 바뀌는지(초)
        private const float MarchStagger = 0.12f;
        private const float ApplyLead = 0.02f;
        // 내 침략 걸음 — 걸음이 끝났다는 신호 없이도 전투로 넘어가는 여유(초, 걸음의 길이에 더한다)
        private const float MarchSlack = 0.5f;
        // 상점 팝업이 내려오는 · 올라가는 시간(초)
        private const float ShopOpenSeconds = 0.3f, ShopCloseSeconds = 0.2f;
        // 못 누르는 버튼의 글 색(상점과 같다)
        private static readonly Color DisabledText = new Color(0.6f, 0.6f, 0.66f);
        // 이벤트 땅의 임시 색(회색 — 지도가 이 색을 어둡게 깔아 칠한다). 이벤트 땅 그림은 다음 단계(ROADMAP 2-5)
        private static readonly Color EventLandColor = new Color(0.85f, 0.85f, 0.85f);

        // Choose = 런의 첫 화면 — 내 국가를 고르는 중(고르면 첫 턴이 열린다). March = 「침략」을 눌러 내 영주가 그 땅으로 걸어가는 중(도착하면 전투 씬)
        // Visit = 이벤트 땅 방문 장면(UI/EventVisitPopup)이 떠 있다 — 끝나면 쉬기와 같은 길(AI 턴 재생)
        // Coalition = 연합 결성 팝업 — 떠 있거나 고르기 창이 닫히길 기다린다(닫으면 요약)
        private enum Stage { Choose, Command, March, Visit, News, Replay, Coalition, Summary }

        private static WorldManager _instance;

        [SerializeField] private BattleConfig _config;
        [SerializeField] private WorldMapView _map;
        [SerializeField] private WorldSidePanel _panel;
        [Tooltip("위 줄 — 턴 · 골드 · 목숨 · 땅")]
        [SerializeField] private Text _topBar;
        [SerializeField] private Button _invade;
        [SerializeField] private Text _invadeLabel;
        [SerializeField] private Button _rest;
        [SerializeField] private Button _shop;
        [Tooltip("상점 팝업(UI/ShopPanel) — 지도 영역을 덮는다. 세 번째 버튼(「상점」)으로 열고 판 안의 큰 「지도로」로 닫는다")]
        [SerializeField] private ShopPanel _shopPopup;
        [Tooltip("AI 턴을 재생하는 동안 화면 전체를 덮는 투명한 판 — 누르면 재생을 건너뛴다")]
        [SerializeField] private Button _skip;
        [Tooltip("왼쪽 패널의 알림(고른 땅 자리) — AI 턴 · 그 요약과 「다음 턴」(통일 · 게임 오버는 이벤트 장면)")]
        [SerializeField] private GameObject _notice;
        [SerializeField] private Text _noticeTitle;
        [SerializeField] private Text _noticeBody;
        [SerializeField] private Button _next;
        [SerializeField] private Text _nextLabel;
        [Tooltip("런을 시작할 때 내 국가를 고르는 판(UI/NationCard) — 순서 = RunState.NationChoices, 「시작」을 누르면 Chosen으로 알려 온다")]
        [SerializeField] private NationCard _nationCard;
        [Tooltip("이벤트 땅 방문 장면(UI/EventVisitPopup) — 「방문」으로 열고, 다 끝나면 Finished로 알려 온다")]
        [SerializeField] private EventVisitPopup _visit;

        private Stage _stage;
        private bool _shopOpen;
        // 0 = 다 올라갔다(꺼짐) · 1 = 다 내려왔다
        private float _shopSlide;
        private RectTransform _shopRect;
        private Vector2 _shopHome;
        private CanvasGroup _shopGroup;
        private RectTransform _shopTower;
        private CanvasGroup _shopTowerGroup;
        private int _selected = WorldMapView.None;
        // 화면에 보이는 턴 번호 — AI 턴을 재생하는 동안에는 방금 끝난 턴
        private int _turn;
        // 재생 중 지도에 보이는 주인 표(턴을 닫기 전 표에 사건을 하나씩 적용한다) — 재생이 끝나면 null(실제 판도를 그린다)
        private int[] _shown;
        private List<WorldEvent> _events;
        // 재생 일정 — 사건마다 시작하는 때 · 판도에 반영하는 때(재생이 시작된 뒤 초), 재생이 끝나는 때
        private float[] _eventStart, _eventApply;
        private bool[] _eventStarted, _eventApplied;
        private float _replayClock, _replayEnd;
        private float _marchClock;

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
            // 영주 그림 머리 위의 닉네임 — 내 그림에는 띄우지 않는다
            _map.Lords.NameOf = id =>
            {
                Lord lord = RunState.World.State.Lords[id];
                return lord.IsPlayer ? null : RunState.NameOf(lord);
            };
            _map.Lords.IsGhost = id => RunState.World.GhostOf(RunState.World.State.Lords[id]) != null;
            _map.Clicked += Select;
            _invade.onClick.AddListener(Invade);
            _rest.onClick.AddListener(Rest);
            _shop.onClick.AddListener(Shop);
            _skip.onClick.AddListener(FinishReplay);
            _next.onClick.AddListener(Next);
            _skip.gameObject.SetActive(false);
            _notice.SetActive(false);
            // 알림은 패널 전체 가운데 정렬 · 양옆 막대에서 Pad씩 — 위의 영주 그림 · 이름과 같은 가운데(UI/WorldSidePanel, 사용자 10/31)
            var notice = (RectTransform)_notice.transform;
            float noticeLeft = FrameBars.InfoLeft + FrameBars.Pad, noticeWidth = (FrameBars.PanelCenter - noticeLeft) * 2f;
            notice.anchoredPosition = new Vector2(noticeLeft, notice.anchoredPosition.y);
            notice.sizeDelta = new Vector2(noticeWidth, notice.sizeDelta.y);
            foreach (Text text in new[] { _noticeTitle, _noticeBody })
                text.rectTransform.sizeDelta = new Vector2(noticeWidth, text.rectTransform.sizeDelta.y);
            _noticeTitle.alignment = TextAnchor.MiddleCenter;
            _noticeBody.alignment = TextAnchor.UpperCenter;
            ((RectTransform)_next.transform).anchoredPosition = new Vector2(noticeWidth * 0.5f, ((RectTransform)_next.transform).anchoredPosition.y);
            _noticeMotion = PopupMotion.Attach((RectTransform)_notice.transform);
            _nationCard.gameObject.SetActive(false);
            // 방문을 마쳤다(RunState.ChooseVisit · DeclineVisit가 효과 · 닫힘 · Phase = Aftermath까지) — 쉬기와 같은 길
            if (_visit != null)
            {
                _visit.Finished += PlayAiTurn;
                // 연합 결성 팝업을 닫았다 — 지도가 뭉친 판도를 그리고 요약
                _visit.CoalitionsClosed += ShowSummary;
                // 나라 소식 · 외교를 다 골랐다 — 명령(상점이 저절로 내려온다)
                _visit.NewsClosed += StartCommand;
            }
            _shopPopup.MapRequested += () => SetShop(false);
            _shopRect = (RectTransform)_shopPopup.transform;
            _shopHome = _shopRect.anchoredPosition;
            _shopGroup = _shopPopup.GetComponent<CanvasGroup>();
            _shopGroup.blocksRaycasts = false;
            _shopPopup.gameObject.SetActive(false);
            // 상점의 내 탑이 설 판 — 왼쪽 패널 바로 위에 겹쳐, 패널의 탑 기둥 자리에(팝업과 같이 켜지고 꺼진다. 팝업처럼 미끄러지지는 않는다)
            var tower = new GameObject("ShopTower", typeof(RectTransform), typeof(CanvasGroup));
            _shopTower = (RectTransform)tower.transform;
            _shopTower.SetParent(_panel.transform.parent, false);
            _shopTower.SetSiblingIndex(_panel.transform.GetSiblingIndex() + 1);
            _shopTower.anchorMin = Vector2.zero;
            _shopTower.anchorMax = Vector2.one;
            _shopTower.offsetMin = _shopTower.offsetMax = Vector2.zero;
            _shopTowerGroup = tower.GetComponent<CanvasGroup>();
            tower.SetActive(false);
            _shopPopup.PlaceTower(_shopTower, _panel.TowerX);

            if (RunState.Phase == RunState.RunPhase.Aftermath)
            {
                if (RunState.LastOutcome == RunState.Outcome.Win)
                    _beaten = RunState.Opponent;
                FellBeaten();
                PlayAiTurn();
            }
            else if (RunState.IsOver)
                ShowEnd();
            else
            {
                // 씬만 다시 불러왔으면(치트) 고른 땅이 남아 있다
                _selected = RunState.PreviewRegion;
                if (!RunState.NationChosen)
                    ShowNations();
                else if (WaitingToStart)
                    _stage = Stage.Choose;
                else
                    BeginTurn();
            }
        }

        // 국가는 골랐지만 첫 턴 전에 고를 업그레이드가 대기 중이다 — 다 고를 때까지 첫 턴을 열지 않는다(Update). 고르기 창은 화면(UI)이 대기열을 보고 띄운다.
        // 시작 때 지역 특수를 고르던 것(10/13)은 없앴다(사용자 10/15) — 지금은 첫 턴 전에 대기가 생길 길이 없어 늘 지나간다
        private static bool WaitingToStart => RunState.NationChosen && !RunState.TurnOpened && RunState.PendingUpgrades.Count > 0;

        // 내 국가 고르기 — 카드(UI/NationCard)를 열고, 「시작」으로 고른 국가를 받으면 바로 첫 턴이 열린다
        private void ShowNations()
        {
            _stage = Stage.Choose;
            _turn = RunState.Round + 1;
            _nationCard.Chosen += nation =>
            {
                RunState.ChooseNation(nation);
                if (!WaitingToStart)
                    BeginTurn();
            };
            _nationCard.Open(RunState.NationChoices, _config.World);
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
            _shown = null;
            _notice.SetActive(false);
            // 나라 소식 · 외교(아웃게임기획 §3-2)가 있으면 상점보다 먼저 — 다 닫히면(NewsClosed) 명령으로
            if (_visit != null && _visit.OpenNews())
            {
                _stage = Stage.News;
                return;
            }
            StartCommand();
        }

        private void StartCommand()
        {
            _stage = Stage.Command;
            // 턴이 열리면 상점이 저절로 내려온다 — 턴마다 한 번(씬만 다시 불러온 것이면 안 내려온다)
            if (RunState.TakeShopCue())
                SetShop(true);
        }

        // 땅을 눌렀다 — 고른다(바다 · 지도 밖을 누르면 푼다). 이벤트 땅은 아직 고르지 않고 로그만(방문 화면은 다음 단계 — 지금은 치트 패널의 「방문」, ROADMAP 2-5)
        private void Select(int region)
        {
            if (_stage != Stage.Command || _shopOpen || _shopSlide != 0f)
                return;
            // 이벤트 땅도 고른다 — 왼쪽 패널이 그 이벤트를 보여 주고 「침략」이 「방문」이 된다(UI/WorldSidePanel)
            _selected = region;
        }

        private void Invade()
        {
            // 이벤트 땅이면 걷지 않고 방문 장면을 연다(전투 씬 없음)
            if (_stage == Stage.Command && _visit != null && RunState.VisitableEvents().Contains(_selected))
            {
                if (_visit.Open(_selected))
                    _stage = Stage.Visit;
                return;
            }
            if (_stage != Stage.Command || !RunState.InvadableRegions().Contains(_selected))
                return;
            // 내 영주가 그 땅으로 걸어 들어간 뒤에 전투로(사용자 10/12) — AI 턴 재생의 침략 걸음(UI/WorldLords.March)과 같은 걸음이다.
            // 걷는 동안은 명령을 받지 않는다(단계가 Command가 아니라 버튼이 숨고 땅도 못 고른다)
            _stage = Stage.March;
            _marchClock = 0f;
            _map.Lords.March(RunState.World.State.Player.Id, _selected, true);
        }

        // 내가 방금 전투에서 쓰러뜨린 영주(땅을 다 빼앗겼다)는 지도로 돌아온 순간 그 땅에 서 있다가, 내 영주가 달려들어 치면 날아간다(UI/WorldLords.Fell).
        // 최후의 연합이 멸망했으면 든 영주들도 다 같이
        private void FellBeaten()
        {
            WorldState state = RunState.World.State;
            Lord beaten = RunState.Opponent;
            IReadOnlyList<int> gains = RunState.LastGains;
            if (RunState.LastOutcome != RunState.Outcome.Win || beaten == null || beaten.Alive || gains.Count == 0)
                return;
            var fallen = new List<Lord> { beaten };
            if (state.CoalitionOf(beaten) is WorldState.Coalition coalition)
                foreach (int member in coalition.Members)
                    fallen.Add(state.Lords[member]);
            for (int i = 0; i < fallen.Count; i++)
                _map.Lords.Fell(gains[i % gains.Count], state.Player.Id, fallen[i].Id, RunState.ColorOf(fallen[i]));
        }

        // 걸음이 끝났다 — 침략을 확정하고 전투 씬으로
        private void StartBattle()
        {
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
            Schedule();
            _stage = Stage.Replay;
            _skip.gameObject.SetActive(true);
            ShowNotice("영주들의 차례", "화면을 누르면 건너뜁니다.", null);
        }

        // 왼쪽 패널의 알림 — 제목 · 읽는 글 · 버튼(next가 null이면 버튼 없이). 꺼져 있다 켜질 때만 판이 내려앉는다(떠 있는 채 글만 바뀌면 그대로)
        private PopupMotion _noticeMotion;

        private void ShowNotice(string title, string body, string next)
        {
            _noticeTitle.text = title;
            _noticeBody.text = BuildingTooltip.Highlight(body);
            _next.gameObject.SetActive(next != null);
            if (next != null)
                _nextLabel.text = next;
            if (_notice.activeSelf)
                return;
            _notice.SetActive(true);
            _noticeMotion.Open();
        }

        // 재생 일정을 짠다 — 침략은 영주가 그 땅으로 걸어 들어가는 걸음(UI/WorldLords)이고, 여럿이 조금씩 어긋나 동시에 걷는다.
        // 같은 영주 · 같은 땅이 얽힌 침략은 앞의 것이 끝난 뒤에 시작하고(순서가 뒤집혀 보이지 않게), 반란 · 연합은 앞의 걸음이 다 끝난 뒤에 벌어진다
        private void Schedule()
        {
            int count = _events.Count, marches = 0;
            foreach (WorldEvent e in _events)
                if (!e.Rebellion && !e.Coalition)
                    marches++;
            float stagger = Mathf.Min(MarchStagger, Mathf.Max(0f, ReplaySeconds - WorldLords.MarchSeconds) / Mathf.Max(1, marches));
            _eventStart = new float[count];
            _eventApply = new float[count];
            _eventStarted = new bool[count];
            _eventApplied = new bool[count];
            var lordFree = new Dictionary<int, float>();
            var regionFree = new Dictionary<int, float>();
            float cursor = 0f, rebellion = -1f;
            _replayEnd = 0f;
            for (int i = 0; i < count; i++)
            {
                WorldEvent e = _events[i];
                if (e.Rebellion)
                {
                    if (rebellion < 0f)
                    {
                        rebellion = _replayEnd;
                        cursor = _replayEnd += RebellionSeconds;
                    }
                    _eventStart[i] = rebellion;
                    _eventApply[i] = rebellion + RebellionApply;
                    continue;
                }
                if (e.Coalition)
                {
                    _eventStart[i] = _eventApply[i] = _replayEnd;
                    cursor = _replayEnd += EventSeconds;
                    continue;
                }
                float start = cursor;
                if (lordFree.TryGetValue(e.Lord, out float lord))
                    start = Mathf.Max(start, lord);
                if (regionFree.TryGetValue(e.Region, out float region))
                    start = Mathf.Max(start, region);
                _eventStart[i] = start;
                // 땅 색은 영주가 다 들어서기 바로 전에 바뀐다
                _eventApply[i] = start + WorldLords.MarchSeconds - ApplyLead;
                regionFree[e.Region] = start + WorldLords.MarchSeconds;
                lordFree[e.Lord] = start + WorldLords.MarchSeconds + (e.Won ? 0f : WorldLords.ReturnSeconds);
                _replayEnd = Mathf.Max(_replayEnd, lordFree[e.Lord]);
                cursor = start + stagger;
            }
            _replayClock = 0f;
        }

        // 이번 AI 턴 재생의 길이(초, 검사용)
        public float ReplayLength => _replayEnd;

        // 재생을 끝낸다(건너뛰기 포함) — 실제 판도를 그리고 요약을 띄운다
        // 이번 턴에 연합이 뭉쳤으면 요약 전에 연합 결성 팝업(UI/EventVisitPopup.OpenCoalitions) — 그동안 지도는 뭉치기 전 판도
        private void FinishReplay()
        {
            _map.Lords.Snap();
            _skip.gameObject.SetActive(false);
            var formed = new List<WorldState.Coalition>();
            for (int i = 0; i < _events.Count; i++)
            {
                WorldEvent e = _events[i];
                if (e.Coalition)
                    formed.Add(RunState.World.State.CoalitionOf(RunState.World.State.Lords[e.Lord]));
                else if (!_eventApplied[i])
                {
                    _eventApplied[i] = true;
                    e.Apply(_shown);
                }
            }
            if (formed.Count > 0 && _visit != null)
            {
                _formed = formed;
                _stage = Stage.Coalition;
                _notice.SetActive(false);
                return;
            }
            ShowSummary();
        }

        private void ShowSummary()
        {
            _shown = null;
            _stage = Stage.Summary;
            ShowNotice("영주들의 차례", RunState.TurnSummary, "다음 턴");
        }

        // 연합 결성 팝업을 띄울 연합들 — 이긴 전투의 결과 장면(멸망 보상) · 국가 위기가 먼저 닫힌 뒤에 뜬다(Update)
        private List<WorldState.Coalition> _formed;

        private void ShowEnd()
        {
            _shown = null;
            _stage = Stage.Summary;
            _selected = WorldMapView.None;
            _skip.gameObject.SetActive(false);
            // 왼쪽 알림 판이 아니라 이벤트 장면(UI/EventVisitPopup.OpenEnd) — 통일한 전투의 결과 장면(멸망 보상)이 먼저 닫힌 뒤에 뜬다(Update)
            _notice.SetActive(false);
            _ending = true;
        }

        // 런의 끝 장면을 띄울 차례를 기다리는 중
        private bool _ending;
        // 이긴 전투의 결과 장면을 띄울 차례를 기다리는 중 — 방금 쓰러뜨린(물러나게 한) 영주
        private Lord _beaten;

        // 끝 장면의 「새 런」
        private void NewRun()
        {
            RunState.StartNew(_config);
            SceneManager.LoadScene(SceneName);
        }

        private void Next() => BeginTurn();

        private void Update()
        {
            WorldState state = RunState.World.State;
            SlideShop();
            // 첫 턴 전 대기를 다 골랐다 — 첫 턴을 연다
            if (_stage == Stage.Choose && RunState.NationChosen && !WaitingToStart)
                BeginTurn();
            if (_stage == Stage.March)
            {
                // 걸음이 끝나면 전투로 — 걸음이 끝났다는 신호를 못 받아도 걸음의 길이 + 여유가 지나면 넘어간다
                // 프레임 시간을 묶는다(걸음 그림 — UI/WorldLords와 같은 상한) — 한 프레임이 길게 멈춰도 걸음을 건너뛰고 전투로 넘어가지 않게
                _marchClock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                if (_marchClock > 0f && (!_map.Lords.AnyMarching || _marchClock >= WorldLords.MarchSeconds + MarchSlack))
                {
                    StartBattle();
                    return;
                }
            }
            if (_stage == Stage.Replay)
            {
                // 한 프레임이 길어도(씬을 막 불러온 프레임 등) 사건이 한꺼번에 넘어가지 않게 프레임당 진행을 묶는다
                _replayClock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                for (int i = 0; i < _events.Count; i++)
                {
                    WorldEvent e = _events[i];
                    if (!_eventStarted[i] && _replayClock >= _eventStart[i])
                    {
                        _eventStarted[i] = true;
                        if (e.Rebellion)
                            _map.Lords.Rebel(e.Lord, e.Regions);
                        else if (!e.Coalition)
                            _map.Lords.March(e.Lord, e.Region, e.Won);
                    }
                    // 연합은 재생에서 땅을 칠하지 않는다 — 연합 결성 팝업을 닫을 때 바뀐다(CloseCoalitions)
                    if (!_eventApplied[i] && _replayClock >= _eventApply[i])
                    {
                        _eventApplied[i] = true;
                        if (!e.Coalition)
                            e.Apply(_shown);
                    }
                }
                if (_replayClock >= _replayEnd)
                    FinishReplay();
            }
            // 이긴 전투의 결과 장면(멸망 · 연합 물러남 · 연합 멸망 — 멸망 보상도 여기서 고른다) — AI 턴 재생이 끝난 뒤(요약 · 연합 결성 · 런의 끝보다 먼저)
            if (_beaten != null && (_stage == Stage.Summary || _stage == Stage.Coalition) && !_visit.Busy)
            {
                _visit.OpenAftermath(_beaten);
                _beaten = null;
            }
            if (_stage == Stage.Coalition && _formed != null && RunState.PendingUpgrades.Count == 0 && !_visit.Busy)
            {
                _visit.OpenCoalitions(_formed);
                _formed = null;
            }
            if (_ending && RunState.PendingUpgrades.Count == 0 && !_visit.Busy)
            {
                _ending = false;
                _visit.OpenEnd(RunState.Cleared, _turn, NewRun);
            }

            bool command = _stage == Stage.Command;
            // 고른 땅은 런 상태에 둔다 — 상점에 다녀와도 남고, 상점 패널이 같은 땅을 보여 준다
            if (command)
                // 이벤트 땅은 영주가 없다 — 미리 보는 상대(탑 · 업그레이드)로 넘기지 않는다
                RunState.Preview(_selected >= 0 && state.Graph.IsEvent[_selected] ? WorldMapView.None : _selected);
            List<int> invadable = command ? RunState.InvadableRegions() : null;
            bool[] open = null;
            if (invadable != null)
            {
                open = new bool[state.Owner.Length];
                foreach (int region in invadable)
                    open[region] = true;
            }
            // 상점 팝업이 지도를 다 덮은 동안에는 영주들이 걷지 않는다(보이지 않는 일을 하지 않는다)
            _map.Lords.enabled = !(_shopOpen && _shopSlide == 1f);
            _map.Lords.Group(state.Coalitions);
            _map.Show(_shown ?? state.Owner, lord => lord == WorldState.NoOwner ? EventLandColor : RunState.ColorOf(state.Lords[lord]), state.Player.Id, open, _selected);
            // 명령을 받는 동안에만 고른 땅 · 명령 버튼이 보인다 — 그 밖에는 그 자리에 알림(AI 턴 · 요약 · 끝)
            // 상점의 내 탑은 팝업이 보이는 동안(미끄러지는 동안 포함) 제자리에 서 있고, 팝업을 못 누르는 동안에는 같이 못 누른다
            bool shopShown = _shopOpen || _shopSlide > 0f;
            if (_shopTower.gameObject.activeSelf != shopShown)
                _shopTower.gameObject.SetActive(shopShown);
            _shopTowerGroup.blocksRaycasts = _shopGroup.blocksRaycasts;
            _panel.Show(_selected, command || _stage == Stage.March || _stage == Stage.Visit || _stage == Stage.News, shopShown);
            if (!command && _shopOpen)
                SetShop(false);
            // 상점이 열린 동안에는 명령 버튼 셋을 다 숨긴다 — 닫는 길은 팝업 안의 큰 「지도로」 하나(사용자 10/11), 닫아야 지도에서 고른다
            foreach (Button button in new[] { _invade, _rest, _shop })
            {
                bool shown = command && !_shopOpen;
                if (button.gameObject.activeSelf != shown)
                    button.gameObject.SetActive(shown);
            }
            _invade.interactable = command && (invadable.Contains(_selected) || RunState.VisitableEvents().Contains(_selected));
            _invadeLabel.color = _invade.interactable ? Color.white : DisabledText;
            _panel.ShowStatus(_topBar, _turn);
        }
    }
}

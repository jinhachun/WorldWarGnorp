using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 이벤트 땅 방문 장면(아웃게임기획 §3-1 · 시안 Captures/EventUI2/v2_*, 사용자 10/25 「지금 되게 좋은 듯」) — 지도 위 가운데 판 하나.
    // 판 = UI_Frame 판 선 · 제목(도트 36) + 바로 밑 안내 한 줄(깔끔 22) · 무대(그 땅 지형의 뒷배경 · 땅 선 · 인물 · 소품, 그림 1px = 화면 6px).
    // 후보(RunState.BeginVisit의 Choices)를 무대 위 사물로 세우고 그것을 직접 눌러 고른다 — 올리면 꺾쇠 ▼ + 고정 자리(판 위쪽 하늘)의 설명 창.
    // 대장간만 무대가 아니라 왼쪽 탑 기둥의 층을 누른다(탑은 한 번만 그린다 — 보관함 후보만 무대에 선다).
    // 고르기 전에는 닫을 수 없다(피의 제단만 「바치지 않기」 · 후보가 없으면 「닫기」 = RunState.DeclineVisit).
    // 고르면 RunState.ChooseVisit(효과 · 닫힘 · 턴 끝) → 결과 연출(고른 것이 결과 자리로 0.3초 날아가고 「+N」, 스타일 가이드 §5) → 판이 닫히고 지도의 그 땅이 깜박인 뒤 닫힘 → Finished.
    // 연출 중 화면을 누르면 건너뛴다. 무엇을 고를 수 있는지 · 효과는 RunState가 정한다 — 여기는 그리기와 누름만
    // 같은 판 · 같은 문법으로 연합 결성(OpenCoalitions) · 나라 소식 · 외교(OpenNews) · 국가 위기(OpenCrisis) · 통일 · 게임 오버(OpenEnd) · 이긴 전투의 결과(OpenAftermath — 멸망 · 연합 물러남 · 연합 멸망)도 연다
    public class EventVisitPopup : MonoBehaviour
    {
        // 그림 1px = 화면 K px(상점 짐꾼과 같다)
        private const int K = 6;
        // 지도 자리(화면 위 왼쪽 기준 px)와 판 크기 · 판 아래 끝에서 땅 선까지
        private const float MapLeft = 420f, MapTop = 67f, MapWidth = 1486f, MapHeight = 996f;
        private const float PanelWidth = 890f, PanelHeight = 600f, GroundUp = 110f;
        // 둘레 지도를 어둡게(사용자 10/25 그대로)
        private const float DimAlpha = 0.5f;
        // 움직임(스타일 가이드 §5) — 판 열림 · 닫힘은 UI/PopupMotion / 꺾쇠 4px · 0.07초 / 결과 날아감 0.3초 · 「+N」을 보여 주는 시간 / 지도 깜박임
        private const float CaretDrop = 4f, CaretSeconds = 0.07f;
        private const float FlySeconds = 0.3f, HoldSeconds = 0.6f, FlashSeconds = 0.6f;
        private const float FloatRise = 24f;
        // 탑 층 조각 안 기물 아이콘 배율(층 칸과 같다 — UI_TowerSlot)
        private const float FloorIcon = 0.6f;
        // 아직 고를 수 없는 것(피의 제단 — 하트를 바치기 전의 업그레이드)
        private const float WaitingAlpha = 0.45f;

        [Header("글")]
        [SerializeField] private Font _dotFont;
        [SerializeField] private Font _notoFont;
        [Header("부품 프리팹 (Assets/Prefabs/UI/Parts)")]
        [SerializeField] private RectTransform _framePart;
        [SerializeField] private Button _buttonPart;
        [SerializeField] private FloorLook _floorPart;
        [SerializeField] private Image _cellPart;
        [SerializeField] private Image _heartPart;
        [Header("소품 그림(임시 — Assets/Sprites/World/Event, 덮어쓰면 반영)")]
        [SerializeField] private Sprite _anvil;
        [SerializeField] private Sprite _altar;
        [SerializeField] private Sprite _candle;
        [SerializeField] private Sprite _scarecrow;
        [SerializeField] private Sprite _bloodAltar;
        [SerializeField] private Sprite _podium;
        [SerializeField] private Sprite _shrine;
        [SerializeField] private Sprite _chest;
        [SerializeField] private Sprite _chestLid;
        [SerializeField] private Sprite _sparks;
        [SerializeField] private Sprite _caret;
        [SerializeField] private Sprite _caretLeft;
        [Header("자연 소품(임시) — 화산 분화구 · 산길 이정표 · 늪 웅덩이 · 굴 입구, 비우면 안 그린다")]
        [SerializeField] private Sprite _vent;
        [SerializeField] private Sprite _signpost;
        [SerializeField] private Sprite _puddle;
        [SerializeField] private Sprite _cave;
        [Header("그 밖의 그림")]
        [SerializeField] private Sprite _coin;
        [SerializeField] private Sprite _glow;
        [SerializeField] private Sprite _lordFlag;
        [Header("인물 — 대장장이 · 사제 · 선동가")]
        [SerializeField] private UnitDefinition _smith;
        [SerializeField] private UnitDefinition _priest;
        [SerializeField] private UnitDefinition _agitator;
        [Header("이어지는 화면")]
        [SerializeField] private WorldMapView _map;
        [SerializeField] private WorldSidePanel _panel;
        [Tooltip("짐꾼 그림(국가 → 유닛)을 상점에서 읽는다 — ShopPanel.PorterOf")]
        [SerializeField] private ShopPanel _shop;

        // 판이 닫히고 지도 깜박임까지 끝났다 — 턴 흐름을 잇는다(WorldManager → AI 턴 재생)
        public event System.Action Finished;

        private enum Step { Closed, Choosing, Result, Flash, Coalition, News, End }

        // 고를 수 있는 것 하나 — 누르면 Choice번째 후보(Heart = 피의 제단의 하트)
        private class Thing
        {
            public EventVisitTarget Target;
            public int Choice;
            // 꺾쇠 자리(아래 가운데, 화면 위 왼쪽 기준) · 왼쪽 탑 층이면 옆을 가리키는 꺾쇠
            public Vector2 Caret;
            public bool Side;
            // 설명 창 — Owned가 있으면 기물 설명(ShowOwned)
            public string Title, Body;
            public OwnedBuilding Owned;
            // 올리면 밝아지는 테두리(없으면 null)
            public Image Frame;
            public Color FrameColor;
            // 날아가는 그림(결과)
            public RectTransform Visual;
            // 대장간 — 올리면 모루 위에 옅게 놓일 기물
            public BuildingDefinition Building;
        }

        private class Flight
        {
            public RectTransform Rect;
            public Vector2 From, To;
            public float Start, Seconds;
            public bool Walk;
            // 닿을 자리를 매 프레임 다시 읽는다(위 줄의 새 업그레이드 칸은 다음 프레임에 생긴다) — 없으면 To 그대로
            public System.Func<Vector2> Follow;
            // 걸음의 그림 1px(0이면 K) · 닿았을 때 한 번(연합 결성 — 깃발 색이 바뀐다)
            public int Scale;
            public System.Action Landed;
        }

        private RectTransform _root, _sheet, _things, _backdrop, _dock;
        private Image _dim, _ground, _block;
        // 판 · 무대(뒷배경을 자르는 칸) — 연합 결성은 판 높이를 내용에 맞춘다(PlaceSheet)
        private RectTransform _box, _stage;
        private Text _title, _line;
        private InlineIcons _lineIcons;
        private Button _action;
        private Text _actionLabel;
        private Image _caretImage;
        private Button _skip;
        private RectTransform _previousDock;

        private Step _step;
        private EventOffer _offer;
        private readonly List<Thing> _list = new List<Thing>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<(RectTransform Rect, float Start)> _floats = new List<(RectTransform, float)>();
        private Thing _hover;
        private float _hoverAt;
        private float _clock;
        private bool _offered;
        private int _chosen = -1;
        // 결과 연출에서 땅에 닿은 뒤 할 일(한 번)
        private System.Action _landed;
        private bool _landedDone;
        // 대장간 — 탑 층에 붙인 누름 받이(닫을 때 뗀다) · 날아가는 동안 비워 보일 층
        private readonly List<EventVisitTarget> _slotTargets = new List<EventVisitTarget>();
        private int _emptyFloor = -1;
        // 대장간 — 올린 기물이 모루 위에 놓일 자리(옅은 그림자)
        private FloorLook _ghost;
        private Image _ghostIcon;
        private const float GhostAlpha = 0.25f;

        // 판 자리(화면 위 왼쪽 기준 px)
        private static float Left => Mathf.Round(MapLeft + (MapWidth - PanelWidth) * 0.5f);
        private static float Top => Mathf.Round(MapTop + (MapHeight - PanelHeight) * 0.5f);
        private static float Center => Left + PanelWidth * 0.5f;
        private static float Ground => Top + PanelHeight - GroundUp;

        // 고르는 중이거나 결과를 보여 주는 중(닫히기 전)
        public bool Busy => _step != Step.Closed;

        private void Awake()
        {
            _root = (RectTransform)transform;
            // 국가 위기 동안 화면 전체를 막는 투명한 판(맨 아래) — 판 밖의 알림 판 「다음 턴」을 못 누르게
            _block = NewImage(_root, "Block", null, Color.clear);
            _block.raycastTarget = true;
            _block.rectTransform.anchorMin = Vector2.zero;
            _block.rectTransform.anchorMax = Vector2.one;
            _block.rectTransform.offsetMin = _block.rectTransform.offsetMax = Vector2.zero;
            _block.gameObject.SetActive(false);
            _dim = NewImage(_root, "Dim", null, new Color(0f, 0f, 0f, DimAlpha));
            _dim.raycastTarget = true;
            _dim.rectTransform.sizeDelta = new Vector2(MapWidth, MapHeight);
            At(_dim.rectTransform, MapLeft, MapTop, new Vector2(0f, 1f));
            // 판 · 글 · 무대는 한 묶음(열릴 때 같이 떨어진다)
            _sheet = NewRect(_root, "Sheet");
            _sheet.anchorMin = Vector2.zero;
            _sheet.anchorMax = Vector2.one;
            _sheet.offsetMin = _sheet.offsetMax = Vector2.zero;
            Image box = NewImage(_sheet, "Panel", null, Color.black);
            _box = box.rectTransform;
            box.raycastTarget = true;
            box.rectTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            At(box.rectTransform, Left, Top, new Vector2(0f, 1f));
            // 무대 — 판 안에서 잘린다(뒷배경 겹)
            RectTransform stage = _stage = NewRect(box.rectTransform, "Stage");
            stage.gameObject.AddComponent<RectMask2D>();
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0f, 1f);
            stage.anchoredPosition = new Vector2(4f, -4f);
            stage.sizeDelta = new Vector2(PanelWidth - 8f, Ground - Top - 4f);
            _backdrop = NewRect(stage, "Backdrop");
            _backdrop.anchorMin = _backdrop.anchorMax = _backdrop.pivot = Vector2.zero;
            RectTransform frame = Instantiate(_framePart, box.rectTransform);
            frame.name = "Frame";
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            Image ground = _ground = NewImage(_sheet, "Ground", null, Color.white);
            ground.rectTransform.sizeDelta = new Vector2(PanelWidth - 32f, 4f);
            At(ground.rectTransform, Left + 16f, Ground, new Vector2(0f, 1f));
            _title = NewText(_sheet, "Title", _dotFont, 36, PanelWidth - 80f);
            At(_title.rectTransform, Center, Top + 24f, new Vector2(0.5f, 1f));
            _line = NewText(_sheet, "Line", _notoFont, 22, PanelWidth - 80f);
            _line.horizontalOverflow = HorizontalWrapMode.Wrap;
            At(_line.rectTransform, Center, Top + 78f, new Vector2(0.5f, 1f));
            _lineIcons = _line.gameObject.AddComponent<InlineIcons>();
            // 설명 창 고정 자리 — 안내 줄 아래 하늘(장면을 가리지 않게)
            _dock = NewRect(_sheet, "TooltipDock");
            _dock.sizeDelta = new Vector2(700f, Ground - Top - 124f - 200f);
            At(_dock, Center, Top + 124f, new Vector2(0.5f, 1f));
            _things = NewRect(_sheet, "Things");
            _things.anchorMin = Vector2.zero;
            _things.anchorMax = Vector2.one;
            _things.offsetMin = _things.offsetMax = Vector2.zero;
            // 명령 버튼(「바치지 않기」 · 「받기」 · 「닫기」) — 판 오른쪽 아래, 땅 선 아래
            _action = Instantiate(_buttonPart, _sheet);
            _action.name = "Action";
            At((RectTransform)_action.transform, Left + PanelWidth - 24f, Top + PanelHeight - 14f - 80f, new Vector2(1f, 1f));
            _actionLabel = _action.transform.Find("LabelRow/Label").GetComponent<Text>();
            _action.transform.Find("LabelRow/Icon").gameObject.SetActive(false);
            _action.onClick.AddListener(Act);
            _caretImage = NewImage(_sheet, "Caret", _caret, Color.white);
            _caretImage.gameObject.SetActive(false);
            // 연출 중 화면 전체 — 누르면 건너뛴다
            Image catcher = NewImage(_root, "Skip", null, Color.clear);
            catcher.raycastTarget = true;
            _skip = catcher.gameObject.AddComponent<Button>();
            _skip.transition = Selectable.Transition.None;
            var skip = (RectTransform)_skip.transform;
            skip.anchorMin = Vector2.zero;
            skip.anchorMax = Vector2.one;
            skip.offsetMin = skip.offsetMax = Vector2.zero;
            _skip.onClick.AddListener(Skip);
            _skip.gameObject.SetActive(false);
            Show(false);
            _dim.gameObject.SetActive(false);
            // 판 열림 · 닫힘(어둠 포함) — 닫힘이 끝나야 판을 비우고 끈다(Close의 할 일). 새로 열 때는 닫히던 것부터 끝낸다(Complete)
            _motion = PopupMotion.Attach(_sheet, _dim);
        }

        private PopupMotion _motion;

        // 그 이벤트 땅의 방문 장면을 연다 — 방문할 수 없으면 false
        public bool Open(int region)
        {
            EventOffer offer = RunState.BeginVisit(region);
            if (offer == null)
                return false;
            _motion.Complete();
            _offer = offer;
            _step = Step.Choosing;
            _clock = 0f;
            _offered = false;
            _chosen = -1;
            _hover = null;
            _landed = null;
            _emptyFloor = -1;
            Clear();
            PlaceSheet(Top, PanelHeight, Ground);
            _previousDock = BuildingTooltip.Dock;
            BuildingTooltip.Dock = _dock;
            // 켠 뒤에 채운다(안내 줄의 아이콘 자리는 켜진 글에서 잰다)
            _dim.gameObject.SetActive(true);
            Show(true);
            _motion.Open();
            Backdrop(RunState.World.Map.TerrainOf(region), region);
            Build();
            return true;
        }

        // ── 국가 위기(사용자 10/23 · 팝업 장면 10/31) — 목숨이 0이 된 뒤의 고르기(RunState.OfferCrisis)를 같은 판 · 같은 무대로 연다.
        // 무대 = 내 땅 지형(더 어둡게) · 왼쪽에 내 영주 · 오른쪽에 영주를 마주 본 백성 무리 셋(사용자 10/31 「백성들이 모여서 요구하는 장면」).
        // 무리마다 팻말 하나를 들고, 팻말 안에 그 신화 업그레이드의 아이콘(원본 크기) — 팻말을 올리면 꺾쇠 · 흰 테두리 · 고정 자리 설명 창, 누르면 고른다.
        // 백성은 1px씩 들썩이고 팻말은 계단으로 오르내린다(그림 1px = K px 단위). 백성 = 내 국가 유닛들(Citizens — 사용자 10/31 「지금은 그 국가 유닛들로」), 팻말 = WorldConfig 「팻말」
        // 고르면 RunState.ChooseUpgrade(카드 고르기와 같다) → 팻말의 아이콘이 위 줄 업그레이드 칸으로 날아감 → 판이 닫힌다(지도 깜박임 · Finished 없음 — 턴 흐름은 요약 알림이 잇는다).
        // 건너뛸 수 없고, 고르기 전에는 판 밖(알림 판 포함)도 막는다(_block). 부르는 쪽 = UI/UpgradePicker(대기열 맨 앞이 위기일 때)
        private const string CrisisTitle = "국가 위기";
        private const string CrisisLine = "백성들이 새 국가 구조를 요구합니다.";
        private const float CrisisBackdrop = 0.1f;
        // 무리 사이 · 무리 안 백성 사이 틈(px) · 팻말을 드는 높이(막대 아래 끝, 땅에서 그림 px)
        private const float CrowdGap = 180f, CrowdPad = 2f;
        private const int PlacardHold = 3;
        // 들썩임 · 팻말 오르내림 — 계단 하나의 시간(초)과 계단마다의 높이(그림 px)
        private const float BobSeconds = 0.25f, WaveSeconds = 0.2f;
        private static readonly int[] Bob = { 0, 0, 1, 0 }, Wave = { 0, 1, 2, 1 };

        private RunState.UpgradeOffer _crisis;
        // 움직이는 백성 · 팻말 — 제자리(anchoredPosition) · 계단 차례 어긋남 · 팻말인가
        private readonly List<(RectTransform Rect, Vector2 Home, int Offset, bool Placard)> _crowd = new List<(RectTransform, Vector2, int, bool)>();

        public void OpenCrisis(RunState.UpgradeOffer offer)
        {
            _motion.Complete();
            _offer = null;
            _crisis = offer;
            _step = Step.Choosing;
            _clock = 0f;
            _chosen = -1;
            _hover = null;
            _landed = null;
            _emptyFloor = -1;
            Clear();
            PlaceSheet(Top, PanelHeight, Ground);
            _previousDock = BuildingTooltip.Dock;
            BuildingTooltip.Dock = _dock;
            _block.gameObject.SetActive(true);
            _dim.gameObject.SetActive(true);
            Show(true);
            _motion.Open();
            _ground.gameObject.SetActive(true);
            _action.gameObject.SetActive(false);
            _title.text = CrisisTitle;
            SetLine(CrisisLine);
            WorldState state = RunState.World.State;
            int home = System.Array.IndexOf(state.Owner, state.Player.Id);
            if (home >= 0)
                Backdrop(RunState.World.Map.TerrainOf(home), home, CrisisBackdrop);
            // 내 영주(왼쪽, 오른쪽을 본다)
            LordFigure(state.Player.Id, RunState.ColorOf(state.Player), Center - 300f, Ground - BodyHeight(state.Player.Id) * K * 0.5f, K, false);
            // 백성 무리 — 후보마다 하나(가운데 무리는 셋, 양옆은 둘)
            WorldConfig world = RunState.Config.World;
            Sprite placard = world.Placard;
            int n = offer.Choices.Length, person = 0;
            List<UnitDefinition> citizens = Citizens();
            Color me = RunState.ColorOf(state.Player);
            for (int i = 0; i < n; i++)
            {
                UpgradeDefinition choice = offer.Choices[i];
                float x = Mathf.Round(Center + 140f + (i - (n - 1) * 0.5f) * CrowdGap);
                // 무리 = 내 국가 유닛들(돌려 가며) — 영주 쪽(왼쪽)을 본다. 그림 폭이 유닛마다 달라 붙여 세운 뒤 무리 가운데를 팻말 아래(x)에.
                // 팻말은 무리 다음에 그린다 — 판보다 큰 유닛(미노타우로스 등)이 있으면 팻말이 그 앞에 와야 가려지지 않는다
                int members = i == (n - 1) / 2 && n > 1 ? 3 : 2;
                var group = new List<RectTransform>();
                float width = (members - 1) * CrowdPad;
                for (int j = 0; j < members; j++)
                {
                    RectTransform unit = NextCitizen(citizens, ref person, K, true, me, float.MaxValue);
                    if (unit == null)
                        break;
                    group.Add(unit);
                    width += unit.sizeDelta.x;
                }
                float at = x - width * 0.5f;
                foreach (RectTransform unit in group)
                {
                    PlaceFigure(unit, Mathf.Round(at + unit.sizeDelta.x * 0.5f), Ground, true);
                    _crowd.Add((unit, unit.anchoredPosition, _crowd.Count * 3 + 1, false));
                    at += unit.sizeDelta.x + CrowdPad;
                }
                Image board = Prop(placard, x, Ground - PlacardHold * K);
                // 무리가 모두 판(막대 위 8px) 아래에 들어가면 팻말을 무리 뒤로 — 막대가 몸 뒤로 간다(10/31 승인된 모습)
                float tallest = 0f;
                foreach (RectTransform unit in group)
                    tallest = Mathf.Max(tallest, unit.sizeDelta.y);
                if (group.Count > 0 && tallest <= (PlacardHold + placard.rect.height - 8f) * K)
                    board.transform.SetSiblingIndex(group[0].GetSiblingIndex());
                // 아이콘 — 판 안쪽 검은 칸 한가운데(판 = 막대 위 그림 8px · 가운데가 막대 끝에서 12px)
                Image icon = NewImage(board.rectTransform, "Icon", choice.Icon, Color.white);
                icon.SetNativeSize();
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(0f, (placard.rect.height - 4f) * K);
                _crowd.Add((board.rectTransform, board.rectTransform.anchoredPosition, i * 2, true));
                Add(new Thing { Target = Hit(board.rectTransform), Choice = i, Caret = new Vector2(x, Ground - (PlacardHold + placard.rect.height + Wave[2]) * K - 10f),
                    Title = choice.DisplayName, Body = BuildingTooltip.Highlight(choice.Describe(choice.ValueAt(offer.Rarity))),
                    Frame = board, FrameColor = offer.Rarity.FrameColor(), Visual = icon.rectTransform });
            }
        }

        // 백성 들썩임 · 팻말 오르내림 — 고르는 동안만. 계단 차례는 저마다 어긋난다
        private void Crowd()
        {
            foreach ((RectTransform rect, Vector2 home, int offset, bool raised) in _crowd)
            {
                if (rect == null)
                    continue;
                int[] steps = raised ? Wave : Bob;
                int at = (Mathf.FloorToInt(Time.unscaledTime / (raised ? WaveSeconds : BobSeconds)) + offset) % steps.Length;
                rect.anchoredPosition = home + new Vector2(0f, steps[at] * (_aftermath ? EndK : K));
            }
        }

        // ── 런의 끝(Docs/팝업_이벤트화면_계획.md K2, 10/31) — 통일 · 게임 오버를 같은 판 · 같은 무대로. 판 오른쪽 아래 「새 런」 = newRun(WorldManager가 넘긴다).
        // 통일 = 가운데 내 영주에게 왕관이 계단으로 내려앉고, 양옆에서 내 국가 유닛들(백성)이 뛰며, 쓰러진 영주들이 무릎 꿇고 고개를 숙인다. 양 끝에 내 색 깃발.
        // 게임 오버 = 내 나라 성이 먼지를 내며 계단으로 땅에 꺼지고, 내 영주는 깃발 색이 회색으로 빠진 뒤 무릎 꿇고, 백성들은 등을 돌려 떠난다.
        // 고를 것이 없어 설명 창 자리(하늘)가 필요 없다 — 판을 낮게(EndHeight) 잡고 인물을 크게(EndK) 그린다(사용자 10/31 「빈 곳이랑 인물 크기」).
        // 움직임은 전부 그림 px 계단(회전 · 번쩍임 없음). 건너뛸 수 없고 판 밖도 막는다(_block). 땅 선 아래로 꺼지는 것(성 · 무릎 · 먼지 · 떠나는 백성)은 무대(_backdrop) 안에 두어 잘리게 한다
        private const float EndBackdrop = 0.1f;
        // 인물 그림 1px = 화면 EndK px(영주 · 왕관 · 쓰러진 영주 · 백성 · 깃발 모두) · 판 높이 · 판 위 끝에서 땅 선까지
        private const int EndK = 9;
        private const float EndHeight = 420f, EndGroundDown = 310f;
        // 왕관 — 머리 위 몇 그림 px에서 출발하나 · 계단 하나의 시간
        private const int CrownLift = 4;
        private const float CrownSeconds = 0.15f;
        // 백성 뛰기(그림 px 계단) · 계단 하나의 시간 · 백성 사이(px)
        private static readonly int[] Jump = { 0, 0, 1, 2, 1, 0 };
        private const float JumpSeconds = 0.1f;
        private const float CitizenPad = 8f;
        // 쓰러진 영주(통일) — 최대 몇 명(양옆에 하나씩) · 고개 숙임 계단(그림 px)
        private const int FallenMax = 2;
        private static readonly int[] Bow = { 0, 0, 0, 1, 1 };
        private const float BowSeconds = 0.3f;
        // 성(게임 오버) — 그림 1px = 화면 몇 px(128×64 그림이라 인물과 같은 배율로는 판에 안 들어간다) · 한 계단에 꺼지는 그림 px · 최대 꺼짐(그림 px) · 계단 간격
        private const int CastleScale = 3, SinkStep = 2, SinkMax = 30;
        private const float SinkSeconds = 0.35f;
        // 내 영주 깃발 색이 빠지는 계단 수 · 계단 간격(마지막 계단 다음에 무릎 꿇는다)
        private const int DrainSteps = 3;
        private const float DrainSeconds = 0.3f;
        // 떠나는 백성 — 출발 간격 · 걸음 속도(px/초)
        private const float LeaveStagger = 0.5f, LeaveSpeed = 90f;
        // 먼지 조각 — 계단 하나의 시간 · 계단 수(계단마다 그림 1px 오르고, 마지막 계단에서 작아진다)
        private const float DustSeconds = 0.12f;
        private const int DustSteps = 3;
        private static readonly Color Drained = new Color(0.45f, 0.45f, 0.45f);

        private System.Action _newRun;
        private bool _cleared;
        private int _endLord;
        private Color _endColor;
        private float _endGround;
        private RectTransform _crown, _castle, _endFigure;
        private Vector2 _crownHome, _castleHome, _endFigureHome;
        private int _sunk, _drained;
        private readonly List<(RectTransform Rect, Vector2 Home, int Offset, bool Jumps)> _cheer = new List<(RectTransform, Vector2, int, bool)>();
        private readonly List<(RectTransform Rect, Vector2 Home, float Start)> _leavers = new List<(RectTransform, Vector2, float)>();
        private readonly List<(RectTransform Rect, Vector2 Home, float Born)> _dust = new List<(RectTransform, Vector2, float)>();
        private readonly System.Random _dustRandom = new System.Random(7);

        public void OpenEnd(bool cleared, int turn, System.Action newRun)
        {
            _motion.Complete();
            _offer = null;
            _crisis = null;
            _newRun = newRun;
            _cleared = cleared;
            _step = Step.End;
            _clock = 0f;
            _hover = null;
            _landed = null;
            _emptyFloor = -1;
            Clear();
            float top = Mathf.Round(MapTop + (MapHeight - EndHeight) * 0.5f);
            _endGround = top + EndGroundDown;
            PlaceSheet(top, EndHeight, _endGround);
            _block.gameObject.SetActive(true);
            _dim.gameObject.SetActive(true);
            Show(true);
            _motion.Open();
            _ground.gameObject.SetActive(true);
            _caretImage.gameObject.SetActive(false);
            _title.text = cleared ? "통일!" : "게임 오버";
            SetLine(BuildingTooltip.ColorNumbers(cleared ? $"턴 {turn}에 모든 땅을 차지했습니다." : $"턴 {turn}에 국가 위기를 넘기지 못하고 무너졌습니다."));
            SetAction("새 런");
            WorldState state = RunState.World.State;
            _endLord = state.Player.Id;
            _endColor = RunState.ColorOf(state.Player);
            // 게임 오버여도 내 땅은 지도에 남아 있다(영주만 쓰러진다) — 내 땅 지형
            int home = System.Array.IndexOf(state.Owner, state.Player.Id);
            if (home >= 0)
                Backdrop(RunState.World.Map.TerrainOf(home), home, EndBackdrop);
            if (cleared)
                BuildUnified(state);
            else
                BuildFallen();
        }

        private void BuildUnified(WorldState state)
        {
            float g = _endGround, body = BodyHeight(_endLord);
            _endFigure = LordFigure(_endLord, _endColor, Center, g - body * EndK * 0.5f, EndK, false);
            // 왕관 — 머리(몸 그림 위 끝) 위 CrownLift px에서 계단으로 내려앉는다
            Sprite crownLook = RunState.Config.World.EventCrown;
            if (crownLook != null)
            {
                _crown = Prop(crownLook, Center, g - (body + CrownLift) * EndK, EndK).rectTransform;
                _crownHome = _crown.anchoredPosition;
            }
            // 쓰러진 영주들 — 내 영주 양옆에 하나씩, 깃발 없이 무릎 꿇고(다리 한 줄이 땅 아래) 내 영주 쪽을 본다
            int fallen = 0;
            foreach (Lord lord in state.Lords)
            {
                if (lord.IsPlayer || fallen >= FallenMax)
                    continue;
                bool right = fallen % 2 == 1;
                Sprite look = LordPaint.Of(WorldLords.BodyOf(lord.Id), RunState.ColorOf(lord));
                Image kneel = NewImage(_things, "Fallen", look, Color.white);
                kneel.rectTransform.sizeDelta = look.rect.size * EndK;
                if (right)
                    kneel.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                Sunken(kneel.rectTransform, Center + (right ? 1f : -1f) * 100f, g + EndK, new Vector2(0.5f, 0f));
                _cheer.Add((kneel.rectTransform, kneel.rectTransform.anchoredPosition, fallen * 2, false));
                fallen++;
            }
            // 양 끝에 꽂힌 내 색 깃발(지도 영주의 깃발 그림) — 장대가 바깥쪽, 백성 뒤에
            foreach (float side in new[] { -1f, 1f })
            {
                Image banner = Prop(_lordFlag, Center + side * 400f, g, EndK);
                banner.color = _endColor;
                if (side > 0f)
                    banner.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
            // 백성 = 내 국가 유닛들 — 양옆에 판 끝까지 들어가는 만큼 내 영주 쪽을 보고 뛴다
            List<UnitDefinition> citizens = Citizens();
            int person = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                float inner = 160f;
                for (int j = 0; ; j++)
                {
                    RectTransform unit = NextCitizen(citizens, ref person, EndK, side > 0f, _endColor, PanelWidth * 0.5f - 12f - inner);
                    if (unit == null)
                        break;
                    PlaceFigure(unit, Center + side * (inner + unit.sizeDelta.x * 0.5f), g, side > 0f);
                    _cheer.Add((unit, unit.anchoredPosition, person * 2 + j, true));
                    inner += unit.sizeDelta.x + CitizenPad;
                }
            }
        }

        private void BuildFallen()
        {
            float g = _endGround;
            // 내 나라 성 — 계단으로 땅 아래로 꺼진다(무대 안이라 땅 선 아래는 잘린다)
            Sprite castle = RunState.Config.World.Castle(RunState.Nation);
            if (castle != null)
            {
                Image image = NewImage(_things, "Castle", LordPaint.Of(castle, Drained), Color.white);
                image.rectTransform.sizeDelta = castle.rect.size * CastleScale;
                Sunken(image.rectTransform, Center - 200f, g, new Vector2(0.5f, 0f));
                _castle = image.rectTransform;
                _castleHome = _castle.anchoredPosition;
            }
            // 내 영주 — 성 오른쪽에서 성(왼쪽)을 본다. 깃발 색이 빠진 뒤 무릎 꿇는다(몸 한 줄이 땅 아래로)
            float y = g - BodyHeight(_endLord) * EndK * 0.5f;
            _endFigure = LordFigure(_endLord, _endColor, Center + 60f, y, EndK, true);
            Sunken(_endFigure, Center + 60f, y, new Vector2(0.5f, 0.5f));
            _endFigureHome = _endFigure.anchoredPosition;
            // 백성 = 내 국가 유닛들 — 영주 오른쪽에서 등을 돌려(오른쪽을 보고) 판 밖으로 걸어 나간다(무대 끝에서 잘린다). 바깥쪽부터 차례로(겹치지 않게)
            List<UnitDefinition> citizens = Citizens();
            var line = new List<RectTransform>();
            float inner = 200f;
            int person = 0;
            while (true)
            {
                RectTransform unit = NextCitizen(citizens, ref person, EndK, false, _endColor, PanelWidth * 0.5f - 12f - inner);
                if (unit == null)
                    break;
                Sunken(unit, Center + inner, g, Vector2.zero);
                line.Add(unit);
                inner += unit.sizeDelta.x + CitizenPad;
            }
            for (int i = 0; i < line.Count; i++)
                _leavers.Add((line[i], line[i].anchoredPosition, PopupMotion.OpenSeconds + 0.6f + (line.Count - 1 - i) * LeaveStagger));
        }

        // 다음 백성 그림 — citizens를 person 차례부터 돌며, 폭이 maxWidth 이하인 첫 유닛을 그린다(한 바퀴 돌아도 없으면 null)
        private RectTransform NextCitizen(List<UnitDefinition> citizens, ref int person, int k, bool left, Color paint, float maxWidth)
        {
            for (int tried = 0; tried < citizens.Count; tried++)
            {
                RectTransform unit = Figure(citizens[person++ % citizens.Count], 0f, 0f, left, paint, k);
                if (unit.sizeDelta.x <= maxWidth)
                    return unit;
                Destroy(unit.gameObject);
            }
            return null;
        }

        // 백성 자리에 서는 내 국가 유닛들 — 상점 기물 중 내 국가 기물이 소환하는 유닛, 크기 1(충돌 박스 키 1)만, 같은 것은 한 번만(돌려 쓴다).
        // 기사 · 미노타우로스처럼 큰 유닛은 세우지 않는다(사용자 10/31)
        // nation을 주면 그 국가의 유닛들(쓰러뜨린 영주의 유닛 — 이긴 전투의 결과 장면)
        private static List<UnitDefinition> Citizens() => Citizens(RunState.Nation);

        private static List<UnitDefinition> Citizens(BuildingNation nation)
        {
            var list = new List<UnitDefinition>();
            foreach (BuildingDefinition building in RunState.Pool)
            {
                UnitDefinition unit = building != null && building.Nation == nation ? building.Unit : null;
                if (unit != null && !list.Contains(unit) && unit.PrefabFor(Team.Ally).GetComponent<BoxCollider2D>().size.y <= 1.01f)
                    list.Add(unit);
            }
            return list;
        }

        // ── 이긴 전투의 결과(Docs/팝업_이벤트화면_계획.md K2, 사용자 10/31) — 지도로 돌아와 AI 턴 재생이 끝난 뒤(WorldManager). 런의 끝과 같은 판 · 같은 배율(EndK).
        // 멸망 = 쓰러뜨린 영주가 내 영주 앞에 무릎 꿇고 고개를 숙이며 깃발이 계단으로 내 색이 된다 · 백성(내 국가 유닛들)이 양옆에서 뛴다.
        // 연합 물러남 = 바탕 영주(크게)와 흡수된 영주들(작게)이 등을 돌려 무대 오른쪽 밖으로 걸어 나간다 · 「앞으로 N번」.
        // 연합 멸망 = 연합 영주들이 무릎 꿇고 깃발이 내 색이 된다.
        // 멸망 보상(RunState.UpgradeOffer.Fall)이 있으면 판을 원래 높이로(설명 창 자리) — 쓰러뜨린 영주의 유닛들이 팻말로 지역 특수를 내민다.
        // 고르는 길 · 결과 날아감은 국가 위기와 같다(_crisis = 그 보상). 「건너뛰기 +N골드」 = RunState.SkipUpgrade, 보상이 없으면 「닫기」.
        // 닫혀도 턴 흐름을 잇지 않는다(요약 알림이 그대로 있다)
        // 흡수된 영주의 그림 1px · 무릎 꿇는 흡수된 영주 수(보상이 있으면 팻말 자리라 바탕 영주만) · 팻말 사이(px)
        private const int SmallK = 5, KneelMembers = 3;
        private const float PlacardStep = 150f;
        // 깃발이 내 색이 되는 계단 수 · 계단 간격 · 첫 계단까지(초, 판이 다 내려앉은 뒤부터)
        private const int TurnSteps = 3;
        private const float TurnSeconds = 0.3f, TurnDelay = 0.6f;
        // 물러남 — 등을 돌리는 때 · 출발 간격 · 걸음 속도(px/초)
        private const float RetreatTurn = 0.5f, RetreatStagger = 0.25f, RetreatSpeed = 120f;

        private bool _aftermath;
        private float _sceneClock;
        private Color _turnTo;
        private int _turned;
        private readonly List<(RectTransform Rect, int Lord, Color From, int K)> _turning = new List<(RectTransform, int, Color, int)>();
        private readonly List<(RectTransform Rect, Vector2 Home, float Start, int K)> _retreat = new List<(RectTransform, Vector2, float, int)>();

        public void OpenAftermath(Lord beaten)
        {
            WorldState state = RunState.World.State;
            WorldState.Coalition coalition = state.CoalitionOf(beaten);
            bool retreat = coalition != null && beaten.Alive;
            RunState.UpgradeOffer offer = RunState.PendingUpgrades.Count > 0 && RunState.PendingUpgrades[0].Fall ? RunState.PendingUpgrades[0] : null;
            _motion.Complete();
            _offer = null;
            _crisis = offer;
            _step = Step.Choosing;
            _clock = 0f;
            _chosen = -1;
            _hover = null;
            _landed = null;
            _emptyFloor = -1;
            Clear();
            _aftermath = true;
            _sceneClock = 0f;
            float top = offer != null ? Top : Mathf.Round(MapTop + (MapHeight - EndHeight) * 0.5f);
            _endGround = offer != null ? Ground : top + EndGroundDown;
            PlaceSheet(top, offer != null ? PanelHeight : EndHeight, _endGround);
            _previousDock = BuildingTooltip.Dock;
            BuildingTooltip.Dock = _dock;
            _block.gameObject.SetActive(true);
            _dim.gameObject.SetActive(true);
            Show(true);
            _motion.Open();
            _ground.gameObject.SetActive(true);
            _caretImage.gameObject.SetActive(false);
            string name = RunState.NameOf(beaten);
            _title.text = coalition == null ? $"{name} 멸망" : retreat ? $"{name} 연합 퇴각" : $"{name} 연합 멸망";
            // 유령 영주(다른 플레이어의 기록)를 쓰러뜨렸으면 맨 앞 줄에(사용자 11/1) — 정산이 유령 표에서 뺐어도 읽는다
            bool ghost = !retreat && (RunState.World.GhostOf(beaten) ?? RunState.World.FallenGhostOf(beaten)) != null;
            SetLine((ghost ? $"{name}의 기록을 꺾었습니다.\n" : "") + BuildingTooltip.ColorNumbers(retreat ? $"앞으로 {state.CoalitionWinsLeft(coalition)}번 더 이기면 연합은 멸망합니다."
                : offer != null ? "남긴 지역 특수 하나를 고릅니다."
                : coalition == null ? "모든 땅을 차지했습니다." : "연합의 모든 땅을 차지했습니다."));
            SetAction(offer != null ? $"건너뛰기 <color={BuildingTooltip.NumberColor}>+{RunState.UpgradeSkipGold}</color>골드" : "닫기");
            _turnTo = RunState.ColorOf(state.Player);
            int home = System.Array.IndexOf(state.Owner, state.Player.Id);
            if (home >= 0)
                Backdrop(RunState.World.Map.TerrainOf(home), home, EndBackdrop);

            float g = _endGround;
            // 내 영주(오른쪽을 본다) — 깃발이 왼쪽으로 12px 삐져나온다. 백성은 그 바깥에서 뛴다
            float lordX = Center + (offer != null ? -240f : -150f);
            LordFigure(state.Player.Id, _turnTo, lordX, g - BodyHeight(state.Player.Id) * EndK * 0.5f, EndK, false);
            List<UnitDefinition> citizens = Citizens();
            int person = 0;
            float inner = Center - lordX + 12f * EndK + CitizenPad;
            for (int j = 0; ; j++)
            {
                RectTransform unit = NextCitizen(citizens, ref person, EndK, false, _turnTo, PanelWidth * 0.5f - 12f - inner);
                if (unit == null)
                    break;
                PlaceFigure(unit, Center - inner - unit.sizeDelta.x * 0.5f, g, false);
                _cheer.Add((unit, unit.anchoredPosition, j * 2, true));
                inner += unit.sizeDelta.x + CitizenPad;
            }

            // 쓰러뜨린 영주(바탕 · 크게)와 흡수된 영주들(작게) — 내 영주를 보고(왼쪽) 줄지어 선다. 깃발은 바깥쪽(오른쪽)
            var lords = new List<(int Id, int K)> { (beaten.Id, EndK) };
            if (coalition != null)
                for (int i = 0; i < coalition.Members.Length && i < (offer != null ? 0 : KneelMembers); i++)
                    lords.Add((coalition.Members[i], SmallK));
            float at = Center + (offer != null ? -110f : 20f) - 4f * EndK;
            for (int i = 0; i < lords.Count; i++)
            {
                (int id, int k) = lords[i];
                float x = at + 4f * k;
                float y = g - BodyHeight(id) * k * 0.5f;
                // 흡수된 영주도 바탕 영주의 색(연합 결성 팝업에서 합류하며 바뀐 색 그대로)
                Color color = RunState.ColorOf(beaten);
                RectTransform figure = LordFigure(id, color, x, y, k, true);
                if (retreat)
                {
                    // 바깥쪽(오른쪽)부터 출발한다 — 겹치지 않게
                    Sunken(figure, x, y, new Vector2(0.5f, 0.5f));
                    _retreat.Add((figure, figure.anchoredPosition, RetreatTurn + 0.3f + (lords.Count - 1 - i) * RetreatStagger, k));
                }
                else
                {
                    // 무릎 꿇는다(몸 한 줄이 땅 아래) · 바탕 영주는 고개를 숙인다 · 깃발이 계단으로 내 색이 된다
                    Sunken(figure, x, y + k, new Vector2(0.5f, 0.5f));
                    if (k == EndK)
                        _cheer.Add((figure, figure.anchoredPosition, 0, false));
                    _turning.Add((figure, id, color, k));
                }
                at = x + 12f * k + 2f * SmallK;
            }

            // 그 밖의 자리 — 보상이 없으면 내 백성이 오른쪽에서도 뛴다(물러남은 연합이 떠난 자리를 비워 둔다)
            if (offer == null)
            {
                if (retreat)
                    return;
                inner = at - Center + CitizenPad;
                for (int j = 0; ; j++)
                {
                    RectTransform unit = NextCitizen(citizens, ref person, EndK, true, _turnTo, PanelWidth * 0.5f - 12f - inner);
                    if (unit == null)
                        break;
                    PlaceFigure(unit, Center + inner + unit.sizeDelta.x * 0.5f, g, true);
                    _cheer.Add((unit, unit.anchoredPosition, j * 2 + 1, true));
                    inner += unit.sizeDelta.x + CitizenPad;
                }
                return;
            }

            // 멸망 보상 — 후보마다 쓰러뜨린 영주의 유닛 하나가 팻말을 들고 내 영주를 본다(국가 위기의 팻말과 같다)
            Sprite placard = RunState.Config.World.Placard;
            List<UnitDefinition> holders = Citizens(RunState.NationOf(beaten));
            Color theirs = RunState.ColorOf(beaten);
            int holder = 0, n = offer.Choices.Length;
            for (int i = 0; i < n; i++)
            {
                UpgradeDefinition choice = offer.Choices[i];
                float x = Mathf.Round(Center + 240f + (i - (n - 1) * 0.5f) * PlacardStep);
                RectTransform unit = NextCitizen(holders, ref holder, EndK, true, theirs, float.MaxValue);
                if (unit != null)
                {
                    PlaceFigure(unit, x, g, true);
                    _crowd.Add((unit, unit.anchoredPosition, i * 3 + 1, false));
                }
                Image board = Prop(placard, x, g - PlacardHold * EndK, EndK);
                if (unit != null && unit.sizeDelta.y <= (PlacardHold + placard.rect.height - 8f) * EndK)
                    board.transform.SetSiblingIndex(unit.GetSiblingIndex());
                Image icon = NewImage(board.rectTransform, "Icon", choice.Icon, Color.white);
                icon.SetNativeSize();
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(0f, (placard.rect.height - 4f) * EndK);
                _crowd.Add((board.rectTransform, board.rectTransform.anchoredPosition, i * 2, true));
                Add(new Thing { Target = Hit(board.rectTransform), Choice = i, Caret = new Vector2(x, g - (PlacardHold + placard.rect.height + Wave[2]) * EndK - 10f),
                    Title = choice.DisplayName, Body = BuildingTooltip.Highlight(choice.Describe(choice.ValueAt(offer.Rarity))),
                    Frame = board, FrameColor = offer.Rarity.FrameColor(), Visual = icon.rectTransform });
            }
        }

        // 이긴 전투의 결과 장면의 움직임 — 판이 다 내려앉은 뒤부터
        private void AftermathScene()
        {
            _sceneClock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float t = _sceneClock - PopupMotion.OpenSeconds;
            Cheer();
            int turned = t < TurnDelay ? 0 : Mathf.Min(TurnSteps, 1 + Mathf.FloorToInt((t - TurnDelay) / TurnSeconds));
            if (turned != _turned)
            {
                _turned = turned;
                foreach ((RectTransform rect, int lord, Color from, int k) in _turning)
                    DrawLord(rect, lord, Color.Lerp(from, _turnTo, turned / (float)TurnSteps), k);
            }
            // 물러남 — 등을 돌리고(좌우만 뒤집는다) 그림 px 계단으로 걸어 무대 오른쪽 밖으로(무대가 자른다)
            foreach ((RectTransform rect, Vector2 home, float start, int k) in _retreat)
            {
                if (rect == null || !rect.gameObject.activeSelf)
                    continue;
                if (t >= RetreatTurn && rect.localScale.x < 0f)
                    rect.localScale = Vector3.one;
                float walked = Mathf.Max(0f, t - start) * RetreatSpeed;
                rect.anchoredPosition = home + UnitFigure.Step(walked, k, walked > 0f);
                if (rect.anchoredPosition.x > PanelWidth + 12f * k)
                    rect.gameObject.SetActive(false);
            }
        }

        // 화면 위 왼쪽 기준 (x, y)를 무대(_backdrop — 아래 끝 = 땅 선 _endGround, 땅 아래는 잘린다) 안 자리로
        private void Sunken(RectTransform rect, float x, float y, Vector2 pivot)
        {
            rect.SetParent(_backdrop, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.anchoredPosition = new Vector2(Mathf.Round(x - Left - 4f), Mathf.Round(_endGround - y));
        }

        // 런의 끝 장면의 움직임 — 판이 다 내려앉은 뒤부터
        private void EndScene()
        {
            float t = _clock - PopupMotion.OpenSeconds;
            Cheer();
            if (_crown != null)
            {
                int lift = Mathf.Clamp(CrownLift - Mathf.FloorToInt(Mathf.Max(0f, t) / CrownSeconds), 0, CrownLift);
                _crown.anchoredPosition = _crownHome - new Vector2(0f, (CrownLift - lift) * EndK);
            }
            if (_cleared)
                return;
            // 성이 꺼진다 — 계단마다 양 끝 · 가운데 아래에서 먼지
            int sunk = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, t) / SinkSeconds) * SinkStep, 0, SinkMax);
            if (_castle != null && sunk != _sunk)
            {
                _sunk = sunk;
                _castle.anchoredPosition = _castleHome - new Vector2(0f, sunk * CastleScale);
                float half = _castle.sizeDelta.x * 0.5f - 10f * CastleScale;
                foreach (float x in new[] { -half, half, (float)_dustRandom.Next(-60, 60) })
                    Dust(_castleHome.x + x + _dustRandom.Next(-3, 4) * CastleScale, _dustRandom.Next(2) == 0 ? 2 * EndK : EndK);
            }
            for (int i = _dust.Count - 1; i >= 0; i--)
            {
                (RectTransform rect, Vector2 home, float born) = _dust[i];
                int step = Mathf.FloorToInt((_clock - born) / DustSeconds);
                if (step >= DustSteps)
                {
                    Destroy(rect.gameObject);
                    _dust.RemoveAt(i);
                    continue;
                }
                rect.anchoredPosition = home + new Vector2(0f, step * EndK);
                if (step == DustSteps - 1 && rect.sizeDelta.x > EndK)
                    rect.sizeDelta = Vector2.one * EndK;
            }
            // 내 영주 — 깃발 색이 계단으로 회색이 되고, 다음 계단에 무릎 꿇는다
            int drained = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, t) / DrainSeconds), 0, DrainSteps + 1);
            if (_endFigure != null && drained != _drained)
            {
                _drained = drained;
                DrawLord(_endFigure, _endLord, Color.Lerp(_endColor, Drained, Mathf.Min(drained, DrainSteps) / (float)DrainSteps), EndK);
                _endFigure.anchoredPosition = _endFigureHome - new Vector2(0f, drained > DrainSteps ? EndK : 0f);
            }
            // 떠나는 백성 — 걸음(그림 px 계단 · 홀수 걸음마다 들썩) · 무대 오른쪽 끝을 넘으면 끈다(넘는 동안은 무대가 자른다)
            foreach ((RectTransform rect, Vector2 home, float start) in _leavers)
            {
                if (rect == null || !rect.gameObject.activeSelf)
                    continue;
                float walked = Mathf.Max(0f, _clock - start) * LeaveSpeed;
                rect.anchoredPosition = home + UnitFigure.Step(walked, EndK, walked > 0f);
                if (rect.anchoredPosition.x > PanelWidth)
                    rect.gameObject.SetActive(false);
            }
        }

        // 뛰는 백성 · 고개 숙인 영주(런의 끝 · 이긴 전투의 결과)
        private void Cheer()
        {
            foreach ((RectTransform rect, Vector2 home, int offset, bool jumps) in _cheer)
            {
                int[] steps = jumps ? Jump : Bow;
                int at = (Mathf.FloorToInt(Time.unscaledTime / (jumps ? JumpSeconds : BowSeconds)) + offset) % steps.Length;
                // 뛰기는 위로 · 고개 숙임은 아래로(무릎 꿇은 영주가 한 줄 더 숙인다)
                rect.anchoredPosition = home + new Vector2(0f, (jumps ? steps[at] : -steps[at]) * EndK);
            }
        }

        // 먼지 조각 하나 — 무대 땅 선 위 x(무대 안 px)에, size px 정사각
        private void Dust(float x, int size)
        {
            Image dust = NewImage(_backdrop, "Dust", null, Color.white);
            dust.rectTransform.anchorMin = dust.rectTransform.anchorMax = Vector2.zero;
            dust.rectTransform.pivot = new Vector2(0.5f, 0f);
            dust.rectTransform.sizeDelta = Vector2.one * size;
            dust.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x / EndK) * EndK, 0f);
            _dust.Add((dust.rectTransform, dust.rectTransform.anchoredPosition, _clock));
        }

        private void Show(bool on)
        {
            _sheet.gameObject.SetActive(on);
        }

        // ── 연합 결성(사용자 10/27 · 10/28) — 같은 판 · 같은 무대 문법, 판 높이는 내용에 맞춘다. 제목 아래 회색 한 줄(왜 뭉쳤나).
        // 연합마다 한 칸(4개를 넘으면 두 줄): 위에 「[이름] 연합 · [왕관] 땅 N · [칼] K」(K = 이겨야 하는 횟수, 좁으면 두 줄) · 가운데에 바탕 영주를 크게 ·
        // 흡수된 영주는 작게 양옆에 칸이 허락하는 만큼만(겹치지 않게) 걸어 들어와 닿으면 깃발이 바탕 영주의 색이 된다 · 땅 선 아래 「+N」(합류한 영주 수).
        // 아무 데나 누르면 닫힌다 → CoalitionsClosed
        public event System.Action CoalitionsClosed;

        private const int CoalitionColumns = 4;
        // 흡수된 영주가 걸어 들어오는 시간 · 다음 영주가 출발하는 간격(초)
        private const float JoinSeconds = 0.8f, JoinStagger = 0.12f;
        // 칸 줄 하나 — 글 두 줄(아래 줄 가운데가 위에서 48) · 그림까지 · 땅 선 아래 「+N」까지(px)
        private const float RowText = 48f, RowFigures = 76f, RowBelow = 52f;
        // 제목 · 설명 줄 아래에서 칸 줄이 시작하는 곳 · 판 아래 여백(px)
        private const float CoalitionAreaTop = 128f, CoalitionBottom = 0f;

        public void OpenCoalitions(List<WorldState.Coalition> coalitions)
        {
            _motion.Complete();
            _offer = null;
            _step = Step.Coalition;
            _clock = 0f;
            _hover = null;
            Clear();
            _dim.gameObject.SetActive(true);
            Show(true);
            _motion.Open();
            _ground.gameObject.SetActive(false);
            _action.gameObject.SetActive(false);
            _title.text = "연합 결성";
            string me = string.IsNullOrEmpty(RunState.PlayerName) ? "내" : $"\"{RunState.PlayerName}\"의";
            _lineIcons.SetText($"<color={BuildingTooltip.NoteColor}>{me} 세력이 너무 강해져, 나머지 진영들이 연합했습니다.</color>");
            _line.rectTransform.sizeDelta = new Vector2(PanelWidth - 80f, _line.preferredHeight);
            WorldState state = RunState.World.State;

            int n = coalitions.Count, columns = Mathf.Min(n, CoalitionColumns), rows = (n + CoalitionColumns - 1) / CoalitionColumns;
            // 바탕 영주 · 흡수된 영주의 그림 1px = 화면 몇 px
            int big = rows > 1 ? 5 : columns > 2 ? 6 : 8, small = columns > 2 || rows > 1 ? 3 : 4;
            // 그림의 범위(그림 px, 몸 한가운데 기준) — 깃발이 몸 왼쪽으로 삐져나온다(오른쪽을 볼 때)
            // 몸 크기는 국가마다 다를 수 있다 — 나오는 영주들 중 가장 큰 것으로 칸을 잡는다
            float bodyW = 0f, bodyH = 0f;
            foreach (WorldState.Coalition c in coalitions)
                foreach (int id in System.Linq.Enumerable.Prepend(c.Members, c.Lord.Id))
                {
                    Sprite body = WorldLords.BodyOf(id);
                    bodyW = Mathf.Max(bodyW, body.rect.width);
                    bodyH = Mathf.Max(bodyH, body.rect.height);
                }
            float outer = bodyW * 0.5f + 8f, inner = Mathf.Max(bodyW * 0.5f, _lordFlag.rect.width - 8f - bodyW * 0.5f);
            float figureH = Mathf.Max(bodyH, 3f + _lordFlag.rect.height);
            float cellWidth = (PanelWidth - 32f) / columns, rowHeight = RowFigures + figureH * big + RowBelow;
            float height = CoalitionAreaTop + rows * rowHeight + CoalitionBottom;
            float top = Mathf.Round(MapTop + (MapHeight - height) * 0.5f);
            float lastGround = top + CoalitionAreaTop + (rows - 1) * rowHeight + RowFigures + figureH * big;
            PlaceSheet(top, height, lastGround);
            _lineIcons.Place();
            int first = System.Array.IndexOf(state.Owner, coalitions[0].Lord.Id);
            if (first >= 0)
                Backdrop(RunState.World.Map.TerrainOf(first), first);

            for (int i = 0; i < n; i++)
            {
                WorldState.Coalition coalition = coalitions[i];
                int row = i / CoalitionColumns, column = i % CoalitionColumns;
                int inRow = Mathf.Min(CoalitionColumns, n - row * CoalitionColumns);
                // 줄의 칸들을 가운데로 모은다(두 번째 줄이 덜 찼을 때)
                float cellLeft = Left + 16f + (PanelWidth - 32f - inRow * cellWidth) * 0.5f + column * cellWidth;
                float x = Mathf.Round(cellLeft + cellWidth * 0.5f), rowTop = top + CoalitionAreaTop + row * rowHeight;
                float ground = Mathf.Round(rowTop + RowFigures + figureH * big);
                Image line = NewImage(_things, "Ground", null, Color.white);
                line.rectTransform.sizeDelta = new Vector2(cellWidth - 24f, 4f);
                At(line.rectTransform, x, ground, new Vector2(0.5f, 0f));
                Color color = RunState.ColorOf(coalition.Lord);
                LordFigure(coalition.Lord.Id, color, x, ground - BodyHeight(coalition.Lord.Id) * big * 0.5f, big, false);

                // 흡수된 영주들 — 바탕 영주에서 가까운 자리부터 왼쪽 · 오른쪽 번갈아, 칸 안에 겹치지 않고 서는 만큼만
                float gap = 2f * small, step = (outer + inner) * small + gap;
                var slots = new List<(float To, float From, bool Left)>();
                float leftAt = x - outer * big - gap - inner * small, rightAt = x + inner * big + gap + inner * small;
                float leftEnd = cellLeft + 6f + outer * small, rightEnd = cellLeft + cellWidth - 6f - outer * small;
                while (slots.Count < coalition.Members.Length && (leftAt >= leftEnd || rightAt <= rightEnd))
                {
                    if (rightAt <= rightEnd)
                    {
                        slots.Add((rightAt, rightEnd, true));
                        rightAt += step;
                    }
                    if (leftAt >= leftEnd && slots.Count < coalition.Members.Length)
                    {
                        slots.Add((leftAt, leftEnd, false));
                        leftAt -= step;
                    }
                }
                for (int j = 0; j < slots.Count; j++)
                {
                    int member = coalition.Members[j];
                    (float to, float from, bool faceLeft) = slots[j];
                    float y = ground - BodyHeight(member) * small * 0.5f;
                    // 오른쪽에서 오는 영주는 왼쪽을 본다(깃발이 바깥쪽)
                    RectTransform holder = LordFigure(member, RunState.ColorOf(state.Lords[member]), Mathf.Round(from), y, small, faceLeft);
                    _flights.Add(new Flight
                    {
                        Rect = holder, From = Anchored(new Vector2(Mathf.Round(from), y)), To = Anchored(new Vector2(Mathf.Round(to), y)),
                        Start = PopupMotion.OpenSeconds + j * JoinStagger, Seconds = JoinSeconds, Walk = true, Scale = small,
                        Landed = () => DrawLord(holder, member, color, small),
                    });
                }
                // 합류한 영주 수 — 땅 선 아래
                if (coalition.Members.Length > 0)
                {
                    Text joined = NewText(_things, "Joined", _dotFont, 24, cellWidth);
                    joined.text = $"<color={BuildingTooltip.NumberColor}>+{coalition.Members.Length}</color>";
                    At(joined.rectTransform, x, ground + 4f + RowBelow * 0.5f, new Vector2(0.5f, 0.5f));
                }
                CoalitionLine(coalition, x, rowTop + RowText, cellWidth - 8f);
            }
            _skip.gameObject.SetActive(true);
        }

        // 판 자리 · 높이와 그 안의 무대(뒷배경을 자르는 칸 — 아래 끝 = ground) · 제목 · 설명 줄 — 이벤트 장면은 늘 같은 자리(Top · PanelHeight · Ground)
        private void PlaceSheet(float top, float height, float ground)
        {
            _box.sizeDelta = new Vector2(PanelWidth, height);
            At(_box, Left, top, new Vector2(0f, 1f));
            _stage.sizeDelta = new Vector2(PanelWidth - 8f, ground - top - 4f);
            At(_title.rectTransform, Center, top + 24f, new Vector2(0.5f, 1f));
            At(_line.rectTransform, Center, top + 78f, new Vector2(0.5f, 1f));
            // 땅 선 · 명령 버튼(땅 선 아래 오른쪽) — 판 높이가 바뀌는 장면(런의 끝)이 있어 땅 선을 따라간다
            At(_ground.rectTransform, Left + 16f, ground, new Vector2(0f, 1f));
            At((RectTransform)_action.transform, Left + PanelWidth - 24f, ground + 16f, new Vector2(1f, 1f));
        }

        private void CloseCoalitions()
        {
            _step = Step.Closed;
            _skip.gameObject.SetActive(false);
            _motion.Close(() =>
            {
                Clear();
                Show(false);
                _dim.gameObject.SetActive(false);
                _ground.gameObject.SetActive(true);
            });
            CoalitionsClosed?.Invoke();
        }

        // 지도와 같은 영주 그림(깃발 · 영주 색 · 사진) — 몸 한가운데를 (x, y)에. left = 왼쪽을 본다
        private RectTransform LordFigure(int lord, Color color, float x, float y, int k, bool left)
        {
            RectTransform holder = NewRect(_things, "Lord" + lord);
            At(holder, x, y, new Vector2(0.5f, 0.5f));
            if (left)
                holder.localScale = new Vector3(-1f, 1f, 1f);
            DrawLord(holder, lord, color, k);
            return holder;
        }

        private void DrawLord(RectTransform holder, int lord, Color color, int k)
        {
            if (holder == null)
                return;
            foreach (Transform part in holder)
                Destroy(part.gameObject);
            WorldLords.Figure(holder, WorldLords.BodyOf(lord), _lordFlag, WorldLords.PhotoOf(lord), color, k);
        }

        // 그 영주 몸 그림의 높이(원본 px) — 국가마다 다를 수 있다
        private static float BodyHeight(int lord) => WorldLords.BodyOf(lord).rect.height;
        private static float BodyWidth(int lord) => WorldLords.BodyOf(lord).rect.width;

        // 「[이름] 연합 · [왕관] 땅 N · [칼] K」 — 맨 아래 줄의 가운데를 (x, y)에(도트 24). 칸 폭에 안 들어가면 「[이름] 연합」 / 「[왕관] 땅 N · [칼] K」 두 줄,
        // 그래도 넘치면 도트 12로
        private void CoalitionLine(WorldState.Coalition coalition, float x, float y, float width)
        {
            string name = $"{RunState.NameOf(coalition.Lord)} 연합";
            string lands = BuildingTooltip.ColorNumbers(RunState.World.State.LandsOf(coalition.Lord).ToString());
            string wins = BuildingTooltip.ColorNumbers(coalition.WinsNeeded.ToString());
            object[] stats = { _map.Crown, $" 땅 {lands} · ", RunState.Config.World.InvadeMark, $" {wins}" };
            var one = new List<object> { name + " · " };
            one.AddRange(stats);
            if (IconRow(one.ToArray(), x, y, 24, width))
                return;
            if (IconRow(stats, x, y, 24, width) && IconRow(new object[] { name }, x, y - 36f, 24, width))
                return;
            IconRow(stats, x, y, 12, float.MaxValue);
            IconRow(new object[] { name }, x, y - 20f, 12, float.MaxValue);
        }

        // 글(도트 size) · 아이콘(원본 크기)을 한 줄로 이어 가운데를 (x, y)에 — 폭이 width를 넘으면 지우고 false
        private bool IconRow(object[] pieces, float x, float y, int size, float width)
        {
            RectTransform row = NewRect(_things, "CoalitionLine");
            At(row, x, y, new Vector2(0.5f, 0.5f));
            var parts = new List<RectTransform>();
            float total = 0f;
            foreach (object piece in pieces)
            {
                RectTransform part;
                if (piece is string text)
                {
                    Text label = NewText(row, "Text", _dotFont, size, 0f);
                    label.text = text;
                    label.rectTransform.sizeDelta = new Vector2(label.preferredWidth, size + 8f);
                    part = label.rectTransform;
                }
                else
                {
                    var sprite = piece as Sprite;
                    Image icon = NewImage(row, "Icon", sprite, Color.white);
                    if (sprite != null)
                        icon.SetNativeSize();
                    part = icon.rectTransform;
                }
                parts.Add(part);
                total += part.sizeDelta.x;
            }
            if (total > width)
            {
                Destroy(row.gameObject);
                return false;
            }
            float at = -total * 0.5f;
            foreach (RectTransform part in parts)
            {
                part.anchorMin = part.anchorMax = new Vector2(0.5f, 0.5f);
                part.pivot = new Vector2(0f, 0.5f);
                part.anchoredPosition = new Vector2(at, 0f);
                at += part.sizeDelta.x;
            }
            return true;
        }

        // ── 나라 소식 · 외교(아웃게임기획 §3-2) — 같은 판 · 같은 무대. 제목 · 회색 사연 한 줄 · 무대 위에 고르기 칸 둘(누르면 고른다 · 고를 수 없으면 흐리게).
        // 기물을 고르는 고르기는 누르면 왼쪽 탑의 층을 누르는 단계로(「뒤로」로 돌아간다). 소식을 고르면 같은 턴의 외교 제안이 이어서 뜬다 → 다 닫히면 NewsClosed
        public event System.Action NewsClosed;

        private const float BoxWidth = 380f, BoxGap = 40f, BoxTop = 140f, BoxPad = 20f;

        private class NewsBox
        {
            public RectTransform Rect;
            public int Choice;
            public bool Enabled;
            // 무대 위 짝 물건(소식 장면 — 없으면 null) · 제자리. 칸이나 물건을 올리면 1px 들린다
            public RectTransform Thing;
            public Vector2 ThingHome;
        }

        private readonly List<NewsBox> _boxes = new List<NewsBox>();
        private NewsBox _boxHover;
        // 외교 판인가 · 기물을 고르는 중인 고르기(-1 = 칸을 고르는 중) · 결과 줄을 보여 주는 중(연금술) · 올린 탑 층
        private bool _diplomacyShown;
        private int _pickChoice = -1;
        private bool _newsResult;
        private int _floorHover = -1;
        private readonly List<(int Floor, EventVisitTarget Target)> _floorTargets = new List<(int, EventVisitTarget)>();

        // 이번 턴의 소식(없으면 외교)을 연다 — 둘 다 없으면 false
        public bool OpenNews()
        {
            if (RunState.CurrentNews == null && RunState.CurrentDiplomacy == null)
                return false;
            _motion.Complete();
            _offer = null;
            _previousDock = BuildingTooltip.Dock;
            BuildingTooltip.Dock = _dock;
            _dim.gameObject.SetActive(true);
            Show(true);
            if (RunState.CurrentNews != null)
                BuildNews();
            else
                BuildDiplomacy();
            return true;
        }

        private void BeginNewsSheet()
        {
            _step = Step.News;
            _clock = 0f;
            Clear();
            _motion.Open();
            _boxes.Clear();
            _boxHover = null;
            _pickChoice = -1;
            _newsResult = false;
            _newsPlay = false;
            _newsScene = NewsScene.Plain;
            _caretImage.gameObject.SetActive(false);
            PlaceSheet(Top, PanelHeight, Ground);
            _endGround = Ground;
            _ground.gameObject.SetActive(true);
            _action.gameObject.SetActive(false);
        }

        private void SetLine(string text)
        {
            _lineIcons.SetText(text);
            _line.rectTransform.sizeDelta = new Vector2(PanelWidth - 80f, _line.preferredHeight);
            _lineIcons.Place();
        }

        private void SetAction(string label)
        {
            _action.gameObject.SetActive(label != null);
            if (label != null)
                _actionLabel.text = label;
        }

        private void BuildNews()
        {
            BeginNewsSheet();
            _diplomacyShown = false;
            NewsOffer news = RunState.CurrentNews;
            WorldState state = RunState.World.State;
            int home = System.Array.IndexOf(state.Owner, state.Player.Id);
            if (home >= 0)
                Backdrop(RunState.World.Map.TerrainOf(home), RunState.Round);
            _title.text = news.Definition.DisplayName;
            _newsStory = $"<color={BuildingTooltip.NoteColor}>{news.Definition.Story}</color>";
            SetLine(_newsStory);
            _newsScene = news.Definition.Scene;
            // 소식 장면이 있으면 내 영주는 왼쪽(오른쪽을 본다) — 없으면 · 건국 기념제는 가운데
            float lordX = _newsScene == NewsScene.Plain || _newsScene == NewsScene.Festival ? Center : Center + NewsLordX;
            LordFigure(state.Player.Id, RunState.ColorOf(state.Player), lordX, Ground - BodyHeight(state.Player.Id) * K * 0.5f, K, false);
            for (int i = 0; i < 2; i++)
                AddBox(i, null, BuildingTooltip.ColorNumbers(news.Definition.Choice(i).Describe()), RunState.CanChooseNews(i));
            LayoutBoxes();
            switch (_newsScene)
            {
                case NewsScene.Migrants: BuildMigrants(); break;
                case NewsScene.Harvest: BuildHarvest(); break;
                case NewsScene.Fire: BuildFire(); break;
                case NewsScene.GoldMine: BuildGoldMine(); break;
                case NewsScene.Locust: BuildLocust(); break;
                case NewsScene.Caravan: BuildCaravan(); break;
                case NewsScene.Master: BuildMaster(); break;
                case NewsScene.Scholar: BuildScholar(); break;
                case NewsScene.Alchemist: BuildAlchemist(); break;
                case NewsScene.Festival: BuildFestival(); break;
            }
            SetAction(RunState.CanSkipNews ? "닫기" : null);
        }

        private void BuildDiplomacy()
        {
            BeginNewsSheet();
            _diplomacyShown = true;
            DiplomacyOffer offer = RunState.CurrentDiplomacy;
            WorldConfig world = RunState.Config.World;
            WorldState state = RunState.World.State;
            int land = System.Array.IndexOf(state.Owner, offer.Lord.Id);
            if (land >= 0)
                Backdrop(RunState.World.Map.TerrainOf(land), land);
            string nation = offer.Nation.DisplayName();
            _title.text = world.DiplomacyTitle;
            SetLine($"<color={BuildingTooltip.NoteColor}>{string.Format(world.DiplomacyLine, nation, RunState.NameOf(offer.Lord))}</color>");
            // 두 영주가 마주 본다 — 한 칸(원본 8px)씩 다가서면 몸이 닿는 자리(수락 연출 DiplomacyAccept, 사용자 11/1)
            _offerLord = LordFigure(offer.Lord.Id, RunState.ColorOf(offer.Lord), Center - (BodyWidth(offer.Lord.Id) * 0.5f + DiplomacyStep) * K, Ground - BodyHeight(offer.Lord.Id) * K * 0.5f, K, false);
            _myLord = LordFigure(state.Player.Id, RunState.ColorOf(state.Player), Center + (BodyWidth(state.Player.Id) * 0.5f + DiplomacyStep) * K, Ground - BodyHeight(state.Player.Id) * K * 0.5f, K, true);
            // 침략 줄은 그 영주가 이웃일 때만 — 이웃이 아니면 이번 턴에 침략할 길이 없어 효과가 없다
            string body = (offer.Neighbor ? world.DiplomacyTruce + "\n" : "") + string.Format(world.DiplomacyTrade, nation);
            AddBox(0, world.DiplomacyAccept, body, true);
            AddBox(1, world.DiplomacyDecline, null, true);
            LayoutBoxes();
        }

        // 고르기 칸 하나 — 검은 판 · 판 선 · 도트 머리(있으면) · 깔끔 본문(줄 안 아이콘). 자리는 LayoutBoxes가
        private void AddBox(int choice, string head, string body, bool enabled)
        {
            Image back = NewImage(_things, "Choice" + choice, null, Color.black);
            back.raycastTarget = true;
            RectTransform frame = Instantiate(_framePart, back.rectTransform);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            float y = BoxPad;
            if (head != null)
            {
                Text title = NewText(back.rectTransform, "Head", _dotFont, 24, BoxWidth - 2f * BoxPad);
                title.text = head;
                Pin(title.rectTransform, y);
                y += 40f;
            }
            if (!string.IsNullOrEmpty(body))
            {
                Text text = NewText(back.rectTransform, "Body", _notoFont, 22, BoxWidth - 2f * BoxPad);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                InlineIcons icons = text.gameObject.AddComponent<InlineIcons>();
                icons.SetText(body);
                text.rectTransform.sizeDelta = new Vector2(BoxWidth - 2f * BoxPad, text.preferredHeight);
                Pin(text.rectTransform, y);
                icons.Place();
                y += text.preferredHeight;
            }
            back.rectTransform.sizeDelta = new Vector2(BoxWidth, y + BoxPad);
            back.gameObject.AddComponent<CanvasGroup>().alpha = enabled ? 1f : WaitingAlpha;
            var box = new NewsBox { Rect = back.rectTransform, Choice = choice, Enabled = enabled };
            EventVisitTarget target = back.gameObject.AddComponent<EventVisitTarget>();
            target.Entered += _ => _boxHover = box;
            target.Exited += _ => { if (_boxHover == box) _boxHover = null; };
            target.Clicked += _ => ClickBox(box);
            _boxes.Add(box);
        }

        // 칸 안 글 — 칸 위 가운데에서 top px 아래
        private static void Pin(RectTransform rect, float top)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        // 칸들을 나란히 · 높이를 맞춰서(글은 위에서부터)
        private void LayoutBoxes()
        {
            float height = 0f;
            foreach (NewsBox box in _boxes)
                height = Mathf.Max(height, box.Rect.sizeDelta.y);
            for (int i = 0; i < _boxes.Count; i++)
            {
                _boxes[i].Rect.sizeDelta = new Vector2(BoxWidth, height);
                At(_boxes[i].Rect, Mathf.Round(Center + (i - (_boxes.Count - 1) * 0.5f) * (BoxWidth + BoxGap)), Top + BoxTop, new Vector2(0.5f, 1f));
            }
        }

        private void ClickBox(NewsBox box)
        {
            if (_step != Step.News || !box.Enabled || _pickChoice >= 0 || _newsResult || _newsPlay)
                return;
            if (_diplomacyShown)
            {
                DiplomacyOffer offer = RunState.CurrentDiplomacy;
                RunState.AnswerDiplomacy(box.Choice == 0);
                PlayDiplomacy(offer, box.Choice);
                return;
            }
            if (RunState.CurrentNews.NeedsBuilding(box.Choice))
            {
                BeginPick(box.Choice);
                return;
            }
            NewsChoice chosen = RunState.CurrentNews.Definition.Choice(box.Choice);
            OwnedBuilding[] before = (OwnedBuilding[])RunState.Field.Clone();
            _goldBefore = RunState.Gold;
            _rarityBefore = System.Array.ConvertAll(RunState.Field, b => OwnedBuilding.IsEmpty(b) ? BuildingRarity.Common : b.Rarity);
            if (RunState.ChooseNews(box.Choice))
                AfterNews(chosen, -1, box.Choice, before);
        }

        // ── 외교 결과 연출(사용자 11/1) — 수락: 두 영주가 마주 보고 한 칸씩 다가가 몸이 닿게 선 뒤 둘이 한 번 같이 뛰고, 그 나라의 짐수레가 왼쪽 밖에서 굴러온다.
        // 수레가 서면 「진열에 ○○ 기물」 · (이웃이면) 「이번 턴 침략 불가」가 떠오른다. 거절: 상대 영주가 돌아서 왼쪽 밖으로 걸어 나간다. 움직임은 그림 px 계단
        private const int DiplomacyStep = 8;
        private const float DiplomacyStepSeconds = 0.04f;
        // 수레가 상대 영주 등(깃발 끝)에서 떨어져 서는 거리(원본 px, 몸의 왼쪽 끝에서) · 수레가 굴러오는 속도(px/초)
        private const float CartBehind = 10f;
        private const float CartSpeed = 360f;
        private RectTransform _offerLord, _myLord;

        private void PlayDiplomacy(DiplomacyOffer offer, int choice)
        {
            _newsPlay = true;
            _boxHover = null;
            _clock = 0f;
            foreach (NewsBox box in _boxes)
                box.Rect.gameObject.SetActive(box.Choice == choice);
            _caretImage.gameObject.SetActive(false);
            SetAction(null);
            _skip.gameObject.SetActive(true);
            _skip.transform.SetAsLastSibling();
            _newsEnd = NextNews;
            RectTransform them = _offerLord, me = _myLord;
            float end;
            if (choice == 0)
            {
                for (int i = 1; i <= DiplomacyStep; i++)
                    Cue(i * DiplomacyStepSeconds, () =>
                    {
                        them.anchoredPosition += new Vector2(K, 0f);
                        me.anchoredPosition -= new Vector2(K, 0f);
                    });
                float jump = DiplomacyStep * DiplomacyStepSeconds + 0.1f;
                Hop(them, jump, 2);
                Hop(me, jump, 2);
                // 수레 — 무대 안(판 밖은 잘린다), 왼쪽 밖에서 상대 영주 등 뒤까지
                Sprite cart = RunState.Config.World.NewsCart;
                Image image = NewImage(_things, cart.name, cart, Color.white);
                image.rectTransform.sizeDelta = cart.rect.size * K;
                float stop = Mathf.Round(Center - (BodyWidth(offer.Lord.Id) + CartBehind) * K - cart.rect.width * K * 0.5f);
                Sunken(image.rectTransform, stop, Ground, new Vector2(0.5f, 0f));
                Vector2 to = image.rectTransform.anchoredPosition;
                Vector2 from = new Vector2(-cart.rect.width * K, to.y);
                image.rectTransform.anchoredPosition = from;
                float roll = jump + 0.3f, seconds = (to.x - from.x) / CartSpeed;
                _flights.Add(new Flight { Rect = image.rectTransform, From = from, To = to, Start = roll, Seconds = seconds, Walk = true });
                string nation = offer.Nation.DisplayName();
                Cue(roll + seconds, () =>
                {
                    // 고른 칸은 떠오르는 글과 같은 말이고 긴 칸(이웃)이면 글과 겹친다 — 거둔다
                    foreach (NewsBox box in _boxes)
                        box.Rect.gameObject.SetActive(false);
                    FloatText($"진열에 {nation} 기물", stop, Ground - 150f);
                    if (offer.Neighbor)
                        FloatText("이번 턴 침략 불가", stop, Ground - 118f);
                });
                end = roll + seconds + 0.3f;
            }
            else
            {
                // 돌아서(왼쪽을 본다) 무대 왼쪽 밖으로 — 무대 안으로 옮겨야 판 밖이 잘린다
                Vector2 at = them.anchoredPosition;
                Sunken(them, at.x, -at.y, new Vector2(0.5f, 0.5f));
                Vector2 from = them.anchoredPosition;
                Vector2 to = new Vector2(-24f * K, from.y);
                float seconds = (from.x - to.x) / NewsWalkSpeed;
                Cue(0.15f, () => them.localScale = new Vector3(-1f, 1f, 1f));
                _flights.Add(new Flight { Rect = them, From = from, To = to, Start = 0.35f, Seconds = seconds, Walk = true });
                end = 0.35f + seconds;
            }
            Cue(end + NewsHold, () => _newsEnd());
        }

        // 기물 고르기 — 칸을 숨기고 왼쪽 탑의 고를 수 있는 층에 누름 받이를 붙인다
        private void BeginPick(int choice)
        {
            _pickChoice = choice;
            _boxHover = null;
            foreach (NewsBox box in _boxes)
                box.Rect.gameObject.SetActive(false);
            SetLine(RunState.CurrentNews.Definition.Choice(choice).Action == NewsAction.Alchemy
                ? "왼쪽 탑의 기물을 하나 누르면, 그 기물에 연금술을 겁니다."
                : "왼쪽 탑의 기물을 하나 누르면, 그 기물의 등급이 한 단계 오릅니다.");
            foreach (int floor in RunState.NewsTargets(choice))
            {
                SlotView slot = _panel.FloorAt(floor);
                if (slot == null)
                    continue;
                EventVisitTarget target = slot.gameObject.AddComponent<EventVisitTarget>();
                _slotTargets.Add(target);
                _floorTargets.Add((floor, target));
                int at = floor;
                target.Entered += _ => _floorHover = at;
                target.Exited += t => { if (_floorHover == at) { _floorHover = -1; BuildingTooltip.Hide(t); } };
                target.Clicked += _ => PickFloor(at);
            }
            SetAction("뒤로");
        }

        private void EndPick()
        {
            foreach ((int _, EventVisitTarget target) in _floorTargets)
                if (target != null)
                {
                    BuildingTooltip.Hide(target);
                    _slotTargets.Remove(target);
                    Destroy(target);
                }
            _floorTargets.Clear();
            _floorHover = -1;
            _pickChoice = -1;
        }

        private void PickFloor(int floor)
        {
            if (_step != Step.News || _pickChoice < 0)
                return;
            int choice = _pickChoice;
            NewsChoice chosen = RunState.CurrentNews.Definition.Choice(choice);
            OwnedBuilding[] before = (OwnedBuilding[])RunState.Field.Clone();
            _goldBefore = RunState.Gold;
            _rarityBefore = System.Array.ConvertAll(RunState.Field, b => OwnedBuilding.IsEmpty(b) ? BuildingRarity.Common : b.Rarity);
            if (!RunState.ChooseNews(choice, floor))
                return;
            EndPick();
            AfterNews(chosen, floor, choice, before);
        }

        // 고른 뒤 — 소식 장면이 있으면 결과 연출(PlayNews) 뒤에, 연금술은 결과 한 줄을 보여 주고 「닫기」, 나머지는 바로 다음(외교 · 닫힘).
        // before = 고르기 전 탑의 층들(바뀐 층을 찾는다)
        private void AfterNews(NewsChoice chosen, int floor, int choice, OwnedBuilding[] before)
        {
            NewsAction action = chosen.Action;
            if (_newsScene != NewsScene.Plain)
            {
                PlayNews(chosen, choice, before, floor);
                return;
            }
            if (action != NewsAction.Alchemy)
            {
                NextNews();
                return;
            }
            ShowAlchemy(floor);
        }

        // 연금술 결과 한 줄 · 「닫기」
        private void ShowAlchemy(int floor)
        {
            _newsPlay = false;
            _skip.gameObject.SetActive(false);
            _newsResult = true;
            foreach (NewsBox box in _boxes)
                box.Rect.gameObject.SetActive(false);
            _caretImage.gameObject.SetActive(false);
            // 결과 줄은 「닫기」까지 머문다 — 떠오르는 글은 계속 올라가 제목을 덮으니 여기서 거둔다
            foreach (var (rect, _) in _floats)
                rect.gameObject.SetActive(false);
            _anims.RemoveAll(anim => _puffs.Contains(anim.Image));
            foreach (Image puff in _puffs)
                puff.gameObject.SetActive(false);
            _puffs.Clear();
            OwnedBuilding owned = floor >= 0 ? RunState.Field[floor] : null;
            string name = owned != null ? owned.Definition.DisplayName : "기물";
            SetLine(RunState.LastAlchemySucceeded ? $"연금술에 성공해, {name} 기물이 신화가 되었습니다." : $"<color={BuildingTooltip.NoteColor}>연금술에 실패했습니다.</color>");
            SetAction("닫기");
        }

        private void NextNews()
        {
            EndPick();
            _newsPlay = false;
            _emptyFloor = -1;
            _skip.gameObject.SetActive(false);
            if (RunState.CurrentNews == null && RunState.CurrentDiplomacy != null)
            {
                BuildDiplomacy();
                return;
            }
            if (RunState.CurrentNews != null)
                return;
            _step = Step.Closed;
            _caretImage.gameObject.SetActive(false);
            _motion.Close(() =>
            {
                Clear();
                _boxes.Clear();
                Show(false);
                _dim.gameObject.SetActive(false);
            });
            if (BuildingTooltip.Dock == _dock)
                BuildingTooltip.Dock = _previousDock;
            NewsClosed?.Invoke();
        }

        // 판 오른쪽 아래 버튼 — 「닫기」(고를 수 없음 · 연금술 결과) · 「뒤로」(기물 고르기)
        private void NewsAct()
        {
            if (_newsResult)
            {
                NextNews();
                return;
            }
            if (_pickChoice >= 0)
            {
                EndPick();
                BuildNews();
                return;
            }
            if (RunState.CanSkipNews)
            {
                RunState.SkipNews();
                NextNews();
            }
        }

        // 고르기 칸 · 탑 층의 올림 — 꺾쇠 · 층 창틀 · 기물 설명(탑 기둥은 매 프레임 패널이 다시 그린다)
        private void NewsLate()
        {
            // 소식 장면 — 올린 칸(또는 그 짝 물건)의 물건이 1px 들린다(결과 연출 중에는 제자리)
            foreach (NewsBox box in _boxes)
                if (box.Thing != null && !_newsPlay && !_actors.Exists(actor => actor.Rect == box.Thing))
                    box.Thing.anchoredPosition = box.ThingHome + new Vector2(0f, box == _boxHover && box.Enabled && _pickChoice < 0 ? K : 0f);
            Sprite sprite = null;
            Vector2 at = Vector2.zero;
            Vector2 pivot = new Vector2(0.5f, 0f);
            if (_pickChoice >= 0)
            {
                foreach ((int floor, EventVisitTarget target) in _floorTargets)
                {
                    SlotView slot = _panel.FloorAt(floor);
                    if (slot == null || RunState.Field[floor] == null)
                        continue;
                    var frame = slot.transform.Find("RarityFrame").GetComponent<Image>();
                    frame.color = floor == _floorHover ? Color.white : SlotView.Highlight(RunState.Field[floor].Rarity.FrameColor(), true);
                    if (floor != _floorHover)
                        continue;
                    OwnedBuilding owned = RunState.Field[floor];
                    BuildingRarity to = RunState.CurrentNews.Definition.Choice(_pickChoice).Action == NewsAction.Alchemy ? BuildingRarity.Mythic : owned.Rarity + 1;
                    BuildingTooltip.Show(target, BuildingTooltip.Title(owned.Definition, false, to), BuildingTooltip.RankUpPreview(owned.Definition, owned.Level, to), new UnitDefinition[0], RunState.Field);
                    Vector2 middle = Middle(slot.transform);
                    sprite = _caretLeft;
                    at = new Vector2(middle.x + ((RectTransform)slot.transform).rect.width * 0.5f + 8f, middle.y);
                    pivot = new Vector2(0f, 0.5f);
                }
            }
            else if (_boxHover != null && _boxHover.Enabled && !_newsResult && !_newsPlay)
            {
                sprite = _caret;
                at = new Vector2(Middle(_boxHover.Rect).x, Top + BoxTop - 8f);
            }
            _caretImage.gameObject.SetActive(sprite != null);
            if (sprite == null)
                return;
            if (_caretImage.sprite != sprite)
                _caretImage.sprite = sprite;
            _caretImage.rectTransform.sizeDelta = sprite.rect.size * K;
            At(_caretImage.rectTransform, at.x, at.y, pivot);
        }

        // ── 소식 장면(Docs/팝업_이벤트화면_계획.md K2-3, 사용자 11/1 — 열 가지 모두). 내 영주는 왼쪽에서 오른쪽을 본다(건국 기념제는 가운데).
        // 고르기마다 무대 위 짝 물건이 있다 — 칸이나 물건을 올리면 물건이 1px 들리고 칸 위에 꺾쇠, 둘 중 아무거나 누르면 고른다(LinkThing).
        // 고르면 고르지 않은 칸을 숨기고 결과 연출(PlayNews — 정해 둔 때에 할 일들, _cues)을 보여 준 뒤 다음(외교 · 닫힘 · 연금술은 결과 한 줄). 「건너뛰기」로 바로 끝(_newsEnd).
        // 기물을 고르는 고르기(명장 ① · 연금술 ① · 기념제 ②)는 탑 층을 고른 뒤에 연출한다 — 고른 층이 장면 속 물건으로 날아갔다 돌아온다(FloorTrip).
        // 손님(상인 · 장인 · 학자 · 연금술사 · 이주민)은 오른쪽 밖에서 걸어 들어온다(Guest).
        // 상점 쪽 효과(리롤 · 진열 칸 · 수입)는 떠오르는 「진열 칸 +1」 글로만 보여 준다(NewsFloats)
        // 내 영주 자리(판 가운데에서) · 걸음 속도(px/초) · 불꽃 그림 한 장 시간 · 연출이 끝난 뒤 머무는 시간
        private const float NewsLordX = -330f;
        private const float NewsWalkSpeed = 240f, FlameSeconds = 0.15f, NewsHold = 0.8f;

        // 무대 위 사람 — From에서 To로 걸은 뒤(Start부터, 그림 px 계단) 제자리에서 들썩인다(Bob)
        private class Actor
        {
            public RectTransform Rect;
            public Vector2 From, To;
            public float Start, Speed;
            public int Offset;
            // 들썩임 대신 뛴다(건국 기념제의 백성)
            public bool Jumps;
        }

        private NewsScene _newsScene;
        // 결과 연출 중 · 연출의 끝(건너뛰기도 이것) · 고르기 전 골드(받은 골드를 보여 준다 — 벌은 0까지만 깎인다) · 고르기 전 층마다 등급(제자리에서 오른 층을 찾는다)
        private bool _newsPlay;
        private System.Action _newsEnd;
        // 소식 이야기 줄 — 층 고르기 안내가 덮어쓴 줄을 결과 연출에서 되돌린다
        private string _newsStory;
        private int _goldBefore;
        private BuildingRarity[] _rarityBefore = new BuildingRarity[0];
        private readonly List<Actor> _actors = new List<Actor>();
        private readonly List<Image> _flames = new List<Image>();
        // 장마다 차례로 바뀌는 그림(연기 · 폭죽 · 반짝임 — 빈 장(null)이면 감춘다)
        private readonly List<(Image Image, Sprite[] Frames, int Offset)> _anims = new List<(Image, Sprite[], int)>();
        // 연금술 — 층이 머문 자리의 연기(결과가 뜰 때 걷는다)
        private readonly List<Image> _puffs = new List<Image>();
        // 메뚜기 떼 — 제자리에서 1px씩 떨고, _scatterAt부터 오른쪽 위로 흩어진다
        private readonly List<(RectTransform Rect, Vector2 Home)> _swarm = new List<(RectTransform, Vector2)>();
        private float _scatterAt = -1f;
        private readonly List<(float At, System.Action Do)> _cues = new List<(float, System.Action)>();
        // 무대 위 사람들 · 그들이 머리에 인 것(볏단 · 물통)
        private readonly List<RectTransform> _newsGroup = new List<RectTransform>();
        private readonly List<RectTransform> _newsLoads = new List<RectTransform>();

        // 고르기 choice의 짝 물건 — 올리면 그 칸을 올린 것과 같고, 누르면 그 칸을 누른 것과 같다
        private void LinkThing(int choice, RectTransform thing)
        {
            NewsBox box = _boxes[choice];
            box.Thing = thing;
            box.ThingHome = thing.anchoredPosition;
            EventVisitTarget target = Hit(thing);
            target.Entered += _ => { if (!_newsPlay) _boxHover = box; };
            target.Exited += _ => { if (_boxHover == box) _boxHover = null; };
            target.Clicked += _ => ClickBox(box);
            if (!box.Enabled)
                thing.gameObject.AddComponent<CanvasGroup>().alpha = WaitingAlpha;
        }

        private void Cue(float at, System.Action act) => _cues.Add((at, act));

        // 무대 위 사람 하나 — 지금 자리에 서 있다(걷게 하려면 From · Start · Speed를 고친다)
        private Actor AddActor(RectTransform rect, int offset)
        {
            var actor = new Actor { Rect = rect, From = rect.anchoredPosition, To = rect.anchoredPosition, Offset = offset };
            _actors.Add(actor);
            return actor;
        }

        // 머리에 이는 것 — 그 사람 그림 위 가운데
        private RectTransform Load(RectTransform person, Sprite sprite)
        {
            Image load = NewImage(person, sprite.name, sprite, Color.white);
            load.rectTransform.sizeDelta = sprite.rect.size * K;
            load.rectTransform.anchorMin = load.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            load.rectTransform.pivot = new Vector2(0.5f, 0f);
            load.rectTransform.anchoredPosition = Vector2.zero;
            _newsLoads.Add(load.rectTransform);
            return load.rectTransform;
        }

        // 내 국가 유닛 n명 — 오른쪽을 보고(left면 왼쪽) xs 자리에(발 가운데), 머리에 load(없으면 맨머리). parent가 있으면 그 안(왼쪽 아래 기준 x)으로 옮긴다
        private List<Actor> Workers(float[] xs, Sprite load, RectTransform parent, bool left = false)
        {
            var made = new List<Actor>();
            List<UnitDefinition> citizens = Citizens();
            Color paint = RunState.ColorOf(RunState.World.State.Player);
            int person = 0;
            for (int i = 0; i < xs.Length && citizens.Count > 0; i++)
            {
                RectTransform unit = NextCitizen(citizens, ref person, K, left, paint, 10f * K);
                if (unit == null)
                    break;
                if (parent != null)
                {
                    unit.SetParent(parent, false);
                    unit.anchorMin = unit.anchorMax = unit.pivot = Vector2.zero;
                    unit.anchoredPosition = new Vector2(xs[i] - unit.sizeDelta.x * 0.5f, 0f);
                }
                else
                    PlaceFigure(unit, xs[i], Ground, left);
                if (load != null)
                    Load(unit, load);
                _newsGroup.Add(unit);
                made.Add(AddActor(unit, i * 2));
            }
            return made;
        }

        // 손님 — 오른쪽 밖에서 걸어 들어와 x(발 가운데)에 서서 왼쪽(내 영주)을 본다. 무대 안이라 판 밖은 잘린다
        private Actor Guest(RectTransform rect, float x)
        {
            rect.localScale = new Vector3(-1f, 1f, 1f);
            Sunken(rect, x, Ground, new Vector2(0.5f, 0f));
            Actor actor = AddActor(rect, 1);
            actor.From = new Vector2(PanelWidth + 60f, actor.To.y);
            actor.Start = PopupMotion.OpenSeconds;
            actor.Speed = NewsWalkSpeed;
            return actor;
        }

        // 손님 그림(공용 인물 — 그림 그대로)
        private Actor Guest(Sprite sprite, float x)
        {
            Image image = NewImage(_things, sprite.name, sprite, Color.white);
            image.rectTransform.sizeDelta = sprite.rect.size * K;
            return Guest(image.rectTransform, x);
        }

        // 걸어간다 — 지금 자리에서 무대 안 x(화면 기준)로, at초부터. 오른쪽으로 가면 오른쪽을 본다
        private void Walk(Actor actor, float x, float at)
        {
            actor.From = actor.To;
            actor.To = new Vector2(Mathf.Round(x - Left - 4f), actor.From.y);
            actor.Start = at;
            actor.Speed = NewsWalkSpeed;
            Cue(at, () => actor.Rect.localScale = new Vector3(actor.To.x > actor.From.x ? 1f : -1f, 1f, 1f));
        }

        // 돌아서 오른쪽 밖으로 걸어 나간다
        private void Leave(Actor actor, float at) => Walk(actor, Left + PanelWidth + 120f, at);

        // 장면 속 그림 하나(아래 가운데를 (x, bottom)에) — 장마다 바뀐다(_anims)
        private Image Anim(Sprite[] frames, float x, float bottom, int offset)
        {
            Image image = NewImage(_things, "Anim", frames[0], Color.white);
            image.rectTransform.sizeDelta = frames[0].rect.size * K;
            At(image.rectTransform, x, bottom, new Vector2(0.5f, 0f));
            _anims.Add((image, frames, offset));
            return image;
        }

        // 그 자리 반짝임(이펙트 — 색을 쓴다) — 켜졌다 꺼졌다 한다
        private void Twinkle(float x, float y, Color color, int offset)
        {
            Image image = Anim(new[] { _sparks, null, null }, x, y, offset);
            image.color = color;
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        // 기물을 고르기 전과 지금이 다른 층(새 기물 · 오른 등급) — 없으면 -1
        private int ChangedFloor(OwnedBuilding[] before)
        {
            OwnedBuilding[] field = RunState.Field;
            for (int i = 0; i < field.Length; i++)
            {
                if (OwnedBuilding.IsEmpty(field[i]))
                    continue;
                bool fresh = i >= before.Length || !ReferenceEquals(field[i], before[i]);
                if (fresh || (i < _rarityBefore.Length && field[i].Rarity != _rarityBefore[i]))
                    return i;
            }
            return -1;
        }

        // 탑 층 하나가 spot으로 날아갔다(창틀 from 색) 닿으면 to 색이 되고(landed) 잠시 뒤 제 층으로 돌아온다 — 그동안 탑 자리는 빈 층. 끝나는 때를 돌려준다
        private float FloorTrip(int at, BuildingDefinition building, Color from, Color to, Vector2 spot, float start, System.Action landed)
        {
            SlotView slot = _panel.FloorAt(at);
            Transform floor = slot != null ? slot.transform : _panel.Roof;
            FloorLook chunk = null;
            Image window = null;
            Cue(start, () =>
            {
                _emptyFloor = at;
                chunk = Floor(building, 0f, 0f, out window);
                window.color = from;
                FlyNow((RectTransform)chunk.transform, Middle(floor), spot).Landed = () =>
                {
                    window.color = to;
                    landed?.Invoke();
                };
            });
            Cue(start + FlySeconds + 0.8f, () => FlyNow((RectTransform)chunk.transform, spot, Middle(floor)).Landed = () =>
            {
                chunk.gameObject.SetActive(false);
                _emptyFloor = -1;
            });
            return start + 2f * FlySeconds + 0.8f;
        }

        // 한 층이 어디서 제 층으로 날아와 앉는다(새 기물) — 그동안 탑 자리는 빈 층
        private void FloorArrive(int at, Vector2 from, float start)
        {
            SlotView slot = _panel.FloorAt(at);
            Transform floor = slot != null ? slot.transform : _panel.Roof;
            OwnedBuilding owned = RunState.Field[at];
            _emptyFloor = at;
            Cue(start, () =>
            {
                FloorLook chunk = Floor(owned.Definition, 0f, 0f, out Image window);
                window.color = owned.Rarity.FrameColor();
                FlyNow((RectTransform)chunk.transform, from, Middle(floor)).Landed = () =>
                {
                    chunk.gameObject.SetActive(false);
                    _emptyFloor = -1;
                };
            });
        }

        // 물건이 그림 px 계단으로 한 번 뛴다(lift칸 올랐다 내려온다)
        private void Hop(RectTransform rect, float at, int lift)
        {
            for (int i = 1; i <= 2 * lift; i++)
            {
                int up = i <= lift ? 1 : -1;
                Cue(at + i * 0.05f, () => rect.anchoredPosition += new Vector2(0f, up * K));
            }
        }

        // 동전 하나가 from에서 to로(화면 위 왼쪽 기준) — 닿으면 사라진다
        private void Coin(Vector2 from, Vector2 to, float delay)
        {
            Image coin = NewImage(_things, "Coin", _coin, Color.white);
            coin.SetNativeSize();
            At(coin.rectTransform, from.x, from.y, new Vector2(0.5f, 0.5f));
            _flights.Add(new Flight { Rect = coin.rectTransform, From = Anchored(from), To = Anchored(to), Start = _clock + delay, Seconds = FlySeconds,
                Landed = () => coin.gameObject.SetActive(false) });
        }

        // 지금부터 날아간다(Fly는 0초부터 센다)
        private Flight FlyNow(RectTransform rect, Vector2 from, Vector2 to)
        {
            Fly(rect, from, to, false);
            Flight flight = _flights[_flights.Count - 1];
            flight.Start = _clock;
            return flight;
        }

        // 받은 효과를 떠오르는 글로 — 「골드 +10」 · 「진열 칸 +1」 · 「턴 수입 -1」(늘면 초록 · 줄면 빨강), 한 줄씩 아래로
        private void NewsFloats(NewsChoice c, int gold, float x, float y)
        {
            var lines = new List<string>();
            if (gold != 0) lines.Add("골드 " + Signed(gold));
            if (c.Income != 0) lines.Add("턴 수입 " + Signed(c.Income));
            if (c.NextIncome != 0) lines.Add("다음 턴 수입 " + Signed(c.NextIncome));
            if (c.NextIncomeScale != 1f)
            {
                string scale = "×" + c.NextIncomeScale.ToString("0.##");
                lines.Add("다음 턴 수입 " + (c.NextIncomeScale > 1f ? Bonus(scale) : Enemy(scale)));
            }
            if (c.Action == NewsAction.HeroShop) lines.Add("진열 " + Bonus("영웅 이상"));
            if (c.FreeRerolls != 0) lines.Add("리롤 " + Signed(c.FreeRerolls));
            if (c.OfferSlots != 0) lines.Add("진열 칸 " + Signed(c.OfferSlots));
            if (c.TierAdvance != 0) lines.Add("상점 단계 " + Signed(c.TierAdvance));
            for (int i = 0; i < lines.Count; i++)
                FloatText(lines[i], x, y + i * 32f);
        }

        private static string Signed(int value) => value > 0 ? Bonus("+" + value) : Enemy(value.ToString());

        // 이주민 행렬 — 성문(① 받아들인다) · 통행세 상자(② 통행세를 받는다). 이주민 넷이 오른쪽 밖에서 걸어 들어와 성문 앞에 선다
        private void BuildMigrants()
        {
            WorldConfig world = RunState.Config.World;
            LinkThing(0, Prop(world.NewsGate, Center + 40f, Ground).rectTransform);
            LinkThing(1, Prop(world.NewsTollBox, Center + 130f, Ground).rectTransform);
            Sprite[] people = world.NewsMigrants;
            for (int i = 0; i < 4 && people.Length > 0; i++)
            {
                Sprite sprite = people[i % people.Length];
                Image migrant = NewImage(_things, "Migrant", sprite, Color.white);
                migrant.rectTransform.sizeDelta = sprite.rect.size * K;
                migrant.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                // 무대 안(땅 아래 · 판 밖은 잘린다)
                Sunken(migrant.rectTransform, Center + 230f + i * 60f, Ground, new Vector2(0.5f, 0f));
                Actor actor = AddActor(migrant.rectTransform, i);
                actor.From = new Vector2(PanelWidth + 40f + i * 60f, actor.To.y);
                actor.Start = PopupMotion.OpenSeconds;
                actor.Speed = NewsWalkSpeed;
                _newsGroup.Add(migrant.rectTransform);
            }
        }

        // 풍년! — 수레(① 곡식을 판다) · 밭(② 밭을 넓힌다). 내 백성 셋이 볏단을 이고 밭에 서 있다
        private void BuildHarvest()
        {
            WorldConfig world = RunState.Config.World;
            Sprite tile = world.NewsField;
            RectTransform field = NewRect(_things, "Field");
            field.sizeDelta = new Vector2(3f * tile.rect.width * K, tile.rect.height * K);
            At(field, Center - 56f, Ground, new Vector2(0.5f, 0f));
            for (int i = 0; i < 3; i++)
            {
                Image part = NewImage(field, "Field", tile, Color.white);
                part.rectTransform.sizeDelta = tile.rect.size * K;
                part.rectTransform.anchorMin = part.rectTransform.anchorMax = part.rectTransform.pivot = Vector2.zero;
                part.rectTransform.anchoredPosition = new Vector2(i * tile.rect.width * K, 0f);
            }
            LinkThing(1, field);
            LinkThing(0, Prop(world.NewsCart, Center + 250f, Ground).rectTransform);
            Workers(new[] { Center - 150f, Center - 60f, Center + 30f }, world.NewsSheaf, null);
        }

        // 대화재 — 내 성 위 불길(① 불길에 맡긴다 — 기물 하나가 타고 한 등급 높은 기물이 나온다) · 물통 줄(② 불을 끈다)
        private void BuildFire()
        {
            WorldConfig world = RunState.Config.World;
            Sprite castle = world.Castle(RunState.Nation);
            if (castle != null)
            {
                Image image = NewImage(_things, "Castle", LordPaint.Of(castle, RunState.ColorOf(RunState.World.State.Player)), Color.white);
                image.rectTransform.sizeDelta = castle.rect.size * 2f;
                At(image.rectTransform, Center + 150f, Ground, new Vector2(0.5f, 0f));
            }
            // 불길 셋 — 성벽 위(성 그림 아래에서 35px · 배율 2)
            Sprite[] frames = world.NewsFlame;
            RectTransform flames = NewRect(_things, "Flames");
            flames.sizeDelta = new Vector2(120f + frames[0].rect.width * K, frames[0].rect.height * K);
            At(flames, Center + 150f, Ground - 70f, new Vector2(0.5f, 0f));
            for (int i = 0; i < 3; i++)
            {
                Image flame = NewImage(flames, "Flame", frames[i % frames.Length], Color.white);
                flame.rectTransform.sizeDelta = frames[0].rect.size * K;
                flame.rectTransform.anchorMin = flame.rectTransform.anchorMax = flame.rectTransform.pivot = Vector2.zero;
                flame.rectTransform.anchoredPosition = new Vector2(i * 60f, 0f);
                _flames.Add(flame);
            }
            LinkThing(0, flames);
            // 물통 줄 — 내 백성 셋이 물통을 이고 성을 본다
            RectTransform line = NewRect(_things, "Buckets");
            line.sizeDelta = new Vector2(210f, (8f + world.NewsBucket.rect.height) * K);
            At(line, Center - 160f, Ground, new Vector2(0.5f, 0f));
            Workers(new[] { 35f, 105f, 175f }, world.NewsBucket, line);
            LinkThing(1, line);
        }

        // 고른 뒤의 결과 연출 — choice = 고른 칸, before = 고르기 전 탑의 층들, floor = 고른 탑 층(기물을 고르는 고르기만)
        private void PlayNews(NewsChoice chosen, int choice, OwnedBuilding[] before, int floor)
        {
            int gold = RunState.Gold - _goldBefore;
            _newsPlay = true;
            _boxHover = null;
            foreach (Actor actor in _actors)
                actor.From = actor.To;
            _clock = 0f;
            foreach (NewsBox box in _boxes)
            {
                box.Rect.gameObject.SetActive(box.Choice == choice);
                if (box.Thing != null)
                    box.Thing.anchoredPosition = box.ThingHome;
            }
            _caretImage.gameObject.SetActive(false);
            SetAction(null);
            if (floor >= 0)
                SetLine(_newsStory);
            _skip.gameObject.SetActive(true);
            _skip.transform.SetAsLastSibling();
            _newsEnd = chosen.Action == NewsAction.Alchemy ? () => ShowAlchemy(floor) : (System.Action)NextNews;
            float end;
            bool first = choice == 0;
            switch (_newsScene)
            {
                case NewsScene.Migrants: end = first ? MigrantsIn(chosen, gold) : MigrantsToll(chosen, gold); break;
                case NewsScene.Harvest: end = first ? HarvestSell(chosen, gold) : HarvestField(chosen, gold); break;
                case NewsScene.Fire: end = first ? FireBurn(before) : FireDouse(chosen, gold); break;
                case NewsScene.GoldMine: end = first ? MineSell(chosen, gold) : MineDig(chosen, gold); break;
                case NewsScene.Locust: end = first ? LocustPay(chosen, gold) : LocustYield(chosen, gold, before); break;
                case NewsScene.Caravan: end = first ? CaravanRare(chosen, gold) : CaravanReroll(chosen, gold); break;
                case NewsScene.Master: end = MasterWork(chosen, gold, first ? floor : ChangedFloor(before), before); break;
                case NewsScene.Scholar: end = first ? ScholarJoin(chosen, gold) : ScholarLeave(chosen, gold); break;
                case NewsScene.Alchemist: end = first ? AlchemyBrew(chosen, gold, floor, before) : AlchemistLeave(); break;
                default: end = first ? FestivalGift(before) : FestivalMedal(floor, before); break;
            }
            Cue(end + NewsHold, () => _newsEnd());
        }

        // ── 금광 발견 — 굴 앞 금덩이 더미(① 판다) · 굴(② 갱도를 판다). 백성 둘이 굴 앞에서 들썩인다
        // 굴 자리(판 가운데에서) · 금빛 반짝임(이펙트 — 불꽃 그림의 노랑)
        private const float MineX = 220f;
        private static readonly Color GoldSpark = new Color(1f, 0.847f, 0.29f);

        private void BuildGoldMine()
        {
            WorldConfig world = RunState.Config.World;
            Sprite caveLook = _cave != null ? _cave : world.NewsMineFrame;
            // 굴은 무대 안에(받침목 틀이 그 앞 · 땅 아래에서 올라온다)
            Image cave = NewImage(_things, "Cave", caveLook, Color.white);
            cave.rectTransform.sizeDelta = caveLook.rect.size * K;
            Sunken(cave.rectTransform, Center + MineX, Ground, new Vector2(0.5f, 0f));
            LinkThing(1, cave.rectTransform);
            Image pile = Prop(world.NewsGoldPile, Center + 20f, Ground);
            LinkThing(0, pile.rectTransform);
            Twinkle(Center - 4f, Ground - 30f, GoldSpark, 0);
            Twinkle(Center + 44f, Ground - 14f, GoldSpark, 1);
            Workers(new[] { Center - 200f, Center - 120f }, null, null);
        }

        // ① 금덩이 더미가 사라지고 동전이 골드로
        private float MineSell(NewsChoice chosen, int gold)
        {
            RectTransform pile = _boxes[0].Thing;
            Vector2 at = Middle(pile);
            Cue(0.2f, () =>
            {
                pile.gameObject.SetActive(false);
                foreach (var anim in _anims)
                    anim.Image.gameObject.SetActive(false);
                for (int i = 0; i < 6; i++)
                    Coin(at + new Vector2((i - 2.5f) * 12f, 0f), Middle(_panel.GoldIcon), i * 0.04f);
                NewsFloats(chosen, gold, at.x, at.y - 90f);
            });
            return 0.2f + FlySeconds + 0.3f;
        }

        // ② 골드에서 동전이 굴로 들어가고, 굴 앞에 받침목 틀이 땅 아래에서 그림 px 계단으로 올라온다
        private float MineDig(NewsChoice chosen, int gold)
        {
            RectTransform cave = _boxes[1].Thing;
            Cue(0f, () =>
            {
                for (int i = 0; i < 10; i++)
                    Coin(Middle(_panel.GoldIcon), Middle(cave), i * 0.05f);
            });
            Sprite sprite = RunState.Config.World.NewsMineFrame;
            Image frame = NewImage(_things, "MineFrame", sprite, Color.white);
            frame.rectTransform.sizeDelta = sprite.rect.size * K;
            Sunken(frame.rectTransform, Center + MineX, Ground + sprite.rect.height * K, new Vector2(0.5f, 0f));
            int steps = Mathf.RoundToInt(sprite.rect.height);
            for (int i = 1; i <= steps; i++)
                Cue(0.8f + i * 0.06f, () => frame.rectTransform.anchoredPosition += new Vector2(0f, K));
            Cue(0.8f, () => NewsFloats(chosen, gold, Center + MineX, Ground - 170f));
            return 0.8f + steps * 0.06f + 0.3f;
        }

        // ── 황충 발생 — 밭을 덮은 메뚜기 떼 · 돈자루(① 돈으로 쫓는다) · 밭(② 밭을 내준다 — 남은 일손으로 생산 기물 하나)
        private void BuildLocust()
        {
            WorldConfig world = RunState.Config.World;
            Sprite tile = world.NewsField;
            RectTransform field = NewRect(_things, "Field");
            field.sizeDelta = new Vector2(3f * tile.rect.width * K, tile.rect.height * K);
            At(field, Center - 56f, Ground, new Vector2(0.5f, 0f));
            for (int i = 0; i < 3; i++)
            {
                Image part = NewImage(field, "Field", tile, Color.white);
                part.rectTransform.sizeDelta = tile.rect.size * K;
                part.rectTransform.anchorMin = part.rectTransform.anchorMax = part.rectTransform.pivot = Vector2.zero;
                part.rectTransform.anchoredPosition = new Vector2(i * tile.rect.width * K, 0f);
            }
            LinkThing(1, field);
            LinkThing(0, Prop(world.NewsMoneyBag, Center + 300f, Ground).rectTransform);
            // 떼 — 밭 위에 흩어 둔다(같은 자리 · 같은 모양이 매번)
            var random = new System.Random(11);
            for (int i = 0; i < 18; i++)
            {
                Image locust = NewImage(_things, "Locust", world.NewsLocust, Color.white);
                locust.rectTransform.sizeDelta = world.NewsLocust.rect.size * K;
                if (random.Next(2) == 0)
                    locust.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                float x = Center - 190f + Mathf.Round(random.Next(0, 45)) * K;
                float y = Ground - 42f - random.Next(0, 14) * K;
                Sunken(locust.rectTransform, x, y, new Vector2(0.5f, 0.5f));
                _swarm.Add((locust.rectTransform, locust.rectTransform.anchoredPosition));
            }
            // 백성 둘이 떼를 보고(왼쪽) 쫓는다
            Workers(new[] { Center + 140f, Center + 210f }, null, null, true);
        }

        // ① 골드에서 동전이 돈자루로 → 떼가 오른쪽 위로 흩어진다
        private float LocustPay(NewsChoice chosen, int gold)
        {
            RectTransform bag = _boxes[0].Thing;
            Cue(0f, () =>
            {
                for (int i = 0; i < 4; i++)
                    Coin(Middle(_panel.GoldIcon), Middle(bag), i * 0.06f);
                NewsFloats(chosen, gold, Middle(bag).x, Ground - 150f);
            });
            Cue(0.5f, () => _scatterAt = _clock);
            return 1.8f;
        }

        // ② 밭 가운데에서 새 생산 기물의 층이 탑으로 날아간다
        private float LocustYield(NewsChoice chosen, int gold, OwnedBuilding[] before)
        {
            int at = ChangedFloor(before);
            if (at >= 0)
                FloorArrive(at, Middle(_boxes[1].Thing) - new Vector2(0f, 40f), 0.3f);
            Cue(0.3f, () => NewsFloats(chosen, gold, Center - 56f, Ground - 170f));
            return 0.3f + FlySeconds + 0.6f;
        }

        // ── 대상단 도착 — 상인(손님)과 짐 수레 · 귀한 상자(① 영웅 진열) · 짐 더미(② 리롤)
        private void BuildCaravan()
        {
            WorldConfig world = RunState.Config.World;
            Prop(world.NewsCart, Center + 330f, Ground);
            LinkThing(0, Prop(world.NewsRareChest, Center - 120f, Ground).rectTransform);
            LinkThing(1, Prop(world.NewsBundles, Center + 40f, Ground).rectTransform);
            Guest(world.NewsMerchant, Center + 190f);
        }

        // ① 귀한 상자가 두 번 뛰고 영웅 테두리 색으로 반짝인다
        private float CaravanRare(NewsChoice chosen, int gold)
        {
            RectTransform chest = _boxes[0].Thing;
            Vector2 at = Middle(chest);
            Color epic = BuildingRarity.Epic.FrameColor();
            Hop(chest, 0.1f, 2);
            Hop(chest, 0.45f, 2);
            Cue(0.3f, () =>
            {
                Twinkle(at.x - 40f, at.y - 50f, epic, 0);
                Twinkle(at.x + 36f, at.y - 70f, epic, 1);
                Twinkle(at.x, at.y - 96f, epic, 2);
                NewsFloats(chosen, gold, at.x, at.y - 150f);
            });
            return 1.4f;
        }

        // ② 짐 더미를 세 번 다시 펼친다(뛴다)
        private float CaravanReroll(NewsChoice chosen, int gold)
        {
            RectTransform bundles = _boxes[1].Thing;
            for (int i = 0; i < 3; i++)
                Hop(bundles, 0.1f + i * 0.4f, 2);
            Cue(0.1f, () => NewsFloats(chosen, gold, Middle(bundles).x, Ground - 150f));
            return 1.4f;
        }

        // ── 명장 방문 — 이름난 장인(대장장이, 손님) · 모루(① 값을 치르고 고른 기물) · 장인(② 장인이 고른 기물). 모루 옆에 돈자루
        private const float AnvilX = -40f;

        private void BuildMaster()
        {
            WorldConfig world = RunState.Config.World;
            Prop(world.NewsMoneyBag, Center + AnvilX - 90f, Ground);
            LinkThing(0, Prop(_anvil, Center + AnvilX, Ground).rectTransform);
            RectTransform smith = Figure(_smith, 0f, 0f, false, Color.white);
            Guest(smith, Center + 110f);
            LinkThing(1, smith);
        }

        // 기물 하나가 모루 위로 와서 불꽃 · 오른 등급 색이 되어 돌아간다(① 돈자루에 값부터)
        private float MasterWork(NewsChoice chosen, int gold, int at, OwnedBuilding[] before)
        {
            float start = 0f;
            if (chosen.Gold < 0)
            {
                Vector2 bag = new Vector2(Center + AnvilX - 90f, Ground - 20f);
                Cue(0f, () =>
                {
                    for (int i = 0; i < 4; i++)
                        Coin(Middle(_panel.GoldIcon), bag, i * 0.06f);
                });
                start = 0.6f;
            }
            Cue(start, () => NewsFloats(chosen, gold, Center + AnvilX - 150f, Ground - 150f));
            if (at < 0)
                return start + 0.6f;
            float top = Ground - _anvil.rect.height * K - 60f;
            return FloorTrip(at, before[at].Definition, _rarityBefore[at].FrameColor(), RunState.Field[at].Rarity.FrameColor(), new Vector2(Center + AnvilX, top), start, () =>
            {
                Spark(Center + AnvilX - 70f, top - 10f);
                Spark(Center + AnvilX + 64f, top + 20f);
            });
        }

        // ── 학자 등용 — 떠돌이 학자(손님) · 벼슬 패(① 등용 — 학자가 내 영주 옆에 선다) · 책(② 책만 두고 떠난다)
        private void BuildScholar()
        {
            WorldConfig world = RunState.Config.World;
            LinkThing(0, Prop(world.NewsTablet, Center + NewsLordX + 90f, Ground).rectTransform);
            LinkThing(1, Prop(world.NewsBooks, Center - 40f, Ground).rectTransform);
            Guest(world.NewsScholar, Center - 140f);
        }

        // ① 벼슬 패가 학자 머리 위로 → 학자가 돌아서 내 영주 옆으로 걸어와 선다(내 영주처럼 오른쪽을 본다)
        private float ScholarJoin(NewsChoice chosen, int gold)
        {
            Actor scholar = _actors[_actors.Count - 1];
            RectTransform tablet = _boxes[0].Thing;
            Cue(0f, () => FlyNow(tablet, Middle(tablet), Middle(scholar.Rect) - new Vector2(0f, scholar.Rect.sizeDelta.y * 0.5f + 30f))
                .Landed = () => tablet.gameObject.SetActive(false));
            Cue(0.2f, () => NewsFloats(chosen, gold, Center - 140f, Ground - 150f));
            Walk(scholar, Center + NewsLordX + 80f, 0.6f);
            Cue(0.6f + (Center - 140f - (Center + NewsLordX + 80f)) / NewsWalkSpeed, () => scholar.Rect.localScale = Vector3.one);
            return 0.6f + 220f / NewsWalkSpeed + 0.3f;
        }

        // ② 학자가 돌아서 오른쪽 밖으로 — 책은 남는다
        private float ScholarLeave(NewsChoice chosen, int gold)
        {
            Cue(0.1f, () => NewsFloats(chosen, gold, Center - 40f, Ground - 150f));
            Leave(_actors[_actors.Count - 1], 0.4f);
            return 2.2f;
        }

        // ── 연금술사의 제안 — 연기 나는 솥(① 맡긴다 — 탑 층을 고른 뒤) · 연금술사(손님, ② 돌려보낸다)
        private const float CauldronX = -60f;

        private void BuildAlchemist()
        {
            WorldConfig world = RunState.Config.World;
            Image pot = Prop(world.NewsCauldron, Center + CauldronX, Ground);
            LinkThing(0, pot.rectTransform);
            float top = Ground - world.NewsCauldron.rect.height * K;
            Anim(world.NewsSmoke, Center + CauldronX - 18f, top, 0);
            Anim(world.NewsSmoke, Center + CauldronX + 18f, top - 12f, 1);
            Image alchemist = NewImage(_things, "Alchemist", world.NewsAlchemist, Color.white);
            alchemist.rectTransform.sizeDelta = world.NewsAlchemist.rect.size * K;
            Guest(alchemist.rectTransform, Center + 80f);
            LinkThing(1, alchemist.rectTransform);
        }

        // ① 골드에서 동전이 솥으로 → 고른 층이 솥 위로 와서 연기 속에 신화(성공) 또는 잿빛(실패)이 되어 돌아간다 → 결과 한 줄
        private float AlchemyBrew(NewsChoice chosen, int gold, int at, OwnedBuilding[] before)
        {
            Vector2 pot = new Vector2(Center + CauldronX, Ground - RunState.Config.World.NewsCauldron.rect.height * K * 0.5f);
            Cue(0f, () =>
            {
                for (int i = 0; i < 4; i++)
                    Coin(Middle(_panel.GoldIcon), pot, i * 0.06f);
                NewsFloats(chosen, gold, pot.x - 110f, Ground - 150f);
            });
            if (at < 0)
                return 0.6f;
            Color to = RunState.LastAlchemySucceeded ? BuildingRarity.Mythic.FrameColor() : Drained;
            Sprite[] smoke = RunState.Config.World.NewsSmoke;
            // 공중 연기는 결과가 뜰 때 걷힌다(ShowAlchemy — 솥 위 연기는 남는다)
            return FloorTrip(at, before[at].Definition, _rarityBefore[at].FrameColor(), to, pot - new Vector2(0f, 150f), 0.6f, () =>
            {
                _puffs.Add(Anim(smoke, pot.x - 50f, pot.y - 130f, 2));
                _puffs.Add(Anim(smoke, pot.x + 50f, pot.y - 150f, 0));
            });
        }

        // ② 연금술사가 돌아서 떠난다
        private float AlchemistLeave()
        {
            Leave(_actors[_actors.Count - 1], 0.2f);
            return 2f;
        }

        // ── 건국 기념제 — 내 영주가 가운데 · 양 끝에 내 색 깃발 · 양옆에서 백성이 뛰고 하늘에 폭죽. 선물 상자(① 전설 기물) · 훈장(② 고른 기물 한 등급)
        private const float GiftX = -110f, MedalX = 110f;

        private void BuildFestival()
        {
            WorldConfig world = RunState.Config.World;
            Color mine = RunState.ColorOf(RunState.World.State.Player);
            foreach (float side in new[] { -1f, 1f })
            {
                Image banner = Prop(_lordFlag, Center + side * 400f, Ground);
                banner.color = mine;
                if (side > 0f)
                    banner.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
            LinkThing(0, Prop(world.NewsGift, Center + GiftX, Ground).rectTransform);
            LinkThing(1, Prop(world.NewsMedal, Center + MedalX, Ground).rectTransform);
            foreach (Actor actor in Workers(new[] { Center - 300f, Center - 220f }, null, null))
                actor.Jumps = true;
            foreach (Actor actor in Workers(new[] { Center + 220f, Center + 300f }, null, null, true))
                actor.Jumps = true;
            // 폭죽 — 작게 → 크게 → 빈 하늘(자리마다 박자를 어긋나게)
            Sprite[] fw = world.NewsFirework;
            var frames = new[] { fw[0], fw[1], null, null, null };
            float[] xs = { -260f, 20f, 270f };
            for (int i = 0; i < xs.Length; i++)
                Anim(frames, Center + xs[i], Ground - 190f - (i % 2) * 40f, i * 2);
        }

        // ① 선물 상자가 뛰고, 전설 기물의 층이 상자에서 탑으로 날아간다
        private float FestivalGift(OwnedBuilding[] before)
        {
            RectTransform gift = _boxes[0].Thing;
            Hop(gift, 0.1f, 2);
            int at = ChangedFloor(before);
            if (at >= 0)
                FloorArrive(at, Middle(gift) - new Vector2(0f, 40f), 0.35f);
            return 0.35f + FlySeconds + 0.6f;
        }

        // ② 고른 층이 내 영주 앞으로 와서 훈장을 받고(오른 등급 색) 돌아간다
        private float FestivalMedal(int at, OwnedBuilding[] before)
        {
            RectTransform medal = _boxes[1].Thing;
            if (at < 0)
                return 0.6f;
            Vector2 spot = new Vector2(Center + MedalX, Ground - 170f);
            return FloorTrip(at, before[at].Definition, _rarityBefore[at].FrameColor(), RunState.Field[at].Rarity.FrameColor(), spot, 0f, () =>
                FlyNow(medal, Middle(medal), spot + new Vector2(0f, 40f)).Landed = () =>
                {
                    medal.gameObject.SetActive(false);
                    Spark(spot.x - 60f, spot.y - 10f);
                });
        }

        // ① 이주민이 성문을 지나 내 영주 앞까지 걸어 들어온다
        private float MigrantsIn(NewsChoice chosen, int gold)
        {
            float far = 0f;
            for (int i = 0; i < _actors.Count; i++)
            {
                Actor actor = _actors[i];
                actor.To = new Vector2(Mathf.Round(Center - 260f + i * 60f - Left - 4f), actor.From.y);
                actor.Start = 0.2f;
                actor.Speed = NewsWalkSpeed;
                far = Mathf.Max(far, (actor.From.x - actor.To.x) / NewsWalkSpeed);
            }
            Cue(0.3f, () => NewsFloats(chosen, gold, Center + 40f, Ground - 150f));
            return 0.2f + far;
        }

        // ② 이주민마다 동전이 통행세 상자로 → 골드로. 이주민은 돌아서 오른쪽 밖으로 걸어 나간다
        private float MigrantsToll(NewsChoice chosen, int gold)
        {
            RectTransform box = _boxes[1].Thing;
            Cue(0f, () =>
            {
                for (int i = 0; i < _newsGroup.Count; i++)
                    Coin(Middle(_newsGroup[i]), Middle(box), i * 0.08f);
            });
            Cue(0.6f, () =>
            {
                for (int i = 0; i < 5; i++)
                    Coin(Middle(box), Middle(_panel.GoldIcon), i * 0.04f);
                NewsFloats(chosen, gold, Center + 130f, Ground - 120f);
            });
            Cue(0.9f, () =>
            {
                foreach (Actor actor in _actors)
                {
                    actor.Rect.localScale = Vector3.one;
                    actor.To = new Vector2(actor.From.x + 400f, actor.From.y);
                    actor.Start = 1.1f;
                    actor.Speed = NewsWalkSpeed;
                }
            });
            return 2.4f;
        }

        // ① 볏단이 수레로 → 동전이 골드로
        private float HarvestSell(NewsChoice chosen, int gold)
        {
            RectTransform cart = _boxes[0].Thing;
            Vector2 top = Middle(cart) - new Vector2(0f, cart.sizeDelta.y * 0.5f + 20f);
            for (int i = 0; i < _newsLoads.Count; i++)
            {
                RectTransform sheaf = _newsLoads[i];
                Vector2 to = top + new Vector2((i - (_newsLoads.Count - 1) * 0.5f) * 30f, 0f);
                Cue(i * 0.15f, () => FlyNow(sheaf, Middle(sheaf), to));
            }
            Cue(0.8f, () =>
            {
                for (int i = 0; i < 5; i++)
                    Coin(top, Middle(_panel.GoldIcon), i * 0.04f);
                NewsFloats(chosen, gold, Center + 250f, Ground - 170f);
            });
            return 1.4f;
        }

        // ② 밭 오른쪽에 새 밭 한 칸이 땅 아래에서 그림 px 계단으로 올라온다
        private float HarvestField(NewsChoice chosen, int gold)
        {
            Sprite sprite = RunState.Config.World.NewsField;
            Image tile = NewImage(_things, "Field", sprite, Color.white);
            tile.rectTransform.sizeDelta = sprite.rect.size * K;
            float x = Center - 56f + 1.5f * sprite.rect.width * K + sprite.rect.width * K * 0.5f;
            Sunken(tile.rectTransform, x, Ground + sprite.rect.height * K, new Vector2(0.5f, 0f));
            int steps = Mathf.RoundToInt(sprite.rect.height);
            for (int i = 1; i <= steps; i++)
                Cue(0.2f + i * 0.08f, () => tile.rectTransform.anchoredPosition += new Vector2(0f, K));
            Cue(0.3f, () => NewsFloats(chosen, gold, x, Ground - 130f));
            return 0.2f + steps * 0.08f + 0.4f;
        }

        // ① 불길이 번지고, 탑의 기물 하나가 불길로 날아가 타고 한 등급 높은 기물이 그 층으로 돌아온다
        private float FireBurn(OwnedBuilding[] before)
        {
            OwnedBuilding[] field = RunState.Field;
            int at = -1;
            for (int i = 0; i < field.Length && i < before.Length; i++)
                if (!ReferenceEquals(field[i], before[i]))
                    at = i;
            RectTransform flames = _boxes[0].Thing;
            Sprite[] frames = RunState.Config.World.NewsFlame;
            float[] more = { -100f, 100f };
            for (int i = 0; i < more.Length; i++)
            {
                float x = Center + 150f + more[i];
                Cue(0.15f + i * 0.2f, () =>
                {
                    Image flame = NewImage(_things, "Flame", frames[0], Color.white);
                    flame.rectTransform.sizeDelta = frames[0].rect.size * K;
                    At(flame.rectTransform, x, Ground - 70f, new Vector2(0.5f, 0f));
                    _flames.Add(flame);
                });
            }
            if (at < 0)
                return 0.6f;
            SlotView slot = _panel.FloorAt(at);
            Transform floor = slot != null ? slot.transform : _panel.Roof;
            _emptyFloor = at;
            Cue(0f, () =>
            {
                FloorLook chunk = Floor(before[at].Definition, 0f, 0f, out Image window);
                window.color = before[at].Rarity.FrameColor();
                FlyNow((RectTransform)chunk.transform, Middle(floor), Middle(flames)).Landed = () => chunk.gameObject.SetActive(false);
            });
            Cue(0.9f, () =>
            {
                OwnedBuilding gained = RunState.Field[at];
                FloorLook chunk = Floor(gained.Definition, 0f, 0f, out Image window);
                window.color = gained.Rarity.FrameColor();
                FlyNow((RectTransform)chunk.transform, Middle(flames), Middle(floor)).Landed = () =>
                {
                    chunk.gameObject.SetActive(false);
                    _emptyFloor = -1;
                };
            });
            return 1.2f + FlySeconds;
        }

        // ② 골드가 물통으로 → 물통이 차례로 불길로 날아가 불길이 하나씩 꺼진다
        private float FireDouse(NewsChoice chosen, int gold)
        {
            Cue(0f, () =>
            {
                for (int i = 0; i < _newsLoads.Count; i++)
                    Coin(Middle(_panel.GoldIcon), Middle(_newsLoads[i]), i * 0.08f);
                NewsFloats(chosen, gold, Center - 160f, Ground - 170f);
            });
            for (int i = 0; i < _newsLoads.Count && i < _flames.Count; i++)
            {
                RectTransform bucket = _newsLoads[i];
                Image flame = _flames[i];
                Cue(0.5f + i * 0.25f, () =>
                    FlyNow(bucket, Middle(bucket), Middle(flame.rectTransform)).Landed = () =>
                    {
                        bucket.gameObject.SetActive(false);
                        flame.gameObject.SetActive(false);
                    });
            }
            return 0.5f + 3 * 0.25f + FlySeconds;
        }

        // 소식 장면의 움직임 — 사람의 걸음 · 들썩임, 불꽃 그림, 결과 연출의 할 일
        private void NewsStage()
        {
            foreach (Actor actor in _actors)
            {
                if (actor.Rect == null)
                    continue;
                float distance = actor.To.x - actor.From.x;
                float walked = Mathf.Min(Mathf.Abs(distance), Mathf.Max(0f, _clock - actor.Start) * actor.Speed);
                if (walked < Mathf.Abs(distance))
                    actor.Rect.anchoredPosition = actor.From + UnitFigure.Step(Mathf.Sign(distance) * walked, K, walked > 0f);
                else
                {
                    // 짝 칸을 올린 손님은 들썩임 대신 1px 들린다(LinkThing)
                    bool lifted = !_newsPlay && _pickChoice < 0 && _boxHover != null && _boxHover.Enabled && _boxHover.Thing == actor.Rect;
                    int[] steps = actor.Jumps ? Jump : Bob;
                    int bob = lifted ? 1 : steps[(Mathf.FloorToInt(Time.unscaledTime / (actor.Jumps ? JumpSeconds : BobSeconds)) + actor.Offset) % steps.Length];
                    actor.Rect.anchoredPosition = actor.To + new Vector2(0f, bob * K);
                }
            }
            Sprite[] frames = RunState.Config.World.NewsFlame;
            for (int i = 0; i < _flames.Count; i++)
            {
                Sprite sprite = frames[(Mathf.FloorToInt(Time.unscaledTime / FlameSeconds) + i) % frames.Length];
                if (_flames[i].sprite != sprite)
                    _flames[i].sprite = sprite;
            }
            foreach ((Image image, Sprite[] look, int offset) in _anims)
            {
                Sprite sprite = look[(Mathf.FloorToInt(Time.unscaledTime / FlameSeconds) + offset) % look.Length];
                if (image.enabled != (sprite != null))
                    image.enabled = sprite != null;
                if (sprite != null && image.sprite != sprite)
                    image.sprite = sprite;
            }
            // 메뚜기 — 0.1초마다 1px씩 떨고, 흩어지면 오른쪽 위로 그림 px 계단(무대가 자른다)
            int jitter = Mathf.FloorToInt(Time.unscaledTime / 0.1f);
            float flown = _scatterAt >= 0f ? Mathf.Max(0f, _clock - _scatterAt) : 0f;
            for (int i = 0; i < _swarm.Count; i++)
            {
                (RectTransform rect, Vector2 home) = _swarm[i];
                int dx = (i * 7 + jitter) % 3 - 1, dy = (i * 5 + jitter * 2) % 3 - 1;
                Vector2 away = new Vector2(Mathf.Round(flown * (300f + i * 20f) / K), Mathf.Round(flown * (160f + i * 10f) / K)) * K;
                rect.anchoredPosition = home + new Vector2(dx, dy) * K + away;
            }
            if (!_newsPlay)
                return;
            var due = _cues.FindAll(cue => _clock >= cue.At);
            _cues.RemoveAll(cue => _clock >= cue.At);
            foreach (var (_, act) in due)
            {
                if (!_newsPlay)
                    break;
                act();
            }
        }

        // ── 장면마다
        private void Build()
        {
            EventLandDefinition definition = _offer.Definition;
            _title.text = definition.DisplayName;
            string line;
            switch (_offer.Kind)
            {
                case EventLandKind.Forge: line = BuildForge(); break;
                case EventLandKind.Temple: line = BuildTemple(); break;
                case EventLandKind.Mercenary: line = BuildMercenary(); break;
                case EventLandKind.Training: line = BuildTraining(); break;
                case EventLandKind.BloodAltar: line = BuildAltar(); break;
                case EventLandKind.Agitator: line = BuildAgitator(); break;
                case EventLandKind.Shrine: line = BuildShrine(); break;
                case EventLandKind.Watermill:
                case EventLandKind.Refugee: line = BuildBoon(); break;
                default: line = BuildTreasury(); break;
            }
            // 후보가 없으면(대장간에 올릴 것이 없음 등) 무대만 두고 「닫기」
            if (_offer.Choices.Length == 0)
                line = $"<color={BuildingTooltip.NoteColor}>지금 고를 수 있는 것이 없습니다.</color>";
            _lineIcons.SetText(BuildingTooltip.ColorNumbers(line));
            _line.rectTransform.sizeDelta = new Vector2(PanelWidth - 80f, _line.preferredHeight);
            _lineIcons.Place();
            string action = _offer.Kind == EventLandKind.BloodAltar && _offer.Choices.Length > 0 ? "바치지 않기"
                : _offer.Choices.Length == 0 ? "닫기"
                : _offer.Kind == EventLandKind.Shrine ? "받기" : null;
            _action.gameObject.SetActive(action != null);
            if (action != null)
                _actionLabel.text = action;
        }

        // 대장간 — 모루 · 대장장이. 고를 것 = 왼쪽 탑의 층(보관함 후보만 무대 오른쪽에 쌓아 세운다)
        private string BuildForge()
        {
            float anvil = Center + 40f;
            // 화산 대장간 — 대장장이 뒤 작은 분화구
            Scenery(_vent, anvil - 300f, Ground);
            Prop(_anvil, anvil, Ground);
            Figure(_smith, anvil - 150f, Ground, false, Color.white);
            _ghost = Floor(null, anvil, Ground - _anvil.rect.height * K, out Image ghostWindow);
            _ghostIcon = _ghost.transform.Find("Icon").GetComponent<Image>();
            _ghost.gameObject.AddComponent<CanvasGroup>().alpha = GhostAlpha;
            _ghost.gameObject.SetActive(false);
            int stored = 0;
            for (int i = 0; i < _offer.Choices.Length; i++)
            {
                EventChoice choice = _offer.Choices[i];
                string title = BuildingTooltip.Title(choice.Building.Definition, false, choice.To);
                string body = BuildingTooltip.RankUpPreview(choice.Building.Definition, choice.Building.Level, choice.To);
                if (!choice.Storage)
                {
                    SlotView slot = _panel.FloorAt(choice.Index);
                    if (slot == null)
                        continue;
                    EventVisitTarget target = slot.gameObject.AddComponent<EventVisitTarget>();
                    _slotTargets.Add(target);
                    var rarity = slot.transform.Find("RarityFrame").GetComponent<Image>();
                    Add(new Thing { Target = target, Choice = i, Side = true, Title = title, Body = body, Frame = rarity, FrameColor = choice.From.FrameColor(), Visual = (RectTransform)slot.transform, Building = choice.Building.Definition });
                    continue;
                }
                // 보관함 후보 — 두 줄 기둥(아래부터 둘씩), 넷까지. 아래에 「보관함」
                if (stored >= 4)
                    continue;
                float x = Center + 200f + stored / 2 * 130f, bottom = Ground - stored % 2 * 120f;
                stored++;
                FloorLook floor = Floor(choice.Building.Definition, x, bottom, out Image window);
                window.color = SlotView.Highlight(choice.From.FrameColor(), false);
                EventVisitTarget hit = Hit((RectTransform)floor.transform);
                Add(new Thing { Target = hit, Choice = i, Caret = new Vector2(x, bottom - 120f - 8f), Title = title, Body = body, Frame = window, FrameColor = choice.From.FrameColor(), Visual = (RectTransform)floor.transform, Building = choice.Building.Definition });
            }
            if (stored > 0)
            {
                Text label = NewText(_things, "Storage", _dotFont, 24, 300f);
                label.text = $"<color={BuildingTooltip.NoteColor}>보관함</color>";
                At(label.rectTransform, Center + 200f + (stored > 2 ? 65f : 0f), Ground + 12f, new Vector2(0.5f, 1f));
            }
            return "왼쪽 탑의 기물을 눌러 모루에 올리면, 그 기물의 등급이 한 단계 오릅니다.";
        }

        // 물레방아 · 피난민 — 대장간과 같은 문법: 왼쪽 탑의 층을 눌러 고른다(보관함은 후보가 아니다). 무대 = 그 이벤트의 소품(EventLandDefinition.Prop) 하나, 위에 고를 층의 그림자
        private float BoonX => Center + 40f;
        private float BoonTop => Ground - (_offer.Definition.Prop != null ? _offer.Definition.Prop.rect.height * K : 0f);

        private string BuildBoon()
        {
            if (_offer.Definition.Prop != null)
                Prop(_offer.Definition.Prop, BoonX, Ground);
            _ghost = Floor(null, BoonX, BoonTop, out Image ghostWindow);
            _ghostIcon = _ghost.transform.Find("Icon").GetComponent<Image>();
            _ghost.gameObject.AddComponent<CanvasGroup>().alpha = GhostAlpha;
            _ghost.gameObject.SetActive(false);
            for (int i = 0; i < _offer.Choices.Length; i++)
            {
                EventChoice choice = _offer.Choices[i];
                SlotView slot = _panel.FloorAt(choice.Index);
                if (slot == null)
                    continue;
                EventVisitTarget target = slot.gameObject.AddComponent<EventVisitTarget>();
                _slotTargets.Add(target);
                var rarity = slot.transform.Find("RarityFrame").GetComponent<Image>();
                Add(new Thing { Target = target, Choice = i, Side = true, Title = BuildingTooltip.Title(choice.Building.Definition, false, choice.From),
                    Body = BuildingTooltip.BoonPreview(choice.Building, choice.Factor, choice.Summons), Frame = rarity, FrameColor = choice.From.FrameColor(),
                    Visual = (RectTransform)slot.transform, Building = choice.Building.Definition });
            }
            return _offer.Kind == EventLandKind.Watermill
                ? $"왼쪽 탑의 기물을 눌러 물레방아에 걸면, 그 기물의 쿨다운이 영구히 ×{_offer.Definition.CooldownFactor:0.##}가 됩니다."
                : $"왼쪽 탑의 기물을 누르면, 그 기물이 소환하는 유닛 수가 영구히 +{_offer.Definition.Summons} 됩니다.";
        }

        // 신전 — 제단 위 사제, 촛대마다 내 업그레이드(다음 등급 테두리)
        private string BuildTemple()
        {
            float altar = Center - 250f;
            Prop(_altar, altar, Ground);
            Figure(_priest, altar, Ground - _altar.rect.height * K, false, Color.white);
            int n = _offer.Choices.Length;
            float step = n > 1 ? Mathf.Min(150f, 470f / (n - 1)) : 0f;
            bool fresh = false;
            for (int i = 0; i < n; i++)
            {
                EventChoice choice = _offer.Choices[i];
                float x = Mathf.Round(Center - 20f + i * step);
                Prop(_candle, x, Ground);
                float bottom = Ground - _candle.rect.height * K - 26f;
                Image cell = Cell(choice.Upgrade.Icon, choice.To.FrameColor(), x, bottom);
                string after = choice.Upgrade.Describe(choice.Upgrade.ValueAt(choice.To));
                string body = choice.New ? BuildingTooltip.Highlight(after)
                    : $"{choice.To.DisplayName()} 등급이 되면\n{Changed(choice.Upgrade.Describe(choice.Upgrade.ValueAt(choice.From)), after)}";
                fresh |= choice.New;
                Add(new Thing { Target = Hit(cell.rectTransform), Choice = i, Caret = new Vector2(x, bottom - 36f - 10f), Title = choice.Upgrade.DisplayName, Body = body, Frame = cell, FrameColor = choice.To.FrameColor(), Visual = cell.rectTransform });
            }
            return fresh ? "촛대 위 업그레이드를 하나 누르면, 그 업그레이드를 얻습니다."
                : "촛대 위 업그레이드를 하나 누르면, 그 업그레이드의 등급이 한 단계 오릅니다.";
        }

        // 용병 상인 — 다른 국가 짐꾼들이 기물 한 층씩을 이고 선다(상점 짐꾼과 같은 그림)
        private string BuildMercenary()
        {
            int n = _offer.Choices.Length;
            // 고갯마루 상인 — 산길 이정표
            Scenery(_signpost, Center - 400f, Ground);
            bool full = false;
            for (int i = 0; i < n; i++)
            {
                EventChoice choice = _offer.Choices[i];
                float x = Mathf.Round(Center + (i - (n - 1) * 0.5f) * 260f);
                RectTransform porter = Figure(_shop.PorterOf(choice.Offer.Nation), x, Ground, i == n - 1 && n > 1, Color.white);
                float top = Ground - porter.sizeDelta.y;
                Image glow = NewImage(_things, "Glow", _glow, Color.white);
                glow.SetNativeSize();
                At(glow.rectTransform, x, top - 8f - 60f, new Vector2(0.5f, 0.5f));
                glow.gameObject.SetActive(false);
                FloorLook floor = Floor(choice.Offer, x, top - 8f, out Image window);
                var owned = new OwnedBuilding(choice.Offer, choice.To - choice.Offer.Rarity, 0, 0, 1f);
                window.color = SlotView.Highlight(choice.To.FrameColor(), false);
                full |= !choice.Receivable;
                var thing = new Thing { Target = Hit((RectTransform)floor.transform), Choice = i, Caret = new Vector2(x, top - 8f - 120f - 18f), Owned = owned, Frame = window, FrameColor = choice.To.FrameColor(), Visual = (RectTransform)floor.transform };
                Add(thing);
                // 올린 짐꾼은 상점처럼 빛이 뒤에 선다
                thing.Target.Entered += _ => glow.gameObject.SetActive(true);
                thing.Target.Exited += _ => glow.gameObject.SetActive(false);
            }
            string line = "짐꾼을 하나 누르면, 그 짐꾼이 이고 있는 기물을 공짜로 받습니다.";
            if (full)
                line += $"\n<color={BuildingTooltip.NoteColor}>탑과 보관함에 자리가 없어 받지 못합니다.</color>";
            return line;
        }

        // 훈련장 — 내 유닛들(내 색)이 줄지어 서고 오른쪽에 허수아비
        private string BuildTraining()
        {
            float scarecrow = Center + 260f;
            Prop(_scarecrow, scarecrow, Ground);
            int n = _offer.Choices.Length;
            float step = n > 1 ? Mathf.Min(140f, 460f / (n - 1)) : 0f;
            Color me = RunState.ColorOf(RunState.World.State.Player);
            for (int i = 0; i < n; i++)
            {
                EventChoice choice = _offer.Choices[i];
                float x = Mathf.Round(Center - 330f + i * step);
                RectTransform figure = Figure(choice.Unit, x, Ground, false, me);
                string body = $"체력 {Number(BuildingTooltip.Whole(choice.Unit.MaxHp))}{Bonus("+" + choice.Hp)}\n공격력 {Number(BuildingTooltip.Whole(choice.Unit.AttackDamage))}{Bonus("+" + choice.Attack)}";
                Add(new Thing { Target = Hit(figure), Choice = i, Caret = new Vector2(x, Ground - figure.sizeDelta.y - 24f), Title = choice.Unit.DisplayName, Body = body, Visual = figure });
            }
            return "유닛을 하나 눌러 허수아비를 치게 하면, 그 유닛의 체력과 공격력이 영구히 오릅니다.";
        }

        // 피의 제단 — 제단 위 하트(먼저 이것을 눌러 바친다) · 그 위에 떠오른 지역 특수 업그레이드들(신화 테두리)
        private string BuildAltar()
        {
            EventLandDefinition definition = _offer.Definition;
            // 핏빛 늪 — 제단 양옆 물웅덩이
            Scenery(_puddle, Center - 200f, Ground);
            Scenery(_puddle, Center + 230f, Ground);
            Prop(_bloodAltar, Center, Ground);
            float heartBottom = Ground - _bloodAltar.rect.height * K - 2f;
            Image heart = Instantiate(_heartPart, _things);
            heart.name = "Heart";
            heart.color = HeartRow.HeartColor;
            At(heart.rectTransform, Center, heartBottom, new Vector2(0.5f, 0f));
            string heartBody = _offer.Blocked ? $"<color={BuildingTooltip.NoteColor}>목숨이 모자라 바칠 수 없습니다.</color>" : BuildingTooltip.Highlight($"목숨 {definition.LifeCost}을 바칩니다.");
            Add(new Thing { Target = Hit(heart.rectTransform), Choice = Heart, Caret = new Vector2(Center, heartBottom - 32f - 10f), Title = "목숨", Body = heartBody, Visual = heart.rectTransform });
            int n = _offer.Choices.Length;
            for (int i = 0; i < n; i++)
            {
                EventChoice choice = _offer.Choices[i];
                float off = i - (n - 1) * 0.5f;
                // 떠 있는 높이 — 설명 창 자리(하늘) 아래, 하트보다 위
                float x = Mathf.Round(Center + off * 200f), y = Ground - 110f - (Mathf.Abs(off) < 0.5f ? 30f : 0f);
                Image cell = Cell(choice.Upgrade.Icon, BuildingRarity.Mythic.FrameColor(), x, y + 18f);
                cell.GetComponent<CanvasGroup>().alpha = WaitingAlpha;
                Add(new Thing { Target = Hit(cell.rectTransform), Choice = i, Caret = new Vector2(x, y - 18f - 10f), Title = choice.Upgrade.DisplayName,
                    Body = BuildingTooltip.Highlight(choice.Upgrade.Describe(choice.Upgrade.ValueAt(BuildingRarity.Mythic))), Frame = cell, FrameColor = BuildingRarity.Mythic.FrameColor(), Visual = cell.rectTransform });
            }
            return $"하트를 눌러 목숨 {definition.LifeCost}을 바치면, 떠오른 신화 업그레이드 {n}개 중 하나를 고릅니다.";
        }

        private const int Heart = -1;

        // 선동가 — 연단 위 선동가(흰색), 그 앞에 이웃 영주들이 제 깃발을 들고 선다
        private string BuildAgitator()
        {
            float podium = Center - 260f;
            // 반란군 동굴 — 연단 뒤 굴 입구(자연 경계 · 사용자 10/26)
            Scenery(_cave, podium, Ground);
            Prop(_podium, podium, Ground);
            Figure(_agitator, podium, Ground - _podium.rect.height * K, false, Color.white);
            int n = _offer.Choices.Length;
            float step = n > 1 ? Mathf.Min(150f, 470f / (n - 1)) : 0f;
            for (int i = 0; i < n; i++)
            {
                EventChoice choice = _offer.Choices[i];
                float x = Mathf.Round(Center - 20f + i * step);
                RectTransform holder = NewRect(_things, "Lord");
                Sprite lordBody = WorldLords.BodyOf(choice.Lord.Id);
                At(holder, x, Ground - lordBody.rect.height * K * 0.5f - K, new Vector2(0.5f, 0.5f));
                holder.localScale = new Vector3(-1f, 1f, 1f);
                WorldLords.Figure(holder, lordBody, _lordFlag, WorldLords.PhotoOf(choice.Lord.Id), RunState.ColorOf(choice.Lord), K);
                RectTransform hitRect = NewRect(_things, "LordHit");
                hitRect.sizeDelta = lordBody.rect.size * K;
                At(hitRect, x, Ground - K, new Vector2(0.5f, 0f));
                string body = $"행복도 {Number(choice.Happiness)}{Enemy("-" + choice.Drop)}";
                if (choice.Happiness - choice.Drop <= 0)
                    body += $"\n<color={BuildingTooltip.NoteColor}>0이 되면 반란이 일어납니다.</color>";
                Add(new Thing { Target = Hit(hitRect), Choice = i, Caret = new Vector2(x, Ground - (lordBody.rect.height + _lordFlag.rect.height) * K - 10f), Title = RunState.NameOf(choice.Lord), Body = body, Visual = holder });
            }
            return $"이웃 영주를 하나 누르면, 그 영주의 행복도가 {_offer.Definition.HappinessDrop} 떨어집니다.";
        }

        // 지형 성소 — 성소 안에 그 땅의 지형 아이콘. 성소를 누르거나 「받기」
        private string BuildShrine()
        {
            MapDefinition terrain = RunState.World.Map.TerrainOf(_offer.Region);
            Image shrine = Prop(_shrine, Center, Ground);
            Image icon = NewImage(_things, "Terrain", terrain.Icon, Color.white);
            icon.SetNativeSize();
            At(icon.rectTransform, Center, Ground - 8 * K - 6f, new Vector2(0.5f, 0.5f));
            if (_offer.Choices.Length > 0)
            {
                EventChoice choice = _offer.Choices[0];
                Add(new Thing { Target = Hit(shrine.rectTransform), Choice = 0, Caret = new Vector2(Center, Ground - _shrine.rect.height * K - 10f), Title = choice.Upgrade.DisplayName,
                    Body = BuildingTooltip.Highlight(RunState.EventText(_offer.Region)), Visual = icon.rectTransform });
            }
            return $"「받기」를 누르면, 이번 런 동안 {terrain.DisplayName} 땅에서 싸울 때 내 유닛의 공격속도가 오릅니다.";
        }

        // 보물 창고 — 닫힌 상자. 누르면 뚜껑이 들리고 동전이 튄다
        private string BuildTreasury()
        {
            Image chest = Prop(_chest, Center, Ground);
            Image lid = Prop(_chestLid, Center, Ground - _chest.rect.height * K);
            lid.name = "Lid";
            if (_offer.Choices.Length > 0)
                Add(new Thing { Target = Hit(chest.rectTransform), Choice = 0, Caret = new Vector2(Center, Ground - (_chest.rect.height + _chestLid.rect.height) * K - 10f), Title = "보물 창고",
                    Body = BuildingTooltip.Highlight($"골드 +{_offer.Choices[0].Gold}"), Visual = lid.rectTransform });
            return "상자를 누르면, 골드를 받습니다.";
        }

        // ── 올림 · 누름
        private void Add(Thing thing)
        {
            _list.Add(thing);
            thing.Target.Entered += target => Enter(thing);
            thing.Target.Exited += target => Exit(thing);
            thing.Target.Clicked += target => Click(thing);
        }

        private bool Clickable(Thing thing)
        {
            if (_step != Step.Choosing)
                return false;
            if (_crisis != null)
                return true;
            if (_offer.Kind != EventLandKind.BloodAltar)
                return true;
            return thing.Choice == Heart ? !_offered && !_offer.Blocked : _offered;
        }

        private void Enter(Thing thing)
        {
            if (_step != Step.Choosing)
                return;
            _hover = thing;
            _hoverAt = Time.unscaledTime;
            Tip(thing);
        }

        private void Exit(Thing thing)
        {
            if (_hover != thing)
                return;
            _hover = null;
            BuildingTooltip.Hide(thing.Target);
        }

        private void Tip(Thing thing)
        {
            // 용병 상인의 기물 — 기물 판만(유닛 판까지 펼치면 무대의 짐꾼을 가린다)
            if (thing.Owned != null)
                BuildingTooltip.Show(thing.Target, BuildingTooltip.Title(thing.Owned.Definition, false, thing.Owned.Rarity),
                    BuildingTooltip.Describe(thing.Owned.Definition, thing.Owned.Level, thing.Owned.Cooldown, thing.Owned.Rarity == BuildingRarity.Mythic),
                    new UnitDefinition[0], RunState.Field, false, null, (thing.Owned.Rarity, thing.Owned.Definition.Nation));
            else
                BuildingTooltip.Show(thing.Target, thing.Title, thing.Body, new UnitDefinition[0], RunState.Field);
        }

        private void Click(Thing thing)
        {
            if (!Clickable(thing))
                return;
            if (thing.Choice == Heart)
            {
                // 하트를 바쳤다 — 하트가 꺼지고 업그레이드들이 밝아진다. 이제는 물릴 수 없다(「바치지 않기」가 사라진다)
                _offered = true;
                ((Image)thing.Visual.GetComponent<Image>()).color = new Color(0.45f, 0.12f, 0.12f);
                foreach (Thing other in _list)
                    if (other.Choice != Heart)
                        other.Visual.GetComponent<CanvasGroup>().alpha = 1f;
                _action.gameObject.SetActive(false);
                Exit(thing);
                return;
            }
            if (_crisis != null)
            {
                // 국가 위기 — 카드 고르기와 같은 길(대기열 맨 앞이 이 위기일 때만)
                if (RunState.PendingUpgrades.Count == 0 || RunState.PendingUpgrades[0] != _crisis)
                    return;
                RunState.ChooseUpgrade(thing.Choice);
                Exit(thing);
                BeginResult(thing);
                return;
            }
            if (!RunState.ChooseVisit(thing.Choice))
                return;
            Exit(thing);
            BeginResult(thing);
        }

        // 판 오른쪽 아래 버튼 — 「받기」(지형 성소) · 「바치지 않기」 · 「닫기」
        private void Act()
        {
            if (_step == Step.End)
            {
                _newRun?.Invoke();
                return;
            }
            if (_step == Step.News)
            {
                NewsAct();
                return;
            }
            if (_step != Step.Choosing)
                return;
            // 이긴 전투의 결과 — 「건너뛰기 +N골드」(멸망 보상을 고르지 않고 골드) · 「닫기」
            if (_aftermath)
            {
                if (_crisis != null)
                {
                    if (!RunState.CanSkipUpgrade || RunState.PendingUpgrades[0] != _crisis)
                        return;
                    RunState.SkipUpgrade();
                }
                BeginFlash();
                return;
            }
            if (_offer.Kind == EventLandKind.Shrine && _offer.Choices.Length > 0)
            {
                if (RunState.ChooseVisit(0))
                    BeginResult(_list[0]);
                return;
            }
            if (RunState.DeclineVisit())
                BeginFlash();
        }

        // ── 결과 연출
        private void BeginResult(Thing thing)
        {
            _step = Step.Result;
            _clock = 0f;
            _chosen = thing.Choice;
            _landedDone = false;
            _caretImage.gameObject.SetActive(false);
            if (_ghost != null)
                _ghost.gameObject.SetActive(false);
            _action.gameObject.SetActive(false);
            _skip.gameObject.SetActive(true);
            _skip.transform.SetAsLastSibling();
            if (_crisis != null)
            {
                // 국가 위기 — 고른 칸이 위 줄의 내 업그레이드 줄 끝으로(피의 제단과 같다)
                Sprite icon = _crisis.Choices[thing.Choice].Icon;
                Fly(thing.Visual, Middle(thing.Visual), Local(_panel.UpgradeCellOf(icon)), false);
                _flights[_flights.Count - 1].Follow = () => Anchored(Local(_panel.UpgradeCellOf(icon)));
                return;
            }
            EventChoice choice = _offer.Choices[thing.Choice];
            switch (_offer.Kind)
            {
                case EventLandKind.Forge:
                {
                    // 고른 층이 모루 위로 — 탑 자리는 그동안 빈 층. 닿으면 창틀이 오른 등급 색이 되고 불꽃
                    float anvil = Center + 40f;
                    FloorLook chunk = Floor(choice.Building.Definition, 0f, 0f, out Image window);
                    window.color = choice.From.FrameColor();
                    Fly((RectTransform)chunk.transform, Middle(thing.Visual), new Vector2(anvil, Ground - _anvil.rect.height * K - 60f), false);
                    ((RectTransform)chunk.transform).pivot = new Vector2(0.5f, 0.5f);
                    if (!choice.Storage)
                        _emptyFloor = choice.Index;
                    else
                        thing.Visual.gameObject.SetActive(false);
                    _landed = () =>
                    {
                        window.color = choice.To.FrameColor();
                        Spark(anvil - 70f, Ground - _anvil.rect.height * K - 70f);
                        Spark(anvil + 64f, Ground - _anvil.rect.height * K - 40f);
                    };
                    break;
                }
                case EventLandKind.Temple:
                case EventLandKind.BloodAltar:
                case EventLandKind.Shrine:
                    // 업그레이드 칸(성소는 지형 아이콘)이 위 줄의 내 업그레이드 줄 끝으로
                    Fly(thing.Visual, Middle(thing.Visual), Local(_panel.UpgradeCellOf(choice.Upgrade.Icon)), false);
                    _flights[_flights.Count - 1].Follow = () => Anchored(Local(_panel.UpgradeCellOf(choice.Upgrade.Icon)));
                    break;
                case EventLandKind.Mercenary:
                    // 이고 있던 층이 내 탑 지붕으로
                    Fly(thing.Visual, Middle(thing.Visual), Middle(_panel.Roof), false);
                    break;
                case EventLandKind.Training:
                {
                    // 고른 유닛이 허수아비 앞으로 걸어가 친다 — 불꽃 · 늘어난 값
                    float scarecrow = Center + 260f;
                    Vector2 from = thing.Visual.anchoredPosition;
                    Fly(thing.Visual, from, new Vector2(scarecrow - 90f - thing.Visual.sizeDelta.x * 0.5f - from.x, 0f) + from, true, true);
                    _landed = () =>
                    {
                        Spark(scarecrow - 20f, Ground - 10 * K);
                        float top = Ground - thing.Visual.sizeDelta.y - 70f;
                        FloatStat(InlineIcons.Kind.Hp, BuildingTooltip.HpIconColor, Bonus("+" + choice.Hp), scarecrow - 140f, top);
                        FloatStat(InlineIcons.Kind.Attack, BuildingTooltip.AttackIconColor, Bonus("+" + choice.Attack), scarecrow - 140f, top + 30f);
                    };
                    break;
                }
                case EventLandKind.Watermill:
                case EventLandKind.Refugee:
                {
                    // 대장간처럼 고른 층이 소품 위로 — 탑 자리는 그동안 빈 층. 닿으면 불꽃과 받은 값(「×0.75」 · 「+1」)
                    FloorLook chunk = Floor(choice.Building.Definition, 0f, 0f, out Image window);
                    window.color = choice.From.FrameColor();
                    Fly((RectTransform)chunk.transform, Middle(thing.Visual), new Vector2(BoonX, BoonTop - 60f), false);
                    ((RectTransform)chunk.transform).pivot = new Vector2(0.5f, 0.5f);
                    _emptyFloor = choice.Index;
                    string gain = _offer.Kind == EventLandKind.Watermill ? $"×{choice.Factor:0.##}" : $"+{choice.Summons}";
                    _landed = () =>
                    {
                        Spark(BoonX - 70f, BoonTop - 70f);
                        FloatText(Bonus(gain), BoonX + 130f, BoonTop - 90f);
                    };
                    break;
                }
                case EventLandKind.Agitator:
                    // 그 영주 머리 위에 「-N」
                    _landed = () => FloatText(Enemy("-" + choice.Drop), Middle(thing.Visual).x, thing.Caret.y + 10f);
                    _clock = FlySeconds;
                    break;
                default:
                {
                    // 보물 창고 — 뚜껑이 들리고 동전이 튀어 오른 뒤 위 줄 골드로
                    thing.Visual.anchoredPosition += new Vector2(0f, 30f);
                    float[] ox = { -90f, -40f, 10f, 60f, 110f, -10f }, oy = { 90f, 132f, 156f, 126f, 84f, 192f };
                    Vector2 gold = Middle(_panel.GoldIcon);
                    for (int i = 0; i < ox.Length; i++)
                    {
                        Image coin = NewImage(_things, "Coin", _coin, Color.white);
                        coin.SetNativeSize();
                        coin.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        Vector2 at = new Vector2(Center + ox[i], Ground - _chest.rect.height * K - oy[i]);
                        At(coin.rectTransform, at.x, at.y, new Vector2(0.5f, 0.5f));
                        _flights.Add(new Flight { Rect = coin.rectTransform, From = Anchored(at), To = Anchored(gold), Start = FlySeconds + i * 0.03f, Seconds = FlySeconds });
                    }
                    _landed = () => FloatText($"<color={BuildingTooltip.NumberColor}>+{choice.Gold}</color>", Center + 150f, Ground - _chest.rect.height * K - 130f);
                    break;
                }
            }
        }

        // 판을 닫고 지도의 그 땅을 깜박인다
        private void BeginFlash()
        {
            _step = Step.Flash;
            _clock = 0f;
            _emptyFloor = -1;
            BuildingTooltip.Hide(_hover != null ? _hover.Target : null);
            _motion.Close(() =>
            {
                Show(false);
                _dim.gameObject.SetActive(false);
            });
            _skip.gameObject.SetActive(true);
            _block.gameObject.SetActive(false);
            // 국가 위기는 땅이 없다 — 깜박임 없이 닫힘이 끝나면 Finish
            if (_offer != null)
                _map.Flash(_offer.Region, FlashSeconds);
        }

        private void Skip()
        {
            if (_step == Step.Coalition)
            {
                CloseCoalitions();
                return;
            }
            // 소식 결과 연출 — 건너뛰고 다음(외교 · 닫힘)
            if (_step == Step.News && _newsPlay)
            {
                _newsEnd();
                return;
            }
            if (_step == Step.Result)
                BeginFlash();
            if (_step == Step.Flash)
                Finish();
        }

        private void Finish()
        {
            _motion.Complete();
            if (_offer != null)
                _map.Flash(_offer.Region, 0f);
            _step = Step.Closed;
            _skip.gameObject.SetActive(false);
            // 국가 위기 · 이긴 전투의 결과는 턴 흐름을 잇지 않는다(요약 알림이 그대로 있다)
            bool quiet = _crisis != null || _aftermath;
            Clear();
            if (BuildingTooltip.Dock == _dock)
                BuildingTooltip.Dock = _previousDock;
            _offer = null;
            _crisis = null;
            if (!quiet)
                Finished?.Invoke();
        }

        private void Update()
        {
            if (_step == Step.Closed)
                return;
            // 한 프레임이 길어도 움직임이 건너뛰지 않게 묶는다
            _clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            if (_crisis != null && _step == Step.Choosing)
                Crowd();
            if (_step == Step.End)
                EndScene();
            if (_aftermath)
                AftermathScene();
            if (_step == Step.News)
                NewsStage();
            foreach (Flight flight in _flights)
            {
                if (flight.Follow != null)
                    flight.To = flight.Follow();
                float p = Mathf.Clamp01((_clock - flight.Start) / flight.Seconds);
                if (flight.Walk)
                    flight.Rect.anchoredPosition = flight.From + UnitFigure.Step((flight.To.x - flight.From.x) * p, flight.Scale > 0 ? flight.Scale : K, p > 0f && p < 1f);
                else
                {
                    float eased = 1f - (1f - p) * (1f - p) * (1f - p);
                    flight.Rect.anchoredPosition = Vector2.Lerp(flight.From, flight.To, eased);
                }
                if (p >= 1f && flight.Landed != null)
                {
                    flight.Landed();
                    flight.Landed = null;
                }
            }
            foreach (var (rect, start) in _floats)
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y) + new Vector2(0f, FloatRise * Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) / HoldSeconds);
            if (_step == Step.Result)
            {
                if (!_landedDone && _clock >= FlySeconds)
                {
                    _landedDone = true;
                    _landed?.Invoke();
                }
                if (_clock >= FlySeconds + HoldSeconds)
                    BeginFlash();
            }
            else if (_step == Step.Flash && !(_crisis != null || _aftermath ? _motion.Closing : _map.Flashing))
                Finish();
        }

        // 탑 기둥 층(대장간)은 매 프레임 패널이 다시 그린다 — 그 뒤에 고를 수 있는 층의 창틀 · 설명 창 · 꺾쇠를 덮어 그린다
        private void LateUpdate()
        {
            if (_step == Step.Closed)
                return;
            if (_emptyFloor >= 0)
            {
                SlotView slot = _panel.FloorAt(_emptyFloor);
                if (slot != null)
                    slot.Show(null, null, false, false);
            }
            if (_step == Step.News)
            {
                NewsLate();
                return;
            }
            if (_step != Step.Choosing)
                return;
            foreach (Thing thing in _list)
                if (thing.Frame != null)
                    thing.Frame.color = thing == _hover && Clickable(thing) ? Color.white : SlotView.Highlight(thing.FrameColor, Clickable(thing) || thing.Side);
            if (_hover != null && _hover.Side)
                Tip(_hover);
            if (_ghost != null)
            {
                bool ghost = _hover != null && _hover.Building != null;
                if (_ghost.gameObject.activeSelf != ghost)
                    _ghost.gameObject.SetActive(ghost);
                if (ghost && _ghostIcon.sprite != _hover.Building.Icon)
                {
                    _ghost.Show(_hover.Building);
                    _ghostIcon.sprite = _hover.Building.Icon;
                    _ghostIcon.SetNativeSize();
                }
            }
            bool caret = _hover != null && Clickable(_hover);
            _caretImage.gameObject.SetActive(caret);
            if (!caret)
                return;
            // 꺾쇠가 4px 밖에서 제자리로 붙는다(2프레임)
            float drop = Time.unscaledTime - _hoverAt < CaretSeconds ? CaretDrop : 0f;
            Sprite sprite = _hover.Side ? _caretLeft : _caret;
            if (_caretImage.sprite != sprite)
                _caretImage.sprite = sprite;
            _caretImage.rectTransform.sizeDelta = sprite.rect.size * K;
            if (_hover.Side)
            {
                // 왼쪽 탑 층 — 층 오른쪽에서 층을 가리킨다
                Vector2 middle = Middle(_hover.Visual);
                At(_caretImage.rectTransform, middle.x + ((RectTransform)_hover.Visual).rect.width * 0.5f + 8f + drop, middle.y, new Vector2(0f, 0.5f));
            }
            else
                At(_caretImage.rectTransform, _hover.Caret.x, _hover.Caret.y - drop, new Vector2(0.5f, 0f));
        }

        // ── 그리기 도우미(자리는 화면 위 왼쪽 기준 px · 아래가 +)
        private void Clear()
        {
            foreach (Transform child in _things)
                Destroy(child.gameObject);
            foreach (Transform child in _backdrop)
                Destroy(child.gameObject);
            foreach (EventVisitTarget target in _slotTargets)
                if (target != null)
                {
                    BuildingTooltip.Hide(target);
                    Destroy(target);
                }
            _slotTargets.Clear();
            _ghost = null;
            _list.Clear();
            _flights.Clear();
            _floats.Clear();
            _crowd.Clear();
            _cheer.Clear();
            _leavers.Clear();
            _dust.Clear();
            _turning.Clear();
            _retreat.Clear();
            _actors.Clear();
            _flames.Clear();
            _cues.Clear();
            _newsGroup.Clear();
            _newsLoads.Clear();
            _anims.Clear();
            _puffs.Clear();
            _swarm.Clear();
            _scatterAt = -1f;
            _aftermath = false;
            _crown = _castle = _endFigure = null;
            _sunk = _drained = _turned = 0;
            _hover = null;
            _sheet.anchoredPosition = Vector2.zero;
        }

        // 그 땅 지형의 뒷배경 겹(전투 맵 · 상점 무대와 같은 그림, 그림 1px = 화면 1px) — 판 안을 어둡게 채운다
        private void Backdrop(MapDefinition terrain, int seed, float darkest = 0.2f)
        {
            var random = new System.Random(seed);
            float width = PanelWidth - 8f;
            foreach (MapDefinition.BackdropLayer layer in terrain.Backdrop)
            {
                if (layer.Sprites.Length == 0)
                    continue;
                float brightness = Mathf.Min(layer.Brightness, darkest);
                for (float x = (float)random.NextDouble() * 40f; x < width + 100f; x += Mathf.Lerp(layer.Spacing.x, layer.Spacing.y, (float)random.NextDouble()) * 8f)
                {
                    Sprite sprite = layer.Sprites[random.Next(layer.Sprites.Length)];
                    Image image = NewImage(_backdrop, "Hill", sprite, new Color(brightness, brightness, brightness));
                    image.rectTransform.sizeDelta = sprite.rect.size;
                    image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
                    image.rectTransform.pivot = new Vector2(0.5f, 0f);
                    image.rectTransform.anchoredPosition = new Vector2(x, Mathf.Lerp(layer.Bottom.x, layer.Bottom.y, (float)random.NextDouble()) * 8f);
                    if (random.Next(2) == 0)
                        image.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                }
            }
        }

        // 소품 — 아래 가운데를 (x, bottom)에, 그림 1px = k px
        private Image Prop(Sprite sprite, float x, float bottom, int k = K)
        {
            Image image = NewImage(_things, sprite.name, sprite, Color.white);
            image.rectTransform.sizeDelta = sprite.rect.size * k;
            At(image.rectTransform, x, bottom, new Vector2(0.5f, 0f));
            return image;
        }

        // 배경 소품(누르지 않는 것) — 그림이 비어 있으면 그리지 않는다
        private void Scenery(Sprite sprite, float x, float bottom)
        {
            if (sprite != null)
                Prop(sprite, x, bottom);
        }

        // 유닛 그림 — 발 가운데를 (x, bottom)에. left = 왼쪽을 본다. 돌려주는 판의 크기 = 그림 크기, 그림 1px = k px
        private RectTransform Figure(UnitDefinition unit, float x, float bottom, bool left, Color paint, int k = K)
        {
            RectTransform holder = NewRect(_things, unit != null ? unit.DisplayName : "Unit");
            At(holder, x, bottom, Vector2.zero);
            if (unit == null)
                return holder;
            holder.sizeDelta = UnitFigure.Draw(unit.PrefabFor(Team.Ally), holder, k, paint);
            if (left)
                holder.localScale = new Vector3(-1f, 1f, 1f);
            PlaceFigure(holder, x, bottom, left);
            return holder;
        }

        // Figure로 그린 유닛을 옮긴다 — 발 가운데를 (x, bottom)에(왼쪽을 보면 판이 뒤집혀 있어 오른쪽 끝이 기준점)
        private static void PlaceFigure(RectTransform holder, float x, float bottom, bool left)
            => At(holder, x + (left ? 0.5f : -0.5f) * holder.sizeDelta.x, bottom, Vector2.zero);

        // 탑 층 조각(UI_TowerFloor + 기물 아이콘 ×0.6) — 아래 가운데를 (x, bottom)에. window = 창틀(등급 색은 부르는 쪽이)
        private FloorLook Floor(BuildingDefinition building, float x, float bottom, out Image window)
        {
            FloorLook floor = Instantiate(_floorPart, _things);
            floor.name = "Floor";
            floor.Show(building);
            At((RectTransform)floor.transform, x, bottom, new Vector2(0.5f, 0f));
            Image icon = NewImage((RectTransform)floor.transform, "Icon", building != null ? building.Icon : null, Color.white);
            icon.SetNativeSize();
            icon.rectTransform.localScale = Vector3.one * FloorIcon;
            window = floor.transform.Find("Window").GetComponent<Image>();
            window.transform.SetAsLastSibling();
            return floor;
        }

        // 업그레이드 칸(UI_UpgradeCell_Hover, 「×N」 끔) — 아래 가운데를 (x, bottom)에. 알파를 바꿀 수 있게 CanvasGroup
        private Image Cell(Sprite icon, Color frame, float x, float bottom)
        {
            Image cell = Instantiate(_cellPart, _things);
            cell.name = "Cell";
            cell.color = frame;
            cell.transform.Find("Count").gameObject.SetActive(false);
            Transform inner = cell.transform.Find("Icon");
            inner.GetComponent<Image>().sprite = icon;
            // 칸 아이콘의 제 툴팁은 끈다 — 장면의 설명 창(고정 자리)이 대신 뜬다
            var own = inner.GetComponent<UpgradeIcon>();
            if (own != null)
                own.enabled = false;
            inner.GetComponent<Image>().raycastTarget = false;
            cell.gameObject.AddComponent<CanvasGroup>();
            At(cell.rectTransform, x, bottom, new Vector2(0.5f, 0f));
            return cell;
        }

        // 누름 받이 — rect에 투명한 판을 깔아 고를 수 있게
        private EventVisitTarget Hit(RectTransform rect)
        {
            Image hit = rect.GetComponent<Image>();
            if (hit == null)
            {
                hit = rect.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
            }
            hit.raycastTarget = true;
            return rect.gameObject.AddComponent<EventVisitTarget>();
        }

        private void Spark(float x, float y)
        {
            Image spark = NewImage(_things, "Sparks", _sparks, Color.white);
            spark.rectTransform.sizeDelta = _sparks.rect.size * K;
            At(spark.rectTransform, x, y, new Vector2(0.5f, 0.5f));
        }

        // 떠오르는 「+N」(도트 24)
        private void FloatText(string text, float x, float y)
        {
            Text label = NewText(_things, "Float", _dotFont, 24, 200f);
            label.text = text;
            At(label.rectTransform, x, y, new Vector2(0.5f, 0.5f));
            _floats.Add((label.rectTransform, _clock));
        }

        // 스탯 아이콘(그 스탯 색) + 「+N」
        private void FloatStat(InlineIcons.Kind kind, Color color, string text, float x, float y)
        {
            RectTransform row = NewRect(_things, "FloatStat");
            At(row, x, y, new Vector2(0f, 0.5f));
            Sprite sprite = RunState.Config.World.InlineIcon(kind);
            Image icon = NewImage(row, "Icon", sprite, color);
            if (sprite != null)
                icon.SetNativeSize();
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text label = NewText(row, "Value", _dotFont, 24, 120f);
            label.alignment = TextAnchor.MiddleLeft;
            label.text = text;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(24f, 0f);
            _floats.Add((row, _clock));
        }

        // rect를 지금 자리 from에서 to로(화면 위 왼쪽 기준) — walk = 유닛 걸음(가로만 · 1px 들썩임)
        private void Fly(RectTransform rect, Vector2 from, Vector2 to, bool walk, bool anchoredAlready = false)
        {
            rect.SetParent(_things, true);
            if (anchoredAlready)
            {
                _flights.Add(new Flight { Rect = rect, From = from, To = to, Start = 0f, Seconds = FlySeconds, Walk = walk });
                return;
            }
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Anchored(from);
            _flights.Add(new Flight { Rect = rect, From = Anchored(from), To = Anchored(to), Start = 0f, Seconds = FlySeconds, Walk = walk });
        }

        // 화면 위 왼쪽 기준 자리 → 위 왼쪽 앵커의 anchoredPosition
        private static Vector2 Anchored(Vector2 at) => new Vector2(at.x, -at.y);

        // 월드 좌표 → 화면 위 왼쪽 기준 px
        private Vector2 Local(Vector3 world)
        {
            Vector3 local = _root.InverseTransformPoint(world);
            Rect area = _root.rect;
            return new Vector2(local.x - area.xMin, area.yMax - local.y);
        }

        // rect 한가운데(화면 위 왼쪽 기준 px)
        private Vector2 Middle(Transform rect) => Local(((RectTransform)rect).TransformPoint(((RectTransform)rect).rect.center));

        private static void At(RectTransform rect, float x, float y, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = sprite != null;
            return image;
        }

        private static Text NewText(Transform parent, string name, Font font, int size, float width)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.sizeDelta = new Vector2(width, size + 8f);
            return text;
        }

        // 글자 색(§5-2-9) — 숫자 노랑 · 달라지는 값 초록 · 줄어든 값 빨강
        private static string Number(int value) => $"<color={BuildingTooltip.NumberColor}>{value}</color>";
        private static string Bonus(string text) => $"<color={BuildingTooltip.BonusColor}>{text}</color>";
        private static string Enemy(string text) => $"<color={BuildingTooltip.EnemyColor}>{text}</color>";

        // 지금 글 → 오른 글: 바뀐 숫자만 초록, 그대로인 숫자는 노랑(RANK UP 미리보기와 같은 규칙)
        private static readonly System.Text.RegularExpressions.Regex Numbers = new System.Text.RegularExpressions.Regex(@"[+\-×]?\d+(\.\d+)?%?");

        private static string Changed(string before, string after)
        {
            var olds = Numbers.Matches(before);
            int k = 0;
            return Numbers.Replace(after, m =>
            {
                string old = k < olds.Count ? olds[k].Value : "";
                k++;
                return old == m.Value ? $"<color={BuildingTooltip.NumberColor}>{m.Value}</color>" : Bonus(m.Value);
            });
        }
    }
}

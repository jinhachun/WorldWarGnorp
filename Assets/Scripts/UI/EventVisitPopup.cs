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
    public class EventVisitPopup : MonoBehaviour
    {
        // 그림 1px = 화면 K px(상점 짐꾼과 같다)
        private const int K = 6;
        // 지도 자리(화면 위 왼쪽 기준 px)와 판 크기 · 판 아래 끝에서 땅 선까지
        private const float MapLeft = 420f, MapTop = 67f, MapWidth = 1486f, MapHeight = 996f;
        private const float PanelWidth = 890f, PanelHeight = 600f, GroundUp = 110f;
        // 둘레 지도를 어둡게(사용자 10/25 그대로)
        private const float DimAlpha = 0.5f;
        // 움직임(스타일 가이드 §5) — 판 열림 16px · 0.12초 계단 3 / 꺾쇠 4px · 0.07초 / 결과 날아감 0.3초 · 「+N」을 보여 주는 시간 / 지도 깜박임
        private const float OpenDrop = 16f, OpenSeconds = 0.12f;
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

        private enum Step { Closed, Choosing, Result, Flash, Coalition, News }

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
        private Image _dim, _ground;
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
        }

        // 그 이벤트 땅의 방문 장면을 연다 — 방문할 수 없으면 false
        public bool Open(int region)
        {
            EventOffer offer = RunState.BeginVisit(region);
            if (offer == null)
                return false;
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
            Backdrop(RunState.World.Map.TerrainOf(region), region);
            Build();
            return true;
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
            _offer = null;
            _step = Step.Coalition;
            _clock = 0f;
            _hover = null;
            Clear();
            _dim.gameObject.SetActive(true);
            Show(true);
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
                        Start = OpenSeconds + j * JoinStagger, Seconds = JoinSeconds, Walk = true, Scale = small,
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
        }

        private void CloseCoalitions()
        {
            _step = Step.Closed;
            _skip.gameObject.SetActive(false);
            Clear();
            Show(false);
            _dim.gameObject.SetActive(false);
            _ground.gameObject.SetActive(true);
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
            _boxes.Clear();
            _boxHover = null;
            _pickChoice = -1;
            _newsResult = false;
            _caretImage.gameObject.SetActive(false);
            PlaceSheet(Top, PanelHeight, Ground);
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
            SetLine($"<color={BuildingTooltip.NoteColor}>{news.Definition.Story}</color>");
            LordFigure(state.Player.Id, RunState.ColorOf(state.Player), Center, Ground - BodyHeight(state.Player.Id) * K * 0.5f, K, false);
            for (int i = 0; i < 2; i++)
                AddBox(i, null, BuildingTooltip.ColorNumbers(news.Definition.Choice(i).Describe()), RunState.CanChooseNews(i));
            LayoutBoxes();
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
            LordFigure(offer.Lord.Id, RunState.ColorOf(offer.Lord), Center - 120f, Ground - BodyHeight(offer.Lord.Id) * K * 0.5f, K, false);
            LordFigure(state.Player.Id, RunState.ColorOf(state.Player), Center + 120f, Ground - BodyHeight(state.Player.Id) * K * 0.5f, K, true);
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
            if (_step != Step.News || !box.Enabled || _pickChoice >= 0 || _newsResult)
                return;
            if (_diplomacyShown)
            {
                RunState.AnswerDiplomacy(box.Choice == 0);
                NextNews();
                return;
            }
            if (RunState.CurrentNews.NeedsBuilding(box.Choice))
            {
                BeginPick(box.Choice);
                return;
            }
            NewsAction action = RunState.CurrentNews.Definition.Choice(box.Choice).Action;
            if (RunState.ChooseNews(box.Choice))
                AfterNews(action, -1);
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
            NewsAction action = RunState.CurrentNews.Definition.Choice(choice).Action;
            if (!RunState.ChooseNews(choice, floor))
                return;
            EndPick();
            AfterNews(action, floor);
        }

        // 고른 뒤 — 연금술은 결과 한 줄을 보여 주고 「닫기」, 나머지는 바로 다음(외교 · 닫힘)
        private void AfterNews(NewsAction action, int floor)
        {
            if (action != NewsAction.Alchemy)
            {
                NextNews();
                return;
            }
            _newsResult = true;
            foreach (NewsBox box in _boxes)
                box.Rect.gameObject.SetActive(false);
            _caretImage.gameObject.SetActive(false);
            OwnedBuilding owned = floor >= 0 ? RunState.Field[floor] : null;
            string name = owned != null ? owned.Definition.DisplayName : "기물";
            SetLine(RunState.LastAlchemySucceeded ? $"연금술에 성공해, {name} 기물이 신화가 되었습니다." : $"<color={BuildingTooltip.NoteColor}>연금술에 실패했습니다.</color>");
            SetAction("닫기");
        }

        private void NextNews()
        {
            EndPick();
            if (RunState.CurrentNews == null && RunState.CurrentDiplomacy != null)
            {
                BuildDiplomacy();
                return;
            }
            if (RunState.CurrentNews != null)
                return;
            _step = Step.Closed;
            _caretImage.gameObject.SetActive(false);
            Clear();
            _boxes.Clear();
            Show(false);
            _dim.gameObject.SetActive(false);
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
            else if (_boxHover != null && _boxHover.Enabled && !_newsResult)
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
            if (!RunState.ChooseVisit(thing.Choice))
                return;
            Exit(thing);
            BeginResult(thing);
        }

        // 판 오른쪽 아래 버튼 — 「받기」(지형 성소) · 「바치지 않기」 · 「닫기」
        private void Act()
        {
            if (_step == Step.News)
            {
                NewsAct();
                return;
            }
            if (_step != Step.Choosing)
                return;
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
            Show(false);
            _dim.gameObject.SetActive(false);
            _skip.gameObject.SetActive(true);
            _map.Flash(_offer.Region, FlashSeconds);
        }

        private void Skip()
        {
            if (_step == Step.Coalition)
            {
                CloseCoalitions();
                return;
            }
            if (_step == Step.Result)
                BeginFlash();
            if (_step == Step.Flash)
                Finish();
        }

        private void Finish()
        {
            _map.Flash(_offer.Region, 0f);
            _step = Step.Closed;
            _skip.gameObject.SetActive(false);
            Clear();
            if (BuildingTooltip.Dock == _dock)
                BuildingTooltip.Dock = _previousDock;
            _offer = null;
            Finished?.Invoke();
        }

        private void Update()
        {
            if (_step == Step.Closed)
                return;
            // 한 프레임이 길어도 움직임이 건너뛰지 않게 묶는다
            _clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            if (_step == Step.Choosing || _step == Step.Coalition || _step == Step.News)
            {
                // 판 열림 — 위에서 16px 떨어지며(계단 3)
                int steps = Mathf.Min(3, Mathf.FloorToInt(_clock / (OpenSeconds / 3f)));
                _sheet.anchoredPosition = new Vector2(0f, OpenDrop * (3 - steps) / 3f);
            }
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
            else if (_step == Step.Flash && !_map.Flashing)
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
            _hover = null;
            _sheet.anchoredPosition = Vector2.zero;
        }

        // 그 땅 지형의 뒷배경 겹(전투 맵 · 상점 무대와 같은 그림, 그림 1px = 화면 1px) — 판 안을 어둡게 채운다
        private void Backdrop(MapDefinition terrain, int seed)
        {
            var random = new System.Random(seed);
            float width = PanelWidth - 8f;
            foreach (MapDefinition.BackdropLayer layer in terrain.Backdrop)
            {
                if (layer.Sprites.Length == 0)
                    continue;
                float brightness = Mathf.Min(layer.Brightness, 0.2f);
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

        // 소품 — 아래 가운데를 (x, bottom)에, 그림 1px = K px
        private Image Prop(Sprite sprite, float x, float bottom)
        {
            Image image = NewImage(_things, sprite.name, sprite, Color.white);
            image.rectTransform.sizeDelta = sprite.rect.size * K;
            At(image.rectTransform, x, bottom, new Vector2(0.5f, 0f));
            return image;
        }

        // 배경 소품(누르지 않는 것) — 그림이 비어 있으면 그리지 않는다
        private void Scenery(Sprite sprite, float x, float bottom)
        {
            if (sprite != null)
                Prop(sprite, x, bottom);
        }

        // 유닛 그림 — 발 가운데를 (x, bottom)에. left = 왼쪽을 본다. 돌려주는 판의 크기 = 그림 크기
        private RectTransform Figure(UnitDefinition unit, float x, float bottom, bool left, Color paint)
        {
            RectTransform holder = NewRect(_things, unit != null ? unit.DisplayName : "Unit");
            At(holder, x, bottom, Vector2.zero);
            if (unit == null)
                return holder;
            Vector2 size = UnitFigure.Draw(unit.PrefabFor(Team.Ally), holder, K, paint);
            holder.sizeDelta = size;
            if (left)
            {
                holder.localScale = new Vector3(-1f, 1f, 1f);
                At(holder, x + size.x * 0.5f, bottom, Vector2.zero);
            }
            else
                At(holder, x - size.x * 0.5f, bottom, Vector2.zero);
            return holder;
        }

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

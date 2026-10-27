using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 월드 지도 — 땅 번호 지도(WorldMapDefinition.ReadCells)의 칸을 칠해 정수배로 키워 보여 준다(RawImage · Point).
    // 남의 땅 속은 그 땅의 지형색(MapDefinition.MapColor), 안쪽 둘레는 영주 색 띠, 영주 사이 국경 · 해안선은 검은 선(사용자 10/26 「지형색 바탕 + 영주 색 띠」).
    // 내 땅만 내 색으로 밝게 채워 굵은 흰 테두리를 두른다(지도에서 가장 먼저 보여야 한다 — 사용자 10/11 · 10/26). 같은 영주의 땅 사이는 옅은 선. 침략할 수 있는 땅에는 내 땅에서 그 땅으로 화살표 하나
    // (국경을 가장 길게 맞댄 내 땅에서, 여덟 방향 도트 도장) — _hatch를 켜면 그 땅에 그 영주 색 빗금도. 고른 땅은 노란 테두리, 가리킨 땅은 조금 밝게.
    // 그림은 셰이더(Shaders/WorldMap)가 그린다(사용자 10/27 「셰이더로」) — 안 바뀌는 것(땅 번호 · 이웃 땅 · 해안까지 거리 · 기복도 · 장식)은 Build에서 텍스처로 한 번 굽고,
    // 판도 · 침략 가능 · 이벤트 상태는 땅 수만 한 표(_lands), 고른 땅 · 가리킨 땅 · 내 영주는 머티리얼 값, 화살표는 바뀔 때만 따로 그리는 텍스처.
    // 땅마다 지역 특수 표시 · 이벤트 칸 · 내 땅 왕관은 구운 표시 자리(AnchorOf)에 한 번 놓는다(지형 아이콘은 띄우지 않는다 — 땅 카드 UI/LandHover, 사용자 10/26).
    // 누름 · 가리킴은 화면 좌표를 칸으로 바꿔 땅 번호를 읽는다 — 무엇을 할지는 WorldManager가 정한다
    [RequireComponent(typeof(RawImage))]
    public class WorldMapView : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler, IScrollHandler, IDragHandler
    {
        public const int None = -1;

        // 채움 밝기 — 남의 땅 · 가리킨 남의 땅 · 침략할 수 있는 땅 · 가리킨 그 땅은 지형색 × 이 값, 내 땅 · 가리킨 내 땅은 내 색 × 이 값
        // 침략할 수 있는 땅만 지형색 그대로, 나머지는 조금 눌러 둔다(사용자 10/20 U4 — 내 땅 · 갈 수 있는 땅이 묻히지 않게)
        private const float Fill = 0.9f;   // 지형색이 Resurrect 64 팔레트 그대로 보이게 살짝만 어둡게(사용자 10/26 「칙칙하다」) — 침략할 수 있는 땅(OpenFill)과는 갈린다
        private const float HoverFill = 1f;
        private const float OpenHoverFill = 1.2f;
        private const float MineFill = 0.8f;
        // 같은 영주의 땅 사이 선 = 채움을 반투명 검정으로 누른 1칸(영주 색 띠는 없다 — 영토는 한 덩어리로, 사용자 10/26)
        private const float InnerLine = 0.55f;
        // 남의 영토 바깥 둘레의 영주 색 띠 밝기(영주 색 × 이 값) — 띠 두께(4칸, 사용자 10/26)는 셰이더
        private const float BandFill = 0.8f;
        // 내 땅 국경 강조(사용자 10/15 「내 땅이 잘 안 보인다」 — 4X식 국경) — 흰 테두리 안쪽 띠의 흰색 섞는 비율(띠 두께는 셰이더)
        private const float GlowWhite = 0.45f;
        // 이벤트 땅(상태 없음) 빗금 밝기(땅 색 × 이 값) · 가리켰을 때
        private const float EventHatch = 0.45f, EventHatchHover = 0.8f;
        // 이벤트 땅 밝기 — 닫힘 · 깜박임 · 방문 가능(가리킴) · 방문 가능 · 열림(가리킴)
        private const float EventClosedLight = 0.5f, EventLitLight = 1.45f, EventVisitableHoverLight = 1.35f, EventVisitableLight = 1.2f, EventHoverLight = 1.15f;
        // 지형 장식 — 내 땅(내 색)은 살짝 밝게, 남의 땅(지형색)은 살짝 어둡게. 지형색만으로 갈리지 않는 눈(적록 색맹)에도 지형이 읽히게 남긴다
        private const float DecorBrightness = 0.22f;
        private const float DecorShade = 0.3f;
        // 선 · 띠 두께 · 선 색은 셰이더(Shaders/WorldMap) 위쪽에 있다
        // 주인에 따라 달라지는 선이 보는 거리(칸) — 셰이더의 가장 넓은 것(영주 색 띠 · 내 땅 번짐 4칸)까지 이웃 땅을 굽는다 · 칸마다 굽는 이웃 땅 수
        private const int NearReach = 4;
        private const int NearSlots = 4;
        // 침략 화살표 — 손으로 찍은 도장 둘(오른쪽 · 오른쪽 아래)을 90도씩 돌려 여덟 방향으로 쓴다(임의 각도로 그으면 4px 격자에서 계단이 깨진다).
        // # = 흰 몸, 검은 테두리는 몸을 한 칸 고르게 두른다
        private static readonly string[] ArrowEast =
        {
            ".......#....",
            ".......##...",
            ".......###..",
            "###########.",
            "############",
            "###########.",
            ".......###..",
            ".......##...",
            ".......#....",
        };
        private static readonly string[] ArrowSouthEast =
        {
            "##........",
            "###.......",
            ".###......",
            "..###.....",
            "...###...#",
            "....###.##",
            ".....#####",
            "......####",
            ".....#####",
            "....######",
        };
        // 화살표 가운데가 국경에서 그 땅 안쪽으로 들어간 거리(칸) — 표시 자리(아이콘)와 Clearance칸을 못 띄우면 내 땅 쪽으로 물러난다(적어도 MinPoke칸은 넘어간다)
        private const float ArrowHalf = 6f;
        private const float ArrowAdvance = 2.5f;
        private const float ArrowClearance = 11f;
        private const float ArrowMinPoke = 2f;
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 SeaColor = new Color32(0, 0, 0, 255);

        // 표시의 자리(표시 자리 기준, 화면 px): 지형 아이콘 뒤 검은 판의 한 변 · 아이콘 높이(땅 번호는 지웠다 — 사용자 10/15)
        private const float IconBack = 36f;
        private const float IconY = 12f;
        // 표시 자리의 px 값들을 잰 기준 — 지도 한 칸 = 화면 4px
        private const float CellPixels = 4f;

        [Tooltip("영주 이름 — 도트 폰트(WorldLords)")]
        [SerializeField] private Font _dotFont;
        [Tooltip("침략할 수 있는 땅에 화살표와 함께 그 영주 색 빗금도 친다")]
        [SerializeField] private bool _hatch;
        [Tooltip("지도 위를 돌아다니는 영주 그림 — 유닛 몸 그림 한 장(영주 색으로 칠한다, 왼쪽 패널의 영주 그림과 같은 것)")]
        [SerializeField] private Sprite _lordSprite;
        [Tooltip("영주 그림이 드는 깃발 — 흰 그림(영주 색을 입힌다)")]
        [SerializeField] private Sprite _lordFlag;
        [Tooltip("내 땅 표시 — 내 땅마다 지형 아이콘 위에 늘 뜬다(사용자 10/15 「내 영토가 눈에 안 띈다」), 원본 크기")]
        [SerializeField] private Sprite _crown;
        [Tooltip("지역 특수 업그레이드 표시 칸 — 부품 프리팹 UI_UpgradeCell_Hover(「×N」은 끈다)")]
        [SerializeField] private Image _upgradeCell;
        [Tooltip("미지의 땅(방문 전 이벤트 땅) 표시 — 「?」 아이콘 32(UI/UnknownLand)")]
        [SerializeField] private Sprite _unknownIcon;
        [Tooltip("지도 그림 셰이더 — Shaders/WorldMap")]
        [SerializeField] private Shader _shader;

        private RawImage _image;
        private Material _material;
        // 구운 지도(칸 · 이웃 땅 번호 · 그 거리) · 땅마다의 표 · 화살표
        private Texture2D _cellTexture, _nearIds, _nearDist, _lands, _arrows, _ramp, _lut;
        private Color32[] _landPixels, _arrowPixels;
        // 화살표가 칠한 칸(텍스처 순서) — 다시 그릴 때 이것만 지운다
        private readonly List<int> _arrowPlotted = new List<int>();
        // 땅마다 화살표를 그렸나 — 판도 · 침략 가능 · 방문 가능이 바뀌어도 이것이 같으면 다시 그리지 않는다
        private bool[] _arrowWanted;
        private int[] _cells;
        private int _width, _height;
        private Vector2Int[] _anchors;
        // 땅마다 — 지형 아이콘의 검은 판 · 지역 특수 업그레이드 표시(외곽선 칸 · 아이콘) · 지금 그려 둔 특수 업그레이드(없으면 null)
        private Image[] _specialFrames, _crowns;
        private UpgradeIcon[] _specialIcons;
        private UpgradeDefinition[] _specials;
        // 이벤트 땅(아웃게임기획 §3-1 · 시안 A안 10/25) — 땅마다 이벤트 칸(지역 특수 칸과 같은 부품 · 같은 자리, 흰 테두리) · 그 아이콘 · 지금 그린 상태(EventState)
        private Image[] _eventCells;
        private UpgradeIcon[] _eventIcons;
        private byte[] _events, _eventMarks;
        // 이벤트 칸 툴팁 글을 마지막으로 채운 턴(보물 창고 골드처럼 턴마다 값이 바뀐다)
        private int _eventTurn = -1;
        // 깜박이는 이벤트 땅(방문을 마친 땅 — Flash) · 끝나는 때(unscaled 시각)
        private int _flash = None;
        private float _flashUntil;
        // 왕관과 지형 아이콘 판 사이(px)
        private const float CrownGap = 2f;
        // [작은 땅 번호 × 땅 수 + 큰 땅 번호] = 두 땅이 맞댄 국경의 칸들 — 화살표가 여기서 나온다
        private readonly Dictionary<int, List<Vector2Int>> _borders = new Dictionary<int, List<Vector2Int>>();

        // 마지막으로 칠한 것 — 달라졌을 때만 다시 칠한다
        private int[] _owner;
        private Color32[] _colors;
        // 땅마다 지형색(Build에서 한 번)
        private Color32[] _terrain;
        private bool[] _invadable;
        private int _selected = None;
        private int _hovered = None;
        private int _mine = None;
        private bool _painted;
        // 마지막으로 셰이더에 넘긴 물결 틱(WaveTick 단위)
        private int _waveTick = -1;

        // 가리키고 있는 땅(없으면 None)
        public int Hovered { get; private set; } = None;
        // 마지막으로 칠한 화살표 수
        public int Arrows { get; private set; }
        // 땅을 눌렀다(바다 · 지도 밖이면 None)
        public event System.Action<int> Clicked;
        // 지도 위를 돌아다니는 영주들 — 표시(아이콘) 위에 그려진다
        public WorldLords Lords { get; private set; }

        public void Build(WorldMapDefinition map)
        {
            _image = GetComponent<RawImage>();
            _home = ((RectTransform)transform).anchoredPosition;
            _cells = map.ReadCells(out _width, out _height);
            int regions = map.RegionCount;
            _owner = new int[regions];
            _colors = new Color32[regions];
            _terrain = new Color32[regions];
            for (int region = 0; region < regions; region++)
                _terrain[region] = map.TerrainOf(region).MapColor;
            _invadable = new bool[regions];
            _anchors = new Vector2Int[regions];
            _specialFrames = new Image[regions];
            _crowns = new Image[regions];
            _invadeMarks = new Image[regions];
            _invadeHome = new float[regions];
            _specialIcons = new UpgradeIcon[regions];
            _specials = new UpgradeDefinition[regions];
            _eventCells = new Image[regions];
            _eventIcons = new UpgradeIcon[regions];
            _events = new byte[regions];
            _eventMarks = new byte[regions];
            // 맞댄 국경 — 오른쪽 · 아래 칸과 땅이 다른 칸
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    int a = _cells[y * _width + x];
                    if (a < 0)
                        continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int b = k == 0 ? At(_cells, _width, _height, x + 1, y) : At(_cells, _width, _height, x, y + 1);
                        if (b < 0 || b == a)
                            continue;
                        int key = Mathf.Min(a, b) * regions + Mathf.Max(a, b);
                        if (!_borders.TryGetValue(key, out List<Vector2Int> cells))
                            _borders[key] = cells = new List<Vector2Int>();
                        cells.Add(new Vector2Int(x, y));
                    }
                }

            var rect = (RectTransform)transform;
            // 한 칸 = 지도 영역에 다 들어가는 가장 큰 정수 px(도트 유지) — 375×249칸이면 4px, 500×249칸이면 3px(임시 — 큰 지도 조작은 ROADMAP 2-5 5번). 그림을 그 크기로 왼쪽 위에 둔다
            float scale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(rect.rect.width / _width, rect.rect.height / _height)));
            rect.sizeDelta = new Vector2(_width, _height) * scale;
            _marks = new RectInt[regions];
            for (int region = 0; region < regions; region++)
            {
                Vector2Int anchor = _anchors[region] = map.AnchorOf(region);
                var mark = (RectTransform)new GameObject($"Land{region + 1:00}", typeof(RectTransform)).transform;
                mark.SetParent(rect, false);
                mark.anchorMin = mark.anchorMax = new Vector2(0f, 1f);
                mark.anchoredPosition = new Vector2(anchor.x * scale, -anchor.y * scale);
                mark.sizeDelta = Vector2.zero;

                // 지형 아이콘은 지도에 띄우지 않는다(사용자 10/26 「지저분하다」) — 지형은 땅에 올리면 뜨는 땅 카드(UI/LandHover)가 보여 준다.
                // 표시 자리에는 칸 하나(지역 특수 또는 이벤트)만 가운데에 선다

                // 지역 특수 업그레이드 표시 — 신화 등급 색 외곽선 칸(가진 업그레이드 줄 UI/UpgradeRow의 칸과 같은 모양) 안에 아이콘. 마우스를 올리면 이름과 효과(UI/UpgradeIcon)
                Image frame = _specialFrames[region] = Instantiate(_upgradeCell, mark);
                frame.name = "Special";
                frame.color = BuildingRarity.Mythic.FrameColor();
                frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                frame.transform.Find("Count").gameObject.SetActive(false);
                _specialIcons[region] = frame.transform.Find("Icon").GetComponent<UpgradeIcon>();
                frame.gameObject.SetActive(false);

                // 내 땅 왕관 — 표시 칸 줄 바로 위 가운데, 검은 테두리(Show가 켜고 끈다)
                Image crown = _crowns[region] = NewImage(mark, "Crown");
                crown.sprite = _crown;
                crown.SetNativeSize();
                crown.preserveAspect = true;
                crown.rectTransform.anchoredPosition = new Vector2(0f, IconY + (IconBack + crown.rectTransform.sizeDelta.y) * 0.5f + CrownGap);
                var crownOutline = crown.gameObject.AddComponent<Outline>();
                crownOutline.effectColor = Color.black;
                crownOutline.effectDistance = new Vector2(2f, 2f);
                crown.gameObject.SetActive(false);

                // 침략할 수 있는 땅 표시 — 이벤트 칸과 같은 부품(흰 테두리 칸) 안에 칼(「침략」 버튼과 같은 그림), 톡톡 튄다(Update). 지역 특수 칸이 있으면 그 위(왕관 자리 — 남의 땅이라 왕관과 안 겹친다). Show가 켜고 끈다
                Image invade = _invadeMarks[region] = Instantiate(_upgradeCell, mark);
                invade.name = "Invade";
                invade.rectTransform.anchorMin = invade.rectTransform.anchorMax = invade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                invade.transform.Find("Count").gameObject.SetActive(false);
                var sword = invade.transform.Find("Icon").GetComponent<UpgradeIcon>();
                sword.Set(RunState.Config != null ? RunState.Config.World.InvadeMark : null, "", "");
                sword.enabled = false;   // 툴팁 없음 — 땅 카드(UI/LandHover)가 보여 준다
                sword.GetComponent<Image>().raycastTarget = false;
                invade.raycastTarget = false;
                invade.gameObject.SetActive(false);

                // 표시 자리 — 칸 하나 + 왕관(칸이 하나뿐이라 옆 · 위아래로 쌓을 일이 없다). 이 자리(칸)는 장식 · 영주 그림이 비켜 간다
                float top = IconY + IconBack * 0.5f + CrownGap + crown.rectTransform.sizeDelta.y;
                _marks[region] = MarkCells(anchor, scale, IconBack * 0.5f, top, IconY - IconBack * 0.5f);
                frame.rectTransform.anchoredPosition = new Vector2(0f, IconY);

                // 이벤트 칸 — 지역 특수 칸과 같은 부품 · 같은 자리, 테두리만 흰색(이벤트 땅에만 ShowEvents가 켠다)
                Image cell = _eventCells[region] = Instantiate(_upgradeCell, mark);
                cell.name = "Event";
                cell.rectTransform.anchorMin = cell.rectTransform.anchorMax = cell.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                cell.rectTransform.anchoredPosition = frame.rectTransform.anchoredPosition;
                cell.transform.Find("Count").gameObject.SetActive(false);
                _eventIcons[region] = cell.transform.Find("Icon").GetComponent<UpgradeIcon>();
                // 이벤트 칸은 제 툴팁을 띄우지 않는다 — 땅 카드(UI/LandHover)가 미지의 땅 · 이벤트 정체를 대신 보여 준다(하나로, 사용자 10/26)
                _eventIcons[region].enabled = false;
                _eventIcons[region].GetComponent<Image>().raycastTarget = false;
                cell.gameObject.SetActive(false);
            }
            BuildDecor(map);
            Bake();
            // 살아 있는 세계(파티클) — 첫 자식 오버레이라 표시 칸 · 영주 그림 아래에 그려진다
            new GameObject("Ambience", typeof(RectTransform), typeof(RawImage), typeof(WorldAmbience)).GetComponent<WorldAmbience>().Init(transform, map, _cells, _width, _height, _owner, _events, _marks);
            Lords = gameObject.AddComponent<WorldLords>();
            Lords.Init(_cells, _width, _height, scale, _anchors, _marks, _lordSprite, _lordFlag, _dotFont);
        }

        // 표시 자리마다 차지하는 칸(장식 · 영주 그림이 비켜 간다)
        private RectInt[] _marks;

        // 표시 자리(anchor)에서 좌우 halfWidth px · 위 top px · 아래 bottom px(위가 +)를 칸으로
        private static RectInt MarkCells(Vector2Int anchor, float scale, float halfWidth, float top, float bottom)
        {
            int xMin = Mathf.FloorToInt(anchor.x - halfWidth / scale), xMax = Mathf.CeilToInt(anchor.x + halfWidth / scale);
            int yMin = Mathf.FloorToInt(anchor.y - top / scale), yMax = Mathf.CeilToInt(anchor.y - bottom / scale);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        // 지역 특수 업그레이드가 놓인 땅에만 표시가 선다 — 내가 얻었거나 사라지면(RunState.SpecialAt이 null) 없어진다
        private void ShowSpecials()
        {
            for (int region = 0; region < _specials.Length; region++)
            {
                UpgradeDefinition special = RunState.SpecialAt(region);
                if (special == _specials[region])
                    continue;
                _specials[region] = special;
                _specialFrames[region].gameObject.SetActive(special != null);
                if (special != null)
                    _specialIcons[region].Set(special.Icon, special.DisplayName, BuildingTooltip.Highlight(special.Describe(special.ValueAt(BuildingRarity.Mythic))));
            }
        }

        // 이벤트 땅의 상태(칠하기 · 표시) — 이벤트 땅이 아님 · 열림(방문 못 함) · 방문할 수 있음 · 닫힘 · 깜박임(방문을 마친 순간)
        private const byte EventNone = 0, EventOpen = 1, EventVisitable = 2, EventClosed = 3, EventLit = 4;
        // 깜박임 한 번의 길이(초)
        private const float FlashBlink = 0.1f;
        // 닫힌 이벤트 땅의 아이콘 · 칸 테두리
        private static readonly Color ClosedIcon = new Color(1f, 1f, 1f, 0.35f), ClosedCell = new Color(0.3f, 0.3f, 0.3f);

        // 방문을 마친 이벤트 땅을 seconds초 깜박인 뒤 닫힌 모양으로(UI/EventVisitPopup). 깜박이는 동안 Flashing
        public void Flash(int region, float seconds)
        {
            _flash = region;
            _flashUntil = Time.unscaledTime + seconds;
        }

        public bool Flashing => _flash != None && Time.unscaledTime < _flashUntil;

        // 내 땅 왕관 그림 — 연합 결성 팝업의 「땅 N」 앞에도 쓴다(UI/EventVisitPopup)
        public Sprite Crown => _crown;

        // 이벤트 땅마다 상태를 읽어 칸 · 아이콘을 맞춘다 — 상태가 바뀐 땅이 있으면 true(다시 칠한다). commanding = 명령을 받는 중(방문할 수 있는 땅을 띄운다)
        private bool ShowEvents(bool commanding)
        {
            WorldState state = RunState.World.State;
            List<int> visitable = commanding ? RunState.VisitableEvents() : null;
            bool lit = Flashing && (int)((_flashUntil - Time.unscaledTime) / FlashBlink) % 2 == 0;
            bool changed = false, retext = _eventTurn != state.Turn;
            _eventTurn = state.Turn;
            for (int region = 0; region < _events.Length; region++)
            {
                byte now = !state.Graph.IsEvent[region] ? EventNone
                    : region == _flash && lit ? EventLit
                    : state.EventClosed[region] ? EventClosed
                    : visitable != null && visitable.Contains(region) ? EventVisitable : EventOpen;
                if (now != _events[region])
                {
                    _events[region] = now;
                    changed = true;
                }
                // 칸 · 아이콘은 닫힘 · 정체가 바뀔 때만(깜박임은 칠하기만) — 열림 = 미지의 땅(「?」) · 방문 가능 자리 = 방문 장면이 열려 드러남 · 닫힘
                byte mark = now == EventNone ? EventNone : state.EventClosed[region] ? EventClosed : UnknownLand.Hidden(region) ? EventOpen : EventVisitable;
                if (mark == _eventMarks[region] && !(retext && mark != EventNone))
                    continue;
                _eventMarks[region] = mark;
                Image cell = _eventCells[region];
                cell.gameObject.SetActive(mark != EventNone);
                if (mark == EventNone)
                    continue;
                bool closed = mark == EventClosed;
                EventLandDefinition definition = RunState.EventOf(region);
                cell.color = closed ? ClosedCell : Color.white;
                // 이름 · 효과는 땅 카드(UI/LandHover)가 보여 준다 — 칸은 그림만
                _eventIcons[region].Set(mark == EventOpen ? _unknownIcon : definition != null ? definition.Icon : null, "", "");
                _eventIcons[region].GetComponent<Image>().color = closed ? ClosedIcon : Color.white;
            }
            return changed;
        }

        private static Image NewImage(RectTransform parent, string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.raycastTarget = false;
            return image;
        }

        private void OnDestroy()
        {
            foreach (Object made in new Object[] { _cellTexture, _nearIds, _nearDist, _lands, _arrows, _ramp, _lut, _material })
                if (made != null)
                    Destroy(made);
        }

        // owner[땅] = 영주 번호, colorOf(영주 번호) = 그 영주의 색, mine = 내 영주 번호, invadable[땅] = 침략할 수 있는 땅(명령을 못 내리는 동안은 null — 화살표 · 빗금 없음),
        // selected = 고른 땅(없으면 None)
        public void Show(int[] owner, System.Func<int, Color> colorOf, int mine, bool[] invadable, int selected)
        {
            // 가리킨 땅의 영주만 그림 위에 이름이 뜬다
            Lords.Point(Hovered == None ? -1 : owner[Hovered]);
            ShowSpecials();
            // 확대한 채 침략하면 걸어가는 내 영주가 화면 밖일 수 있다 — 걷는 동안 지도가 내 영주를 가운데에 두고 따라간다(끌기 한계 안에서, 배율은 그대로)
            if (_zoom > 1 && Lords.MarchingAt(mine, out Vector2 at))
            {
                Vector2 size = ((RectTransform)transform).rect.size;
                Place(_home + new Vector2(size.x, -size.y) * 0.5f - at * _zoom);
            }
            bool lands = ShowEvents(invadable != null) || !_painted, owners = !_painted || mine != _mine;
            for (int region = 0; region < _owner.Length; region++)
            {
                Color32 color = colorOf(owner[region]);
                bool open = invadable != null && invadable[region];
                if (owner[region] != _owner[region])
                    owners = true;
                if (owner[region] != _owner[region] || open != _invadable[region] || !color.Equals(_colors[region]))
                    lands = true;
                _owner[region] = owner[region];
                bool crowned = owner[region] == mine;
                if (_crowns[region].gameObject.activeSelf != crowned)
                    _crowns[region].gameObject.SetActive(crowned);
                if (_invadeMarks[region].gameObject.activeSelf != open)
                    _invadeMarks[region].gameObject.SetActive(open);
                _invadeHome[region] = _specials[region] != null
                    ? IconY + (IconBack + _invadeMarks[region].rectTransform.sizeDelta.y) * 0.5f + CrownGap
                    : IconY;
                _colors[region] = color;
                _invadable[region] = open;
            }
            bool pointed = !_painted || selected != _selected || Hovered != _hovered || mine != _mine;
            if (!lands && !pointed)
                return;
            _selected = selected;
            _hovered = Hovered;
            _mine = mine;
            _painted = true;
            if (pointed)
            {
                SetFloat(MineId, _mine);
                SetFloat(HoveredId, _hovered);
                SetFloat(SelectedId, _selected);
            }
            if (lands || owners)
                WriteLands(owners);
            Lords.Sync(_owner, colorOf, _mine);
        }

        private static readonly int MineId = Shader.PropertyToID("_Mine"), HoveredId = Shader.PropertyToID("_Hovered"), SelectedId = Shader.PropertyToID("_Selected"),
            HatchId = Shader.PropertyToID("_Hatch"), WaveShiftId = Shader.PropertyToID("_WaveShift"), WaveShift4Id = Shader.PropertyToID("_WaveShift4");

        // 마스크 안이면 uGUI가 머티리얼 사본(스텐실)으로 그린다 — 사본에도 넣는다
        private void SetFloat(int id, float value)
        {
            _material.SetFloat(id, value);
            Material shown = _image.materialForRendering;
            if (shown != _material)
                shown.SetFloat(id, value);
        }

        // 땅마다의 표(주인 색 · 주인 · 지형색 · 이벤트 상태 · 침략 가능)를 다시 쓰고, 화살표가 달라졌으면 다시 그린다. owners = 판도나 내 영주가 바뀌었다
        private void WriteLands(bool owners)
        {
            int regions = _owner.Length;
            bool arrows = owners;
            for (int region = 0; region < regions; region++)
            {
                Color32 color = _colors[region], terrain = _terrain[region];
                _landPixels[region] = new Color32(color.r, color.g, color.b, (byte)(_owner[region] + 1));
                _landPixels[regions + region] = new Color32(terrain.r, terrain.g, terrain.b, (byte)(_events[region] | (_invadable[region] ? 8 : 0)));
                bool wanted = _invadable[region] || _events[region] == EventVisitable;
                if (wanted != _arrowWanted[region])
                    arrows = true;
                _arrowWanted[region] = wanted;
            }
            _lands.SetPixels32(_landPixels);
            _lands.Apply();
            if (!arrows)
                return;
            foreach (int pixel in _arrowPlotted)
                _arrowPixels[pixel] = default;
            _arrowPlotted.Clear();
            Arrows = 0;
            for (int region = 0; region < regions; region++)
                if (_arrowWanted[region] && DrawArrow(region))
                    Arrows++;
            _arrows.SetPixels32(_arrowPixels);
            _arrows.Apply();
        }

        // 내 땅에서 region으로 화살표 하나 — 국경을 가장 길게 맞댄 내 땅과의 국경 한가운데에서, region의 표시 자리 쪽(여덟 방향 중 가장 가까운 쪽)으로.
        // 맞댄 내 땅이 없으면 false
        private bool DrawArrow(int region)
        {
            List<Vector2Int> border = null;
            int regions = _owner.Length;
            for (int other = 0; other < regions; other++)
                if (_owner[other] == _mine && _borders.TryGetValue(Mathf.Min(region, other) * regions + Mathf.Max(region, other), out List<Vector2Int> cells)
                    && (border == null || cells.Count > border.Count))
                    border = cells;
            if (border == null)
                return false;
            // 국경 칸들의 한가운데에 가장 가까운 국경 칸 — 굽은 국경에서도 화살표가 국경 위에서 나오게
            Vector2 mean = Vector2.zero;
            foreach (Vector2Int cell in border)
                mean += cell;
            mean /= border.Count;
            Vector2 from = border[0];
            foreach (Vector2Int cell in border)
                if (((Vector2)cell - mean).sqrMagnitude < (from - mean).sqrMagnitude)
                    from = cell;
            // 겨누는 곳 = 그 땅의 아이콘 한가운데(표시 자리의 조금 위)
            Vector2 to = _anchors[region] + new Vector2(0f, -IconY / CellPixels);
            // 방향 — 0 오른쪽 · 1 오른쪽 아래 · 2 아래 … 시계 방향으로 45도씩(칸 좌표는 아래가 +y)
            int direction = Mathf.RoundToInt(Mathf.Atan2(to.y - from.y, to.x - from.x) / (Mathf.PI / 4f));
            direction = (direction % 8 + 8) % 8;
            float angle = direction * Mathf.PI / 4f;
            float advance = Mathf.Clamp((to - from).magnitude - ArrowClearance - ArrowHalf, ArrowMinPoke - ArrowHalf, ArrowAdvance);
            Vector2 center = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * advance;
            Stamp(direction % 2 == 0 ? ArrowEast : ArrowSouthEast, direction / 2, center);
            return true;
        }

        // 도장을 시계 방향으로 90도씩 turns번 돌려 center에 찍는다(화살표 텍스처에) — 흰 몸과, 몸에 닿은(대각선 포함) 빈 칸의 검은 테두리
        private void Stamp(string[] shape, int turns, Vector2 center)
        {
            int rows = shape.Length, columns = shape[0].Length;
            var body = new HashSet<Vector2Int>();
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    if (shape[row][column] != '#')
                        continue;
                    float x = column - (columns - 1) * 0.5f, y = row - (rows - 1) * 0.5f;
                    for (int turn = 0; turn < turns; turn++)
                    {
                        float swap = x;
                        x = -y;
                        y = swap;
                    }
                    body.Add(new Vector2Int(Mathf.FloorToInt(center.x + x + 0.5f), Mathf.FloorToInt(center.y + y + 0.5f)));
                }
            foreach (Vector2Int cell in body)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var near = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (!body.Contains(near))
                            Plot(near, SeaColor);
                    }
            foreach (Vector2Int cell in body)
                Plot(cell, White);
        }

        private void Plot(Vector2Int cell, Color32 color)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= _width || cell.y >= _height)
                return;
            int pixel = (_height - 1 - cell.y) * _width + cell.x;
            _arrowPixels[pixel] = color;
            _arrowPlotted.Add(pixel);
        }

        // 물결 흐름(사용자 10/26) — 줄이 오른쪽으로 흐른다(칸/초), 줄마다 빠르기를 0.6~1.4배(다섯 갈래)로 달리 · 0.1초 계단. 물결 무늬는 셰이더에
        private const float WaveSpeed = 1.5f, WaveTick = 0.1f;
        private const int WaveSpeeds = 5;
        // 밝기 표(_lut)의 줄 수 — 줄 순서는 Bake · 셰이더 ROW_와 같다
        private const int LutRows = 19;

        // 1보다 크면 그만큼 흰색 쪽으로(1.2 = 흰색 20% 섞음)
        private static Color32 Scaled(Color32 color, float scale)
            => scale > 1f ? Color32.Lerp(color, White, scale - 1f) : new Color32((byte)(color.r * scale), (byte)(color.g * scale), (byte)(color.b * scale), 255);

        // 빠르기 갈래 speed(0~4)의 물결이 time초에 흘러간 칸 — 셰이더에는 이 값을 넘긴다(옛 칠하기와 같은 내림)
        private static int WaveShift(float time, int speed) => Mathf.FloorToInt(time * WaveSpeed * (0.6f + speed * 0.2f));
        // 땅 가장자리 띠 두께(칸) — 이벤트 땅 깜박임 흰 테두리 · 고른 땅 노란 테두리
        private const int EdgeBand = 2;

        // 안 바뀌는 그림을 텍스처로 굽고 머티리얼을 만든다(Build에서 한 번). 텍스처는 모두 아래 줄부터(텍스처 순서) · Point
        // 칸 : R = 땅 + 1(0 = 바다) · G = 오른쪽 칸의 다른 땅 + 1 · B = 아래 칸의 다른 땅 + 1 · A = 지도 안 바다까지 거리(0~7) | 가장자리 2칸 안 << 3 | 장식 << 4 | 기복도 톤 << 5
        // 이웃 땅 : NearReach칸(체비쇼프) 안의 다른 땅을 가까운 순으로 NearSlots개(+ 1, 0 = 없음) · 그 거리
        private void Bake()
        {
            int count = _width * _height, regions = _owner.Length;
            var cells = new Color32[count];
            var ids = new Color32[count];
            var dists = new Color32[count];
            byte[] tones = Relief(_cells, _width, _height);
            var decor = new bool[count];
            foreach (int pixel in _decor)
                decor[pixel] = true;
            // 칸마다 이웃 땅 → 가장 가까운 거리(창 안에서 처음 본 순서대로)
            var near = new int[(NearReach * 2 + 1) * (NearReach * 2 + 1)];
            var nearDistance = new int[near.Length];
            var id = new byte[NearSlots];
            var gap = new byte[NearSlots];
            int crowded = 0;
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    int region = _cells[y * _width + x], pixel = (_height - 1 - y) * _width + x;
                    if (region < 0)
                        continue;
                    int found = 0, sea = 7;
                    for (int dy = -NearReach; dy <= NearReach; dy++)
                        for (int dx = -NearReach; dx <= NearReach; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= _width || ny >= _height)
                                continue;   // 지도 밖은 바다도 남의 땅도 아니다(대륙이 판 밖으로 이어진다)
                            int other = _cells[ny * _width + nx], distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            if (other < 0)
                                sea = Mathf.Min(sea, distance);
                            if (other < 0 || other == region)
                                continue;
                            int slot = System.Array.IndexOf(near, other, 0, found);
                            if (slot < 0)
                            {
                                slot = found++;
                                near[slot] = other;
                                nearDistance[slot] = distance;
                            }
                            else
                                nearDistance[slot] = Mathf.Min(nearDistance[slot], distance);
                        }
                    if (found > NearSlots)
                    {
                        crowded++;
                        System.Array.Sort(nearDistance, near, 0, found);
                    }
                    System.Array.Clear(id, 0, NearSlots);
                    System.Array.Clear(gap, 0, NearSlots);
                    for (int slot = 0; slot < Mathf.Min(found, NearSlots); slot++)
                    {
                        id[slot] = (byte)(near[slot] + 1);
                        gap[slot] = (byte)nearDistance[slot];
                    }
                    ids[pixel] = new Color32(id[0], id[1], id[2], id[3]);
                    dists[pixel] = new Color32(gap[0], gap[1], gap[2], gap[3]);
                    int right = At(_cells, _width, _height, x + 1, y), down = At(_cells, _width, _height, x, y + 1);
                    int flags = sea | (NearEdge(_cells, _width, _height, x, y, region, EdgeBand) ? 8 : 0) | (decor[pixel] ? 16 : 0) | tones[y * _width + x] << 5;
                    cells[pixel] = new Color32((byte)(region + 1), (byte)(right >= 0 && right != region ? right + 1 : 0), (byte)(down >= 0 && down != region ? down + 1 : 0), (byte)flags);
                }
            // 다섯 땅 이상이 4칸 안에 모이는 칸은 가까운 넷만 본다 — 지금 지도(40칸)에는 없다
            if (crowded > 0)
                Debug.LogWarning($"WorldMapView: 이웃 땅이 {NearSlots}개를 넘는 칸 {crowded}개 — 가까운 {NearSlots}개만 본다");
            _cellTexture = DataTexture(_width, _height, cells, true);
            _nearIds = DataTexture(_width, _height, ids, true);
            _nearDist = DataTexture(_width, _height, dists, true);
            _landPixels = new Color32[regions * 2];
            _lands = DataTexture(regions, 2, _landPixels, false);
            _arrowPixels = new Color32[count];
            _arrows = DataTexture(_width, _height, _arrowPixels, false);
            _arrowWanted = new bool[regions];
            // 밝기 표 — [줄 × 256 + 값] = 그 값을 그 줄의 밝기로 바꾼 값. 셰이더가 줄 번호(ROW_)로 읽는다.
            // 내림 경계(220 × 0.9 = 197.99…)에서 GPU의 float 곱과 C#이 갈리므로, 옛 칠하기와 같은 C# 식으로 여기서 계산해 둔다
            float[] fills = { Fill, HoverFill, OpenHoverFill, MineFill };
            var lut = new Color32[LutRows * 256];
            for (int value = 0; value < 256; value++)
            {
                var gray = new Color32((byte)value, (byte)value, (byte)value, 255);
                for (int row = 0; row < fills.Length; row++)
                {
                    float fill = fills[row];
                    lut[row * 256 + value] = Scaled(gray, fill);
                    lut[(row + 4) * 256 + value] = Scaled(gray, fill * InnerLine);
                }
                float[] scales = { BandFill, EventHatch, EventHatchHover, EventClosedLight, EventLitLight, EventVisitableHoverLight, EventVisitableLight, EventHoverLight };
                for (int k = 0; k < scales.Length; k++)
                    lut[(8 + k) * 256 + value] = Scaled(gray, scales[k]);
                lut[16 * 256 + value] = Color32.Lerp(gray, White, GlowWhite);
                lut[17 * 256 + value] = Color32.Lerp(gray, White, DecorBrightness);
                lut[18 * 256 + value] = Color32.Lerp(gray, SeaColor, DecorShade);
            }
            _lut = DataTexture(256, LutRows, lut, true);
            // 0~255 → 화면 색 — 옛 그림(sRGB 텍스처)과 같은 변환을 타게 sRGB 텍스처로 둔다
            var ramp = new Color32[256];
            for (int i = 0; i < ramp.Length; i++)
                ramp[i] = new Color32((byte)i, (byte)i, (byte)i, 255);
            _ramp = new Texture2D(ramp.Length, 1, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _ramp.SetPixels32(ramp);
            _ramp.Apply(false, true);

            _material = new Material(_shader) { name = "WorldMap (지도)" };
            _material.SetTexture("_NearIds", _nearIds);
            _material.SetTexture("_NearDist", _nearDist);
            _material.SetTexture("_Lands", _lands);
            _material.SetTexture("_Arrows", _arrows);
            _material.SetTexture("_Ramp", _ramp);
            _material.SetTexture("_Lut", _lut);
            _material.SetFloat(HatchId, _hatch ? 1f : 0f);
            _image.texture = _cellTexture;
            _image.material = _material;
        }

        // 값을 그대로 읽는 텍스처(색 공간 변환 · 거르기 없음). frozen = 다시 쓰지 않는다(CPU 쪽 사본을 버린다)
        private static Texture2D DataTexture(int width, int height, Color32[] pixels, bool frozen)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, frozen);
            return texture;
        }

        // 지금 갈 수 있는 땅의 표시(칼 · 방문할 수 있는 이벤트 칸)가 톡톡 튀는 높이(px) — 칸마다 BobStep초. 픽셀이 뭉개지지 않게 크기 대신 자리를 계단으로(사용자 10/27, Slay the Spire 지도 참고)
        private static readonly float[] Bob = { 0f, 3f, 6f, 6f, 3f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        private const float BobStep = 0.07f;
        private Image[] _invadeMarks;
        private float[] _invadeHome;
        private int _bobTick = -1;

        // 바다 물결 — 셰이더가 칸마다 그리고, 여기서는 0.1초마다 갈래별로 흘러간 칸 다섯 개만 넘긴다
        private void Update()
        {
            int bob = Mathf.FloorToInt(Time.unscaledTime / BobStep);
            if (_invadeMarks != null && bob != _bobTick)
            {
                _bobTick = bob;
                float lift = Bob[bob % Bob.Length];
                for (int region = 0; region < _invadeMarks.Length; region++)
                {
                    if (_invadeMarks[region].gameObject.activeSelf)
                        _invadeMarks[region].rectTransform.anchoredPosition = new Vector2(0f, _invadeHome[region] + lift);
                    if (_eventCells[region].gameObject.activeSelf)
                        _eventCells[region].rectTransform.anchoredPosition = new Vector2(0f, IconY + (_events[region] == EventVisitable ? lift : 0f));
                }
            }
            int tick = Mathf.FloorToInt(Time.unscaledTime / WaveTick);
            if (_material == null || tick == _waveTick)
                return;
            _waveTick = tick;
            SetWave(tick * WaveTick);
        }

        private void SetWave(float time)
        {
            var shifts = new Vector4(WaveShift(time, 0), WaveShift(time, 1), WaveShift(time, 2), WaveShift(time, 3));
            _material.SetVector(WaveShiftId, shifts);
            _material.SetFloat(WaveShift4Id, WaveShift(time, WaveSpeeds - 1));
            Material shown = _image.materialForRendering;
            if (shown != _material)
            {
                shown.SetVector(WaveShiftId, shifts);
                shown.SetFloat(WaveShift4Id, WaveShift(time, WaveSpeeds - 1));
            }
        }

        // 지도 밖은 바다로 친다
        private static int At(int[] cells, int width, int height, int x, int y)
            => x < 0 || y < 0 || x >= width || y >= height ? WorldMapDefinition.Sea : cells[y * width + x];

        // 그 땅의 가장자리에서 range칸 안인가
        private static bool NearEdge(int[] cells, int width, int height, int x, int y, int region, int range)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                    if (At(cells, width, height, x + dx, y + dy) != region)
                        return true;
            return false;
        }

        // 이벤트 땅 = 산맥 띠 — 음영 기복도(사용자 10/26 참고 그림: 아이콘 없이 등성이 한쪽은 빛 · 반대쪽은 그림자, 비탈에 깃털 같은 잔 골짜기, 따뜻한 갈색~주황).
        // 높이 = 띠 가장자리에서 떨어진 거리(가운데가 등성이) − 골짜기 노이즈. 빛은 왼쪽 위에서 — 기울기로 잰 밝기를 톤 셋으로 나눠 굽는다(색은 셰이더 RockTone).
        // 상태(셰이더): 열림 = 그대로 · 방문 가능 = 밝아짐(화살표는 Show가) · 반짝임 = 더 밝게 + 흰 굵은 테두리 · 닫힘 = 어둡게(아이콘 반투명은 칸이)
        // 톤 경계(빛 세기) — 평지(기울기 없음, 0.73)가 가운데 톤. 톤 셋 · 큰 골짜기만(사용자 10/26 「너무 구체적 — 밀도를 낮춰」)
        private static readonly float[] ToneSteps = { 0.6f, 0.85f };
        // 등성이 높이(가장자리에서 1칸당) · 골짜기 깊이 · 골짜기 노이즈 한 덩이 크기(칸)
        private const float RidgeRise = 1f, GullyDepth = 1f, GullyScale = 11f;
        // 기복도 톤 표(칸마다) — 지도 칸 배열이 바뀔 때만 다시 만든다
        private static int[] _reliefCells;
        private static byte[] _relief;

        // 땅 칸마다 기복도 톤 번호(0~2) — 이벤트 땅에만 쓰지만 모양만 보므로 모든 땅을 한 번에 잰다
        private static byte[] Relief(int[] cells, int width, int height)
        {
            if (_reliefCells == cells)
                return _relief;
            int count = width * height;
            // 가장자리에서의 거리(같은 땅 안으로만 번지는 3-4 챔퍼 — 가로세로 1, 대각선 1.4)
            var distance = new float[count];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int region = cells[y * width + x];
                    bool edge = At(cells, width, height, x + 1, y) != region || At(cells, width, height, x - 1, y) != region
                        || At(cells, width, height, x, y + 1) != region || At(cells, width, height, x, y - 1) != region;
                    distance[y * width + x] = edge ? 0.5f : float.MaxValue;
                }
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < count; k++)
                {
                    int i = pass == 0 ? k : count - 1 - k, x = i % width, y = i / width, step = pass == 0 ? -1 : 1;
                    if (cells[i] < 0)
                        continue;
                    for (int n = 0; n < 4; n++)
                    {
                        int dx = n == 0 ? step : n - 2, dy = n == 0 ? 0 : step;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || cells[ny * width + nx] != cells[i])
                            continue;
                        distance[i] = Mathf.Min(distance[i], distance[ny * width + nx] + (dx != 0 && dy != 0 ? 1.4f : 1f));
                    }
                }
            // 높이 — 가장자리 3칸까지는 골짜기를 얕게(띠 테두리가 깨지지 않게)
            var rise = new float[count];
            for (int i = 0; i < count; i++)
                if (cells[i] >= 0)
                    rise[i] = distance[i] * RidgeRise - GullyDepth * Gullies(i % width, i / width) * Mathf.Min(distance[i] / 3f, 1f);
            // 빛 — 왼쪽 위 비스듬히(칸 좌표는 아래가 +y)
            var sun = new Vector3(-1f, -1f, 1.5f).normalized;
            var tones = new byte[count];
            for (int i = 0; i < count; i++)
            {
                if (cells[i] < 0)
                    continue;
                int x = i % width, y = i / width;
                float gx = (RiseAt(rise, cells, width, height, x + 1, y, i) - RiseAt(rise, cells, width, height, x - 1, y, i)) * 0.5f;
                float gy = (RiseAt(rise, cells, width, height, x, y + 1, i) - RiseAt(rise, cells, width, height, x, y - 1, i)) * 0.5f;
                float shine = Vector3.Dot(new Vector3(-gx, -gy, 1f).normalized, sun);
                byte tone = 0;
                while (tone < ToneSteps.Length && shine >= ToneSteps[tone])
                    tone++;
                tones[i] = tone;
            }
            _reliefCells = cells;
            return _relief = tones;
        }

        // (x, y)의 높이 — 다른 땅 · 지도 밖이면 기준 칸(self)의 높이를 0으로 깎아 낸 값(띠 밖은 낮다)
        private static float RiseAt(float[] rise, int[] cells, int width, int height, int x, int y, int self)
            => x < 0 || y < 0 || x >= width || y >= height || cells[y * width + x] != cells[self] ? 0f : rise[y * width + x];

        // 골짜기 노이즈(0~1) — 값 노이즈 세 겹을 「능선형」으로 접어, 가는 선이 갈라지며 뻗는 무늬
        private static float Gullies(int x, int y)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f / GullyScale;
            for (int octave = 0; octave < 3; octave++)
            {
                sum += amplitude * (1f - Mathf.Abs(2f * ValueNoise(x * frequency + octave * 17.3f, y * frequency) - 1f));
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return sum / 1.75f;
        }

        private static float ValueNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
        }

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        // 화면 좌표의 땅 번호 — 바다 · 지도 밖이면 None
        public int RegionAt(Vector2 screen, Camera camera)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out Vector2 local))
                return None;
            Rect area = rect.rect;
            int x = Mathf.FloorToInt((local.x - area.xMin) / area.width * _width);
            int y = Mathf.FloorToInt((area.yMax - local.y) / area.height * _height);
            int region = At(_cells, _width, _height, x, y);
            return region < 0 ? None : region;
        }

        // ── 지형 장식(사용자 10/11 — 임시 그림) — 땅 안에 그 지형의 작은 도장을 드문드문. 지도 한 칸 = 도장의 한 점.
        // 자리는 땅 번호로 씨앗을 고정한다(턴마다 그대로). 국경 · 표시 자리(아이콘 · 번호)에서 떨어뜨린다. 땅 모양(번호 지도)은 건드리지 않고 칠할 때 얹기만 한다
        // 땅 넓이(칸) 이만큼에 도장 하나 · 땅마다 최대 개수 · 국경에서 띄우는 칸 · 도장끼리 띄우는 칸
        private const int DecorArea = 260;
        private const int DecorMax = 7;
        private const int DecorBorderGap = 3;
        private const int DecorGap = 5;

        // 지형(전투 맵 에셋 이름) → 도장. 표에 없는 지형은 장식이 없다
        private static readonly Dictionary<string, string[]> DecorStamps = new Dictionary<string, string[]>
        {
            { "Map_Forest", new[] { "..#..", ".###.", "#####", "..#.." } },                 // 나무
            { "Map_Ice", new[] { "..#..", ".#.#.", "#...#" } },                            // 얼음 산
            { "Map_Grassland", new[] { "#.#.#", ".###." } },                               // 풀
            { "Map_Swamp", new[] { ".#.#.", ".#.#.", "#####" } },                          // 갈대와 물
            { "Map_Jungle", new[] { ".##.##.", "#..#..#", "...#...", "...#..." } },        // 큰 잎
            { "Map_Plain", new[] { "..##..", ".#..#." } },                                 // 언덕
        };

        private int[] _decor = new int[0];

        private void BuildDecor(WorldMapDefinition map)
        {
            var pixels = new List<int>();
            var taken = new List<RectInt>();
            for (int region = 0; region < map.RegionCount; region++)
            {
                if (!DecorStamps.TryGetValue(map.TerrainOf(region).name, out string[] stamp))
                    continue;
                int area = 0, minX = _width, maxX = 0, minY = _height, maxY = 0;
                for (int i = 0; i < _cells.Length; i++)
                    if (_cells[i] == region)
                    {
                        area++;
                        minX = Mathf.Min(minX, i % _width);
                        maxX = Mathf.Max(maxX, i % _width);
                        minY = Mathf.Min(minY, i / _width);
                        maxY = Mathf.Max(maxY, i / _width);
                    }
                int want = Mathf.Min(DecorMax, area / DecorArea), w = stamp[0].Length, h = stamp.Length;
                var random = new System.Random(1000 + region);
                RectInt mark = _marks[region];   // 표시 자리(아이콘 판 · 왕관)는 비운다
                // 자리를 못 찾아도 끝나게 시도 횟수를 묶는다
                for (int tries = 0, placed = 0; tries < 400 && placed < want; tries++)
                {
                    int x = random.Next(minX, maxX + 1), y = random.Next(minY, maxY + 1);
                    var box = new RectInt(x - DecorGap, y - DecorGap, w + DecorGap * 2, h + DecorGap * 2);
                    if (box.Overlaps(mark) || taken.Exists(other => other.Overlaps(box)) || !Inside(region, x - DecorBorderGap, y - DecorBorderGap, w + DecorBorderGap * 2, h + DecorBorderGap * 2))
                        continue;
                    taken.Add(new RectInt(x, y, w, h));
                    placed++;
                    for (int sy = 0; sy < h; sy++)
                        for (int sx = 0; sx < w; sx++)
                            if (stamp[sy][sx] == '#')
                                pixels.Add((_height - 1 - (y + sy)) * _width + x + sx);
                }
            }
            _decor = pixels.ToArray();
        }

        // (x, y)에서 width × height 칸이 전부 region의 땅인가
        private bool Inside(int region, int x, int y, int width, int height)
        {
            for (int cy = y; cy < y + height; cy++)
                for (int cx = x; cx < x + width; cx++)
                    if (At(_cells, _width, _height, cx, cy) != region)
                        return false;
            return true;
        }

        // 확대한 지도를 끌어 옮긴 끝의 떼기는 누르기가 아니다
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_zoom == 1 || !eventData.dragging)
                Clicked?.Invoke(RegionAt(eventData.position, eventData.pressEventCamera));
        }

        // ── 확대 · 끌기(사용자 10/11) — 지도는 처음 크기(지도 한 칸 = 화면 4px)가 가장 작고, 휠을 올리면 정수배로만 커진다(도트 유지).
        // 마우스가 가리키는 곳을 중심으로 커지고, 커진 지도는 끌어 옮긴다. 지도 영역 밖으로 빈 곳이 드러나지 않게 가둔다.
        // 그림 · 아이콘 · 글이 같이 커진다(RectTransform의 크기 배율). 화면 좌표 → 땅 번호(RegionAt)는 배율 · 자리를 따라간다
        private const int MaxZoom = 2;
        private int _zoom = 1;
        private Vector2 _home;

        public int Zoom => _zoom;

        public void OnScroll(PointerEventData eventData)
        {
            int zoom = Mathf.Clamp(_zoom + (eventData.scrollDelta.y > 0f ? 1 : -1), 1, MaxZoom);
            var rect = (RectTransform)transform;
            if (zoom == _zoom || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.enterEventCamera, out Vector2 local))
                return;
            // 가리킨 점(local)이 화면의 같은 자리에 남게
            Vector2 position = rect.anchoredPosition + local * (_zoom - zoom);
            _zoom = zoom;
            rect.localScale = new Vector3(zoom, zoom, 1f);
            Place(position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_zoom > 1)
                Place(((RectTransform)transform).anchoredPosition + eventData.delta / GetComponentInParent<Canvas>().scaleFactor);
        }

        // 지도의 왼쪽 위(기준점)를 position에 — 처음 자리(_home)의 지도 영역을 늘 다 덮는 범위 안으로
        private void Place(Vector2 position)
        {
            var rect = (RectTransform)transform;
            Vector2 extra = rect.rect.size * (_zoom - 1);
            rect.anchoredPosition = new Vector2(Mathf.Clamp(position.x, _home.x - extra.x, _home.x), Mathf.Clamp(position.y, _home.y, _home.y + extra.y));
        }
        public void OnPointerMove(PointerEventData eventData)
        {
            Hovered = RegionAt(eventData.position, eventData.enterEventCamera);
            HoverPosition = eventData.position;
        }

        // 가리키고 있는 화면 좌표(마지막 포인터 이동) — 땅 카드(UI/LandHover)가 이 옆에 선다
        public Vector2 HoverPosition { get; private set; }
        public void OnPointerExit(PointerEventData eventData) => Hovered = None;
    }
}

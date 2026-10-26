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
    // 판도 · 고른 땅 · 가리킨 땅이 바뀔 때마다 그림을 통째로 다시 칠한다(375×249칸).
    // 땅마다 지역 특수 표시 · 이벤트 칸 · 내 땅 왕관은 구운 표시 자리(AnchorOf)에 한 번 놓는다(지형 아이콘은 띄우지 않는다 — 땅 카드 UI/LandHover, 사용자 10/26).
    // 누름 · 가리킴은 화면 좌표를 칸으로 바꿔 땅 번호를 읽는다 — 무엇을 할지는 WorldManager가 정한다
    [RequireComponent(typeof(RawImage))]
    public class WorldMapView : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler, IScrollHandler, IDragHandler
    {
        public const int None = -1;

        // 채움 밝기 — 남의 땅 · 가리킨 남의 땅 · 침략할 수 있는 땅 · 가리킨 그 땅은 지형색 × 이 값, 내 땅 · 가리킨 내 땅은 내 색 × 이 값. 같은 영주의 땅 사이 선(채움 대비)
        // 침략할 수 있는 땅만 지형색 그대로, 나머지는 조금 눌러 둔다(사용자 10/20 U4 — 내 땅 · 갈 수 있는 땅이 묻히지 않게)
        private const float Fill = 0.9f;   // 지형색이 Resurrect 64 팔레트 그대로 보이게 살짝만 어둡게(사용자 10/26 「칙칙하다」) — 침략할 수 있는 땅(OpenFill)과는 갈린다
        private const float HoverFill = 1f;
        private const float OpenFill = 1f;
        private const float OpenHoverFill = 1.2f;
        private const float MineFill = 0.8f;
        private const float MineHoverFill = 1f;
        // 같은 영주의 땅 사이 선 = 채움을 반투명 검정으로 누른 1칸(영주 색 띠는 없다 — 영토는 한 덩어리로, 사용자 10/26)
        private const float InnerLine = 0.55f;
        // 남의 영토(같은 주인의 땅 전부) 바깥 둘레의 영주 색 띠 — 두께(칸, 국경선 포함) · 밝기(영주 색 × 이 값). 국경 · 해안선은 검은 선(EdgeColor)
        // 두께 4칸 = 1배 12px · 2배 24px(사용자 10/26 「더 두껍게」 — 2칸 · 3칸 · 4칸을 같은 런에서 찍어 골랐다)
        private const int LordBand = 4;
        private const float BandFill = 0.8f;
        private static readonly Color32 EdgeColor = new Color32(16, 16, 16, 255);
        // 내 땅 덩어리의 바깥 테두리 두께(칸) · 빗금 간격(칸) · 고른 땅 테두리 두께(칸)
        private const int MineBorder = 2;
        // 내 땅 국경 강조(사용자 10/15 「내 땅이 잘 안 보인다」 — 4X식 국경) — 흰 테두리 안쪽 띠(칸, 테두리 포함 두께) · 그 띠의 흰색 섞는 비율 · 바깥(남의 땅 쪽) 검은 그림자(칸)
        private const int MineGlow = 4;
        private const float GlowWhite = 0.45f;
        private const int MineShadow = 1;
        private const int HatchStep = 5;
        private const int SelectedWidth = 2;
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
        private static readonly Color32 SelectedColor = new Color32(255, 217, 90, 255);
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

        private RawImage _image;
        private Texture2D _texture;
        private Color32[] _pixels;
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
        // 바다 칸의 픽셀 자리(텍스처 순서) · 마지막으로 물결을 칠한 틱(WaveTick 단위)
        private int[] _sea;
        private int _waveTick;

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
            _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _pixels = new Color32[_width * _height];
            _image.texture = _texture;
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
            var sea = new List<int>();
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    if (_cells[y * _width + x] < 0)
                        sea.Add((_height - 1 - y) * _width + x);
            _sea = sea.ToArray();

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
            if (_texture != null)
                Destroy(_texture);
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
            bool changed = ShowEvents(invadable != null);
            changed |= !_painted || selected != _selected || Hovered != _hovered || mine != _mine;
            for (int region = 0; region < _owner.Length; region++)
            {
                Color32 color = colorOf(owner[region]);
                bool open = invadable != null && invadable[region];
                if (owner[region] != _owner[region] || open != _invadable[region] || !color.Equals(_colors[region]))
                    changed = true;
                _owner[region] = owner[region];
                bool crowned = owner[region] == mine;
                if (_crowns[region].gameObject.activeSelf != crowned)
                    _crowns[region].gameObject.SetActive(crowned);
                _colors[region] = color;
                _invadable[region] = open;
            }
            if (!changed)
                return;
            _selected = selected;
            _hovered = Hovered;
            _mine = mine;
            _painted = true;
            Paint(_pixels, _cells, _width, _height, _owner, _colors, _terrain, _mine, _invadable, _hatch ? _invadable : null, _selected, _hovered, _events);
            // 지형 장식 — 내 땅(내 색)은 살짝 밝게, 남의 땅(지형색)은 살짝 어둡게(화살표는 그 위에 찍는다). 지형색만으로 갈리지 않는 눈(적록 색맹)에도 지형이 읽히게 남긴다
            foreach (int pixel in _decor)
            {
                // 이벤트 땅은 비운다(A안 — 장식 없이 국경 · 빗금만)
                int land = _cells[(_height - 1 - pixel / _width) * _width + pixel % _width];
                if (land >= 0 && _events[land] != EventNone)
                    continue;
                _pixels[pixel] = land >= 0 && _owner[land] == _mine
                    ? Color32.Lerp(_pixels[pixel], White, DecorBrightness)
                    : Color32.Lerp(_pixels[pixel], SeaColor, DecorShade);
            }
            Arrows = 0;
            for (int region = 0; region < _owner.Length; region++)
                if ((_invadable[region] || _events[region] == EventVisitable) && DrawArrow(region))
                    Arrows++;
            DrawSea(_waveTick * WaveTick);
            _texture.SetPixels32(_pixels);
            _texture.Apply();
            Lords.Sync(_owner, colorOf, _mine);
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

        // 도장을 시계 방향으로 90도씩 turns번 돌려 center에 찍는다 — 흰 몸과, 몸에 닿은(대각선 포함) 빈 칸의 검은 테두리
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
            if (cell.x >= 0 && cell.y >= 0 && cell.x < _width && cell.y < _height)
                _pixels[(_height - 1 - cell.y) * _width + cell.x] = color;
        }

        // cells = [y × width + x] 땅 번호(위 왼쪽이 0번 칸), pixels = 텍스처 순서(아래 줄부터). open = 침략할 수 있는 땅(밝게만 — 점선 띠는 국경과 따로 놀아 뺐다, 사용자 10/21), hatch = 빗금을 칠 땅(없으면 null)
        // events[땅] = 이벤트 땅의 상태(EventState — 없으면 null: 이벤트 땅을 옛 임시 빗금으로)
        // terrain[땅] = 그 땅의 지형색(남의 땅 속)
        public static void Paint(Color32[] pixels, int[] cells, int width, int height, int[] owner, Color32[] colors, Color32[] terrain, int mine, bool[] open, bool[] hatch, int selected, int hovered, byte[] events = null)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int region = cells[y * width + x];
                    Color32 pixel = SeaColor;
                    if (region >= 0)
                    {
                        Color32 color = colors[region];
                        bool own = owner[region] == mine;
                        bool reachable = !own && open != null && open[region];
                        float fill = own ? (region == hovered ? MineHoverFill : MineFill)
                            : reachable ? (region == hovered ? OpenHoverFill : OpenFill)
                            : region == hovered ? HoverFill : Fill;
                        Color32 ground = own ? color : terrain[region];
                        pixel = Scaled(ground, fill);
                        if (hatch != null && hatch[region] && (x + y) % HatchStep == 0)
                            pixel = color;
                        // 이벤트 땅(주인 없음) — 바깥 땅의 고른 회색과 갈리게 검은 바탕에 옅은 빗금(임시 — 그림은 ROADMAP 2-5 3번)
                        if (owner[region] < 0)
                            pixel = (x + y) % HatchStep == 0 ? Scaled(color, region == hovered ? EventHatchHover : EventHatch) : SeaColor;
                        // 이벤트 땅(A안 10/25) — 거의 검게 비우고 국경 · 빗금만: 열림 = 회색 국경 + 옅은 빗금 / 방문 가능 = 흰 굵은 국경 + 밝은 빗금 / 닫힘 = 까만 구멍
                        if (events != null && events[region] != EventNone)
                        {
                            pixel = EventPixel(cells, width, height, x, y, region, events[region], region == hovered);
                            if (region == selected && NearEdge(cells, width, height, x, y, region, SelectedWidth))
                                pixel = SelectedColor;
                            pixels[(height - 1 - y) * width + x] = pixel;
                            continue;
                        }
                        // 선은 한 줄만 — 오른쪽 · 아래 칸과 주인이 다르면 국경선, 주인은 같고 땅만 다르면 옅은 선. 바다와 닿은 칸은 해안선(지도 끝은 선이 없다 — 대륙이 판 밖으로 이어진다, 10/25)
                        bool coast = NearShore(cells, width, height, x, y, CoastWidth);
                        bool lordEdge = false, landEdge = false;
                        for (int k = 0; k < 2; k++)
                        {
                            int other = k == 0 ? At(cells, width, height, x + 1, y) : At(cells, width, height, x, y + 1);
                            if (other >= 0 && other != region)   // 바다 쪽은 위 coast(CoastWidth칸 굵기)
                            {
                                if (owner[other] != owner[region])
                                    lordEdge = true;
                                else
                                    landEdge = true;
                            }
                        }
                        // 내 땅 덩어리의 바깥 가장자리는 굵게 · 그 안쪽은 내 색이 밝게 번진 띠 · 맞닿은 남의 땅 쪽에는 검은 그림자
                        // 남의 땅 국경 · 해안선은 검은 선, 그 안쪽 둘레는 영주 색 띠 — 흰 국경은 내 땅만(사용자 10/15 · 10/26)
                        if (own && (coast || lordEdge || NearOther(cells, width, height, x, y, owner, mine, MineBorder)))
                            pixel = White;
                        else if (own && NearOther(cells, width, height, x, y, owner, mine, MineGlow))
                            pixel = Color32.Lerp(color, White, GlowWhite);
                        else if (!own && NearMine(cells, width, height, x, y, owner, mine))
                            pixel = SeaColor;
                        else if (coast || lordEdge)
                            pixel = EdgeColor;
                        else if (!own && NearOther(cells, width, height, x, y, owner, owner[region], LordBand))
                            pixel = Scaled(color, BandFill);
                        else if (landEdge)
                            pixel = Scaled(ground, fill * InnerLine);
                        // 고른 땅 테두리는 이웃 땅 쪽에 그려진 국경선(흰 선 · 그림자 · 영주 색 선)까지 덮는다 — 안 덮으면 노란 선 밖으로 다른 선이 비친다(사용자 10/15)
                        if (region == selected ? NearEdge(cells, width, height, x, y, region, SelectedWidth)
                            : selected >= 0 && Near(cells, width, height, x, y, selected, own ? MineBorder : MineShadow))
                            pixel = SelectedColor;
                    }
                    else
                        pixel = SeaPixel(x, y);
                    pixels[(height - 1 - y) * width + x] = pixel;
                }
        }

        // ── 바다(사용자 10/25 「삼국지 지도처럼」 — 남 · 동은 바다와 맞닿는 해안) — 어두운 파랑에 옅은 물결 줄. 남의 땅 채움(지형색)보다 어둡게
        private static readonly Color32 SeaFill = new Color32(31, 78, 140, 255), SeaWave = new Color32(58, 110, 178, 255);   // 남청(사용자 10/25 「1안」)
        // 이벤트 땅 빗금 밝기(땅 색 × 이 값) · 가리켰을 때
        private const float EventHatch = 0.45f, EventHatchHover = 0.8f;
        // 물결 — 이 칸 간격의 줄마다 짧은 가로 줄(줄마다 어긋나게)
        private const int WaveRow = 6, WaveSpan = 14, WaveLength = 3;

        // 물결 흐름(사용자 10/26) — 줄이 오른쪽으로 흐른다(칸/초), 줄마다 빠르기를 0.6~1.4배로 달리 · 다시 칠하는 간격(초)
        private const float WaveSpeed = 1.5f, WaveTick = 0.1f;

        // time = 흐른 시각(초) — 0이면 멈춘 물결
        private static Color32 SeaPixel(int x, int y, float time = 0f)
        {
            int row = y / WaveRow;
            int shift = Mathf.FloorToInt(time * WaveSpeed * (0.6f + row * 7 % 5 * 0.2f));
            return y % WaveRow == 0 && ((x - shift + row * (WaveSpan / 2)) % WaveSpan + WaveSpan) % WaveSpan < WaveLength ? SeaWave : SeaFill;
        }

        // 바다 칸만 지금 시각의 물결로 다시 칠한다 — 화살표 도장이 덮은 칸은 둔다. 바뀐 칸이 있으면 true
        private bool DrawSea(float time)
        {
            bool changed = false;
            foreach (int pixel in _sea)
            {
                Color32 now = _pixels[pixel];
                if (!now.Equals(SeaFill) && !now.Equals(SeaWave))
                    continue;
                Color32 next = SeaPixel(pixel % _width, _height - 1 - pixel / _width, time);
                if (!next.Equals(now))
                {
                    _pixels[pixel] = next;
                    changed = true;
                }
            }
            return changed;
        }

        private void Update()
        {
            int tick = Mathf.FloorToInt(Time.unscaledTime / WaveTick);
            if (!_painted || tick == _waveTick)
                return;
            _waveTick = tick;
            if (!DrawSea(tick * WaveTick))
                return;
            _texture.SetPixels32(_pixels);
            _texture.Apply();
        }

        // 해안선 굵기(칸) — 국경선(1칸)보다 한 단계 굵게(사용자 10/25)
        private const int CoastWidth = 2;

        // (x, y)에서 range칸 안에 지도 안의 바다 칸이 있나(지도 밖은 바다가 아니다)
        private static bool NearShore(int[] cells, int width, int height, int x, int y, int range)
        {
            for (int dy = -range; dy <= range; dy++)
                for (int dx = -range; dx <= range; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && cells[ny * width + nx] < 0)
                        return true;
                }
            return false;
        }

        // 지도 밖은 바다로 친다
        private static int At(int[] cells, int width, int height, int x, int y)
            => x < 0 || y < 0 || x >= width || y >= height ? WorldMapDefinition.Sea : cells[y * width + x];

        // 내 땅의 칸 (x, y)에서 range칸 안에 내 땅이 아닌 칸(남의 땅 · 바다)이 있나
        private static bool NearOther(int[] cells, int width, int height, int x, int y, int[] owner, int mine, int range)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;   // 지도 밖은 남의 칸이 아니다 — 대륙이 판 밖으로 이어진다(10/25)
                    int other = cells[ny * width + nx];
                    if (other < 0 || owner[other] != mine)
                        return true;
                }
            return false;
        }

        // 남의 땅의 칸 (x, y)에서 MineShadow칸 안에 내 땅이 있나
        private static bool NearMine(int[] cells, int width, int height, int x, int y, int[] owner, int mine)
        {
            for (int dx = -MineShadow; dx <= MineShadow; dx++)
                for (int dy = -MineShadow; dy <= MineShadow; dy++)
                {
                    int other = At(cells, width, height, x + dx, y + dy);
                    if (other >= 0 && owner[other] == mine)
                        return true;
                }
            return false;
        }

        // 그 땅의 가장자리에서 range칸 안인가
        private static bool NearEdge(int[] cells, int width, int height, int x, int y, int region, int range)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                    if (At(cells, width, height, x + dx, y + dy) != region)
                        return true;
            return false;
        }

        // (x, y)에서 range칸 안에 그 땅이 있나
        private static bool Near(int[] cells, int width, int height, int x, int y, int region, int range)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                    if (At(cells, width, height, x + dx, y + dy) == region)
                        return true;
            return false;
        }

        // 이벤트 땅 = 산맥 띠 — 음영 기복도(사용자 10/26 참고 그림: 아이콘 없이 등성이 한쪽은 빛 · 반대쪽은 그림자, 비탈에 깃털 같은 잔 골짜기, 따뜻한 갈색~주황).
        // 높이 = 띠 가장자리에서 떨어진 거리(가운데가 등성이) − 골짜기 노이즈. 빛은 왼쪽 위에서 — 기울기로 잰 밝기를 RockTones 다섯 톤으로 칠한다.
        // 상태: 열림 = 그대로 · 방문 가능 = 밝아짐 + 흰 굵은 테두리(화살표는 Show가) · 반짝임 = 더 밝게 · 닫힘 = 어둡게(아이콘 반투명은 칸이)
        private static readonly Color32[] RockTones =
        {
            new Color32(70, 45, 30, 255), new Color32(120, 82, 52, 255), new Color32(180, 120, 66, 255),
        };
        // 톤 경계(빛 세기) — 평지(기울기 없음, 0.73)가 가운데 톤. 톤 셋 · 큰 골짜기만(사용자 10/26 「너무 구체적 — 밀도를 낮춰」)
        private static readonly float[] ToneSteps = { 0.6f, 0.85f };
        // 등성이 높이(가장자리에서 1칸당) · 골짜기 깊이 · 골짜기 노이즈 한 덩이 크기(칸)
        private const float RidgeRise = 1f, GullyDepth = 1f, GullyScale = 11f;
        // 기복도 톤 표(칸마다) — 지도 칸 배열이 바뀔 때만 다시 만든다
        private static int[] _reliefCells;
        private static byte[] _relief;

        private static Color32 EventPixel(int[] cells, int width, int height, int x, int y, int region, byte state, bool hovered)
        {
            float light = state == EventClosed ? 0.5f : state == EventLit ? 1.45f : state == EventVisitable ? (hovered ? 1.35f : 1.2f) : hovered ? 1.15f : 1f;
            if (state != EventClosed && state != EventOpen && NearEdge(cells, width, height, x, y, region, 2))
                return White;
            return Scaled(RockTones[Relief(cells, width, height)[y * width + x]], light);
        }

        // 땅 칸마다 기복도 톤 번호(RockTones) — 이벤트 땅에만 쓰지만 모양만 보므로 모든 땅을 한 번에 잰다
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

        // 1보다 크면 그만큼 흰색 쪽으로(1.2 = 흰색 20% 섞음)
        private static Color32 Scaled(Color32 color, float scale)
            => scale > 1f ? Color32.Lerp(color, White, scale - 1f) : new Color32((byte)(color.r * scale), (byte)(color.g * scale), (byte)(color.b * scale), 255);

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
        private const float DecorBrightness = 0.22f;
        private const float DecorShade = 0.3f;
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

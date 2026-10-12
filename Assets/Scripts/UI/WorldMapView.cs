using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 월드 지도 — 땅 번호 지도(WorldMapDefinition.ReadCells)의 칸을 그 땅 영주의 색으로 칠해 정수배로 키워 보여 준다(RawImage · Point).
    // 남의 땅은 영주 색을 어둡게 채우고, 내 땅만 밝게 채워 굵은 흰 테두리를 두른다(지도에서 가장 먼저 보여야 한다 — 사용자 10/11).
    // 다른 영주와의 국경 · 해안선은 흰 선, 같은 영주의 땅 사이는 옅은 선. 침략할 수 있는 땅에는 내 땅에서 그 땅으로 화살표 하나
    // (국경을 가장 길게 맞댄 내 땅에서, 여덟 방향 도트 도장) — _hatch를 켜면 그 땅에 그 영주 색 빗금도. 고른 땅은 노란 테두리, 가리킨 땅은 조금 밝게.
    // 판도 · 고른 땅 · 가리킨 땅이 바뀔 때마다 그림을 통째로 다시 칠한다(375×252칸).
    // 땅마다 지형 아이콘 · 업그레이드 아이콘과 땅 번호는 구운 표시 자리(AnchorOf)에 한 번 놓는다.
    // 누름 · 가리킴은 화면 좌표를 칸으로 바꿔 땅 번호를 읽는다 — 무엇을 할지는 WorldManager가 정한다
    [RequireComponent(typeof(RawImage))]
    public class WorldMapView : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler, IScrollHandler, IDragHandler
    {
        public const int None = -1;

        // 채움 밝기(영주 색 × 이 값) — 남의 땅 · 가리킨 남의 땅 · 내 땅 · 가리킨 내 땅 · 같은 영주의 땅 사이 선(채움 대비)
        private const float Fill = 0.34f;
        private const float HoverFill = 0.5f;
        private const float MineFill = 0.8f;
        private const float MineHoverFill = 1f;
        private const float InnerLine = 0.6f;
        // 내 땅 덩어리의 바깥 테두리 두께(칸) · 빗금 간격(칸) · 고른 땅 테두리 두께(칸)
        private const int MineBorder = 2;
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

        // 표시의 자리(표시 자리 기준, 화면 px): 아이콘 뒤 검은 판(지형 · 업그레이드 둘이 나란히) · 아이콘 높이 · 땅 번호 높이
        private const float IconBack = 36f;
        private const float IconY = 12f;
        private const float LabelY = -22f;
        private const int LabelSize = 24;
        // 표시 자리의 px 값들을 잰 기준 — 지도 한 칸 = 화면 4px
        private const float CellPixels = 4f;

        [Tooltip("땅 번호 · 업그레이드 글 — 도트 폰트")]
        [SerializeField] private Font _dotFont;
        [Tooltip("침략할 수 있는 땅에 화살표와 함께 그 영주 색 빗금도 친다")]
        [SerializeField] private bool _hatch;
        [Tooltip("지도 위를 돌아다니는 영주 그림 — 유닛 몸 그림 한 장(영주 색으로 칠한다, 왼쪽 패널의 영주 그림과 같은 것)")]
        [SerializeField] private Sprite _lordSprite;
        [Tooltip("영주 그림이 드는 깃발 — 흰 그림(영주 색을 입힌다)")]
        [SerializeField] private Sprite _lordFlag;

        private RawImage _image;
        private Texture2D _texture;
        private Color32[] _pixels;
        private int[] _cells;
        private int _width, _height;
        private Vector2Int[] _anchors;
        // [작은 땅 번호 × 땅 수 + 큰 땅 번호] = 두 땅이 맞댄 국경의 칸들 — 화살표가 여기서 나온다
        private readonly Dictionary<int, List<Vector2Int>> _borders = new Dictionary<int, List<Vector2Int>>();

        // 마지막으로 칠한 것 — 달라졌을 때만 다시 칠한다
        private int[] _owner;
        private Color32[] _colors;
        private bool[] _invadable;
        private int _selected = None;
        private int _hovered = None;
        private int _mine = None;
        private bool _painted;

        // 가리키고 있는 땅(없으면 None)
        public int Hovered { get; private set; } = None;
        // 마지막으로 칠한 화살표 수
        public int Arrows { get; private set; }
        // 땅을 눌렀다(바다 · 지도 밖이면 None)
        public event System.Action<int> Clicked;
        // 지도 위를 돌아다니는 영주들 — 표시(번호 · 아이콘) 위에 그려진다
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
            _invadable = new bool[regions];
            _anchors = new Vector2Int[regions];
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
            BuildDecor(map);
            float scale = rect.rect.width / _width;
            for (int region = 0; region < regions; region++)
            {
                Vector2Int anchor = _anchors[region] = map.AnchorOf(region);
                var mark = (RectTransform)new GameObject($"Land{region + 1:00}", typeof(RectTransform)).transform;
                mark.SetParent(rect, false);
                mark.anchorMin = mark.anchorMax = new Vector2(0f, 1f);
                mark.anchoredPosition = new Vector2(anchor.x * scale, -anchor.y * scale);
                mark.sizeDelta = Vector2.zero;

                // 지형 아이콘(왼쪽) · 업그레이드 아이콘(오른쪽) — 각각 검은 판 위에, 원본 크기
                Sprite[] icons = { map.TerrainOf(region).Icon, map.UpgradeOf(region).Icon };
                for (int i = 0; i < icons.Length; i++)
                {
                    float x = (i - 0.5f) * IconBack;
                    Image back = NewImage(mark, "Back");
                    back.color = Color.black;
                    back.rectTransform.sizeDelta = new Vector2(IconBack, IconBack);
                    back.rectTransform.anchoredPosition = new Vector2(x, IconY);
                    Image image = NewImage(mark, "Icon");
                    image.sprite = icons[i];
                    image.SetNativeSize();
                    image.preserveAspect = true;   // 지도 확대가 localScale을 쓴다(CLAUDE.md §5-1)
                    image.rectTransform.anchoredPosition = new Vector2(x, IconY);
                }

                var label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
                label.transform.SetParent(mark, false);
                label.font = _dotFont;
                label.fontSize = LabelSize;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                label.text = $"{region + 1:00}";
                label.rectTransform.sizeDelta = Vector2.zero;
                label.rectTransform.anchoredPosition = new Vector2(0f, LabelY);
                var outline = label.GetComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, 2f);
            }
            Lords = gameObject.AddComponent<WorldLords>();
            Lords.Init(_cells, _width, _height, scale, _anchors, _lordSprite, _lordFlag, _dotFont);
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
            bool changed = !_painted || selected != _selected || Hovered != _hovered || mine != _mine;
            for (int region = 0; region < _owner.Length; region++)
            {
                Color32 color = colorOf(owner[region]);
                bool open = invadable != null && invadable[region];
                if (owner[region] != _owner[region] || open != _invadable[region] || !color.Equals(_colors[region]))
                    changed = true;
                _owner[region] = owner[region];
                _colors[region] = color;
                _invadable[region] = open;
            }
            if (!changed)
                return;
            _selected = selected;
            _hovered = Hovered;
            _mine = mine;
            _painted = true;
            Paint(_pixels, _cells, _width, _height, _owner, _colors, _mine, _hatch ? _invadable : null, _selected, _hovered);
            // 지형 장식 — 채움 색 위에 살짝 밝게(화살표는 그 위에 찍는다)
            foreach (int pixel in _decor)
                _pixels[pixel] = Color32.Lerp(_pixels[pixel], White, DecorBrightness);
            Arrows = 0;
            for (int region = 0; region < _owner.Length; region++)
                if (_invadable[region] && DrawArrow(region))
                    Arrows++;
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

        // cells = [y × width + x] 땅 번호(위 왼쪽이 0번 칸), pixels = 텍스처 순서(아래 줄부터). hatch = 빗금을 칠 땅(없으면 null)
        public static void Paint(Color32[] pixels, int[] cells, int width, int height, int[] owner, Color32[] colors, int mine, bool[] hatch, int selected, int hovered)
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
                        float fill = own ? (region == hovered ? MineHoverFill : MineFill) : region == hovered ? HoverFill : Fill;
                        pixel = Scaled(color, fill);
                        if (hatch != null && hatch[region] && (x + y) % HatchStep == 0)
                            pixel = color;
                        // 선은 한 줄만 — 오른쪽 · 아래 칸과 주인이 다르면 흰 선, 주인은 같고 땅만 다르면 옅은 선. 바다 · 지도 끝과 닿은 칸은 해안선
                        bool coast = At(cells, width, height, x - 1, y) < 0 || At(cells, width, height, x, y - 1) < 0;
                        bool lordEdge = false, landEdge = false;
                        for (int k = 0; k < 2; k++)
                        {
                            int other = k == 0 ? At(cells, width, height, x + 1, y) : At(cells, width, height, x, y + 1);
                            if (other < 0)
                                coast = true;
                            else if (other != region)
                            {
                                if (owner[other] != owner[region])
                                    lordEdge = true;
                                else
                                    landEdge = true;
                            }
                        }
                        // 내 땅 덩어리의 바깥 가장자리는 굵게
                        if (coast || lordEdge || (own && NearOther(cells, width, height, x, y, owner, mine)))
                            pixel = White;
                        else if (landEdge)
                            pixel = Scaled(color, fill * InnerLine);
                        if (region == selected && NearEdge(cells, width, height, x, y, region))
                            pixel = SelectedColor;
                    }
                    pixels[(height - 1 - y) * width + x] = pixel;
                }
        }

        // 지도 밖은 바다로 친다
        private static int At(int[] cells, int width, int height, int x, int y)
            => x < 0 || y < 0 || x >= width || y >= height ? WorldMapDefinition.Sea : cells[y * width + x];

        // 내 땅의 칸 (x, y)에서 MineBorder칸 안에 내 땅이 아닌 칸(남의 땅 · 바다)이 있나
        private static bool NearOther(int[] cells, int width, int height, int x, int y, int[] owner, int mine)
        {
            for (int dx = -MineBorder; dx <= MineBorder; dx++)
                for (int dy = -MineBorder; dy <= MineBorder; dy++)
                {
                    int other = At(cells, width, height, x + dx, y + dy);
                    if (other < 0 || owner[other] != mine)
                        return true;
                }
            return false;
        }

        // 그 땅의 가장자리에서 SelectedWidth칸 안인가
        private static bool NearEdge(int[] cells, int width, int height, int x, int y, int region)
        {
            for (int dx = -SelectedWidth; dx <= SelectedWidth; dx++)
                for (int dy = -SelectedWidth; dy <= SelectedWidth; dy++)
                    if (At(cells, width, height, x + dx, y + dy) != region)
                        return true;
            return false;
        }

        private static Color32 Scaled(Color32 color, float scale)
            => new Color32((byte)(color.r * scale), (byte)(color.g * scale), (byte)(color.b * scale), 255);

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
        // 땅 넓이(칸) 이만큼에 도장 하나 · 땅마다 최대 개수 · 국경에서 띄우는 칸 · 도장끼리 띄우는 칸
        private const int DecorArea = 260;
        private const int DecorMax = 7;
        private const int DecorBorderGap = 3;
        private const int DecorGap = 5;
        // 표시 자리(기준점)에서 좌우 · 위 · 아래로 비우는 칸 — 아이콘 판 둘과 번호
        private const int MarkHalfWidth = 11, MarkAbove = 9, MarkBelow = 10;

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
                Vector2Int anchor = map.AnchorOf(region);
                var mark = new RectInt(anchor.x - MarkHalfWidth, anchor.y - MarkAbove, MarkHalfWidth * 2, MarkAbove + MarkBelow);
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
        public void OnPointerMove(PointerEventData eventData) => Hovered = RegionAt(eventData.position, eventData.enterEventCamera);
        public void OnPointerExit(PointerEventData eventData) => Hovered = None;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 월드 지도 — 땅 번호 지도(WorldMapDefinition.ReadCells)의 칸을 그 땅 영주의 색으로 칠해 정수배로 키워 보여 준다(RawImage · Point).
    // 남의 땅은 영주 색을 어둡게 채우고, 내 땅만 밝게 채워 굵은 흰 테두리를 두른다(지도에서 가장 먼저 보여야 한다 — 사용자 10/11).
    // 다른 영주와의 국경 · 해안선은 흰 선, 같은 영주의 땅 사이는 옅은 선. 침략할 수 있는 땅에는 내 땅에서 그 땅으로 화살표 하나
    // (국경을 가장 길게 맞댄 내 땅에서) — _hatch를 켜면 그 땅에 그 영주 색 빗금도. 고른 땅은 노란 테두리, 가리킨 땅은 조금 밝게.
    // 판도 · 고른 땅 · 가리킨 땅이 바뀔 때마다 그림을 통째로 다시 칠한다(375×252칸).
    // 땅마다 지형 아이콘과 「번호 + 업그레이드 첫 글자」는 구운 표시 자리(AnchorOf)에 한 번 놓는다.
    // 누름 · 가리킴은 화면 좌표를 칸으로 바꿔 땅 번호를 읽는다 — 무엇을 할지는 WorldManager가 정한다
    [RequireComponent(typeof(RawImage))]
    public class WorldMapView : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
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
        // 침략 화살표(칸) — 국경에서 내 땅 쪽으로 물러난 길이 · 그 땅 안으로 들어간 길이(표시 자리까지의 거리에서 Clearance를 뺀 만큼, 이 범위 안)
        // · 몸통 반폭 · 촉 길이 · 촉 반폭
        private const float ArrowBack = 5f;
        private const float ArrowMinReach = 5f;
        private const float ArrowMaxReach = 15f;
        private const float ArrowClearance = 14f;
        private const float ArrowShaft = 1.6f;
        private const float ArrowHead = 7f;
        private const float ArrowHeadHalf = 5f;
        private static readonly Color32 SelectedColor = new Color32(255, 217, 90, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 SeaColor = new Color32(0, 0, 0, 255);

        // 표시의 자리(표시 자리 기준, 화면 px — 지도 한 칸 = 4px일 때의 시안 그대로): 아이콘 뒤 검은 판 · 아이콘 · 글
        private const float IconBack = 36f;
        private const float IconY = 12f;
        private const float LabelY = -22f;
        private const int LabelSize = 24;

        [Tooltip("땅 번호 · 업그레이드 글 — 도트 폰트")]
        [SerializeField] private Font _dotFont;
        [Tooltip("침략할 수 있는 땅에 화살표와 함께 그 영주 색 빗금도 친다")]
        [SerializeField] private bool _hatch;

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

        public void Build(WorldMapDefinition map)
        {
            _image = GetComponent<RawImage>();
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
            float scale = rect.rect.width / _width;
            for (int region = 0; region < regions; region++)
            {
                Vector2Int anchor = _anchors[region] = map.AnchorOf(region);
                var mark = (RectTransform)new GameObject($"Land{region + 1:00}", typeof(RectTransform)).transform;
                mark.SetParent(rect, false);
                mark.anchorMin = mark.anchorMax = new Vector2(0f, 1f);
                mark.anchoredPosition = new Vector2(anchor.x * scale, -anchor.y * scale);
                mark.sizeDelta = Vector2.zero;

                Sprite icon = map.TerrainOf(region).Icon;
                Image back = NewImage(mark, "Back");
                back.color = Color.black;
                back.rectTransform.sizeDelta = new Vector2(IconBack, IconBack);
                back.rectTransform.anchoredPosition = new Vector2(0f, IconY);
                Image image = NewImage(mark, "Icon");
                image.sprite = icon;
                image.SetNativeSize();
                image.rectTransform.anchoredPosition = new Vector2(0f, IconY);

                var label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
                label.transform.SetParent(mark, false);
                label.font = _dotFont;
                label.fontSize = LabelSize;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                label.text = $"{region + 1:00} {map.UpgradeOf(region).DisplayName[0]}";
                label.rectTransform.sizeDelta = Vector2.zero;
                label.rectTransform.anchoredPosition = new Vector2(0f, LabelY);
                var outline = label.GetComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, 2f);
            }
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
            Arrows = 0;
            for (int region = 0; region < _owner.Length; region++)
                if (_invadable[region] && DrawArrow(region))
                    Arrows++;
            _texture.SetPixels32(_pixels);
            _texture.Apply();
        }

        // 내 땅에서 region으로 화살표 하나 — 국경을 가장 길게 맞댄 내 땅과의 국경 한가운데에서, region의 표시 자리 쪽으로. 맞댄 내 땅이 없으면 false
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
            Vector2 to = _anchors[region];
            float distance = (to - from).magnitude;
            Vector2 direction = (to - from) / Mathf.Max(distance, 0.01f);
            float reach = Mathf.Clamp(distance - ArrowClearance, ArrowMinReach, ArrowMaxReach);
            Vector2 start = from - direction * ArrowBack;
            float length = ArrowBack + reach;

            // 흰 화살표 + 검은 테두리 한 칸 — 칸마다 화살표 안쪽인지, 한 칸 넓힌 모양의 안쪽인지 본다
            int pad = Mathf.CeilToInt(length * 0.5f + ArrowHeadHalf) + 3;
            Vector2 middle = start + direction * (length * 0.5f);
            for (int y = Mathf.RoundToInt(middle.y) - pad; y <= Mathf.RoundToInt(middle.y) + pad; y++)
                for (int x = Mathf.RoundToInt(middle.x) - pad; x <= Mathf.RoundToInt(middle.x) + pad; x++)
                {
                    if (x < 0 || y < 0 || x >= _width || y >= _height)
                        continue;
                    bool inside = InArrow(x, y, start, direction, length, 0f);
                    if (inside || InArrow(x, y, start, direction, length, 1f))
                        _pixels[(_height - 1 - y) * _width + x] = inside ? White : SeaColor;
                }
            return true;
        }

        // 화살표 좌표(along = 진행 방향, across = 그 옆)로 본다 — grow = 바깥으로 넓힌 칸 수(테두리)
        private static bool InArrow(int x, int y, Vector2 start, Vector2 direction, float length, float grow)
        {
            Vector2 p = new Vector2(x, y) - start;
            float along = Vector2.Dot(p, direction);
            float across = Mathf.Abs(p.x * direction.y - p.y * direction.x);
            float neck = length - ArrowHead;
            if (along < -grow || along > length + grow * 1.5f)
                return false;
            // 몸통은 목까지, 촉은 목에서 가장 넓고 끝에서 0
            if (along < neck - grow)
                return across <= ArrowShaft + grow;
            return across <= (length + grow * 1.5f - along) * (ArrowHeadHalf / ArrowHead);
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

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(RegionAt(eventData.position, eventData.pressEventCamera));
        public void OnPointerMove(PointerEventData eventData) => Hovered = RegionAt(eventData.position, eventData.enterEventCamera);
        public void OnPointerExit(PointerEventData eventData) => Hovered = None;
    }
}

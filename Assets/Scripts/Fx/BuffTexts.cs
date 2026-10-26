using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 버프 글자 — 스탯 · 쿨다운 버프 선(BuffLinks)이 대상에 닿는 순간 그 위에 「[아이콘] +5%」를 띄운다(사용자 10/15).
    // 값은 데미지 숫자(DamageNumbers)와 같은 3×5 도트 글꼴 + 검은 테두리를 GlyphScale배로(사용자 10/15 "데미지 표기 폰트와 같은 폰트로").
    // 아이콘이 없는 스탯의 이름(「공격속도」)만 도트 폰트 TextMesh로 쓴다. 선을 긋기로 한 것만 띄운다(BuffLinkFx가 솎은 뒤 부른다).
    // FxDirector가 만든다(씬에 따로 두지 않는다)
    public class BuffTexts : MonoBehaviour
    {
        private const float Lifetime = 0.9f;
        private const float RiseHeight = 0.6f;
        private const float FadeFrom = 0.6f;
        // 그림 픽셀 하나(PPU 8)
        private const float Pixel = 0.125f;
        // 값 글자는 데미지 숫자의 몇 배인가 — 2배는 너무 커서 1배(가로·세로 절반 = 넓이 1/4, 사용자 10/15)
        private const int GlyphScale = 1;
        // 뜨는 자리 흩뜨림(월드 칸) — 가로 · 세로 ±
        private const float JitterX = 0.4f;
        private const float JitterY = 0.25f;
        // 글자 칸 = 3×5 + 사방 1픽셀 테두리, 간격 4픽셀(테두리끼리 겹친다) — DamageNumbers와 같다
        private const int CellW = 5;
        private const int CellH = 7;
        private const int Advance = 4;
        private const int FontSize = 12;
        // TextMesh는 characterSize 1에서 fontSize 10 = 1칸이다 — 폰트 1px = 그림 1px이 되게
        private const float CharacterSize = Pixel * 10f;
        private const int IconPixels = 8;
        private const int SortingOrder = 210;   // 데미지 숫자(200) 위

        // 숫자 그림은 DamageNumbers.Glyphs, 그 뒤에 이 다섯(위에서 아래로 3칸씩)
        private const string Extra = "+-%×.";
        private static readonly string[] ExtraGlyphs =
        {
            "000010111010000", "000000111000000", "101001010100101", "000101010101000", "000000000000010",
        };

        private class Entry
        {
            public GameObject Root;
            public SpriteRenderer Icon;
            // 아이콘의 검은 테두리 — 같은 그림을 8방향으로 1px씩 밀어 뒤에 깐다(값 글자의 테두리와 같은 두께)
            public readonly SpriteRenderer[] IconOutline = new SpriteRenderer[8];
            public TextMesh Name;
            public TextMesh NameShadow;
            public MeshRenderer NameRenderer;
            public readonly List<SpriteRenderer> Glyphs = new List<SpriteRenderer>();
            public Transform Value;
            public int GlyphCount;
            public float NameX;
            public float ValueWidth;
            // 전체 폭 — 켜진 첫 프레임에 이름 폭을 읽어 정한다(-1 = 아직)
            public float Width;
            public Vector2 At;
            // 흩뜨리기 전 자리 · 지금 글(합칠 때 같은 글인지 본다)
            public Vector2 Origin;
            public BuffLabel Label;
            public float Side;
            public float Age;
        }

        // 같은 종류의 글이 이 거리(칸) 안의 같은 자리에 아직 떠 있으면 새로 띄우지 않고 값을 합친다(처치마다 · 발동마다 반복되는 버프 — 총괄 10/20).
        // 합친 글은 사라지기 시작하기 전 나이로 되돌려 반복이 이어지는 동안 하나만 남는다
        private const float MergeDistance = 1f;

        private static readonly Vector2[] OutlineDirs =
        {
            new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1), new Vector2(-1, 0),
            new Vector2(1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1),
        };

        private static BuffTexts _instance;

        private Font _font;
        private Sprite[] _icons;
        private Sprite[] _glyphs;
        private readonly List<Entry> _active = new List<Entry>();
        private readonly Stack<Entry> _free = new Stack<Entry>();

        // icons = BuffIcon 순서(None 자리는 null)
        public void Init(Font font, Sprite[] icons)
        {
            _instance = this;
            _font = font;
            _icons = icons;
            _glyphs = BuildGlyphs();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 선이 대상에 닿는 때(delay — 보통 BuffLinkShape.ArriveTime, 층이면 그 층이 튀는 때 HitRingShape.EffectDelay) 뜨도록 나이를 그만큼 음수에서 시작한다
        // side = 0이면 at 가운데 위 · +1이면 at에서 오른쪽으로 · -1이면 at에서 왼쪽으로(탑 층 · 타워가 대상일 때 탑을 가리지 않게)
        public static void Show(Vector2 at, BuffLabel label, float side = 0f, float delay = BuffLinkShape.ArriveTime)
        {
            if (_instance != null && _instance._font != null && label.Text != null)
                _instance.Add(at, label, side, delay);
        }

        private static int GlyphIndex(char c) => c >= '0' && c <= '9' ? c - '0' : Extra.IndexOf(c) is int i && i >= 0 ? 10 + i : -1;

        private void Add(Vector2 at, BuffLabel label, float side, float delay)
        {
            foreach (Entry old in _active)
                if (old.Side == side && old.Label.Icon == label.Icon && (old.Origin - at).sqrMagnitude < MergeDistance * MergeDistance
                    && Merge(old.Label.Text, label.Text, out string merged))
                {
                    old.Label = new BuffLabel { Icon = label.Icon, Text = merged };
                    Fill(old, old.Label);
                    old.Age = Mathf.Min(old.Age, Lifetime * FadeFrom);
                    return;
                }

            Entry e = _free.Count > 0 ? _free.Pop() : New();
            Fill(e, label);
            // 뜨는 자리를 조금씩 흩뜨린다 — 여러 개가 한 자리에 겹쳐 뜨지 않게(사용자 10/15)
            e.Origin = at;
            e.Label = label;
            e.At = at + new Vector2(Random.Range(-JitterX, JitterX), Random.Range(-JitterY, JitterY));
            e.Side = side;
            e.Age = -delay;
            e.Root.SetActive(false);
            _active.Add(e);
        }

        // 「이름 값」 둘을 합친다 — 이름이 같고 값의 꼴(+N · +N% · ×N)이 같을 때만. + · - 는 더하고 ×는 곱한다
        private static bool Merge(string a, string b, out string merged)
        {
            merged = null;
            if (a == null || b == null)
                return false;
            int sa = a.LastIndexOf(' '), sb = b.LastIndexOf(' ');
            string nameA = sa >= 0 ? a.Substring(0, sa + 1) : "", nameB = sb >= 0 ? b.Substring(0, sb + 1) : "";
            string va = a.Substring(sa + 1), vb = b.Substring(sb + 1);
            if (nameA != nameB || va.Length == 0 || vb.Length == 0 || va.EndsWith("%") != vb.EndsWith("%"))
                return false;
            string suffix = va.EndsWith("%") ? "%" : "";
            bool timesA = va[0] == '×', timesB = vb[0] == '×';
            if (timesA != timesB
                || !float.TryParse(va.Substring(timesA ? 1 : 0).TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(vb.Substring(timesB ? 1 : 0).TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                return false;
            string value = timesA ? "×" + (x * y).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                : (x + y).ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture) + suffix;
            merged = nameA + value;
            return true;
        }

        // 아이콘 · 이름 · 값 글자를 label로 채운다(자리 · 나이는 그대로)
        private void Fill(Entry e, BuffLabel label)
        {
            // 「공격속도 +5%」 → 이름 「공격속도」 + 값 「+5%」. 값만 도트 숫자로 그린다
            int split = label.Text.LastIndexOf(' ');
            string name = split >= 0 ? label.Text.Substring(0, split) : "";
            string value = split >= 0 ? label.Text.Substring(split + 1) : label.Text;

            float x = 0f;
            Sprite icon = _icons[(int)label.Icon];
            e.Icon.sprite = icon;
            e.Icon.enabled = icon != null;
            for (int k = 0; k < e.IconOutline.Length; k++)
            {
                e.IconOutline[k].sprite = icon;
                e.IconOutline[k].enabled = icon != null;
                // 아이콘 안쪽 좌표 — 그림 1px = 그림 폭 ÷ 8
                if (icon != null)
                    e.IconOutline[k].transform.localPosition = (Vector3)(OutlineDirs[k] * (icon.bounds.size.x / IconPixels));
            }
            if (icon != null)
            {
                // 아이콘도 값 글자와 같은 배율 — 8px 그대로면 2배 글자 옆에서 작아 보인다
                e.Icon.transform.localScale = Vector3.one * (IconPixels * GlyphScale * Pixel / icon.bounds.size.x);
                e.Icon.transform.localPosition = new Vector3(IconPixels * GlyphScale * 0.5f * Pixel, 0f, 0f);
                x = (IconPixels + 1) * GlyphScale * Pixel;
            }

            e.Name.text = e.NameShadow.text = name;
            e.Name.gameObject.SetActive(name != "");
            e.NameShadow.gameObject.SetActive(name != "");
            e.Name.transform.localPosition = new Vector3(x, 0f, 0f);
            e.NameShadow.transform.localPosition = new Vector3(x + Pixel, -Pixel, 0.01f);
            e.NameX = x;

            // 값 글자는 Value 아래에 0부터 — 이름 폭은 켜진 뒤에야 읽히므로 Value 자리는 LateUpdate가 정한다
            e.GlyphCount = 0;
            float step = Advance * GlyphScale * Pixel;
            float gx = 0f;
            foreach (char c in value)
            {
                int g = GlyphIndex(c);
                if (g < 0)
                    continue;
                if (e.GlyphCount == e.Glyphs.Count)
                    e.Glyphs.Add(NewGlyph(e.Value));
                SpriteRenderer glyph = e.Glyphs[e.GlyphCount++];
                glyph.sprite = _glyphs[g];
                glyph.transform.localPosition = new Vector3(gx, -CellH * GlyphScale * Pixel * 0.5f, 0f);
                glyph.gameObject.SetActive(true);
                gx += step;
            }
            for (int i = e.GlyphCount; i < e.Glyphs.Count; i++)
                e.Glyphs[i].gameObject.SetActive(false);
            // 마지막 글자는 간격이 아니라 칸 폭만큼
            e.ValueWidth = e.GlyphCount > 0 ? gx - step + CellW * GlyphScale * Pixel : 0f;
            e.Width = -1f;
        }

        private Entry New()
        {
            var e = new Entry { Root = new GameObject("BuffText") };
            e.Root.transform.SetParent(transform, false);
            e.Icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
            e.Icon.transform.SetParent(e.Root.transform, false);
            e.Icon.sortingOrder = SortingOrder;
            for (int k = 0; k < e.IconOutline.Length; k++)
            {
                e.IconOutline[k] = new GameObject("IconOutline").AddComponent<SpriteRenderer>();
                e.IconOutline[k].transform.SetParent(e.Icon.transform, false);
                e.IconOutline[k].sortingOrder = SortingOrder - 1;
            }
            e.NameShadow = Text(e.Root.transform, Color.black, SortingOrder - 1);
            e.Name = Text(e.Root.transform, Color.white, SortingOrder);
            e.NameRenderer = e.Name.GetComponent<MeshRenderer>();
            e.Value = new GameObject("Value").transform;
            e.Value.SetParent(e.Root.transform, false);
            return e;
        }

        private static SpriteRenderer NewGlyph(Transform parent)
        {
            var glyph = new GameObject("Glyph").AddComponent<SpriteRenderer>();
            glyph.transform.SetParent(parent, false);
            glyph.sortingOrder = SortingOrder;
            return glyph;
        }

        private TextMesh Text(Transform parent, Color color, int order)
        {
            var text = new GameObject("Text").AddComponent<TextMesh>();
            text.transform.SetParent(parent, false);
            text.font = _font;
            text.fontSize = FontSize;
            text.characterSize = CharacterSize;
            text.anchor = TextAnchor.MiddleLeft;
            text.color = color;
            var meshRenderer = text.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _font.material;
            meshRenderer.sortingOrder = order;
            // 도트 폰트 — 번지지 않게
            if (_font.material.mainTexture != null)
                _font.material.mainTexture.filterMode = FilterMode.Point;
            return text;
        }

        private void LateUpdate()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Entry e = _active[i];
                e.Age += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                if (e.Age >= Lifetime)
                {
                    e.Root.SetActive(false);
                    _active.RemoveAt(i);
                    _free.Push(e);
                    continue;
                }
                if (e.Age < 0f)
                    continue;
                if (!e.Root.activeSelf)
                    e.Root.SetActive(true);
                if (e.Width < 0f)
                {
                    float nameW = e.Name.gameObject.activeSelf ? e.NameRenderer.bounds.size.x + 2 * Pixel : 0f;
                    e.Value.localPosition = new Vector3(Snap(e.NameX + nameW), 0f, 0f);
                    e.Width = e.NameX + nameW + e.ValueWidth;
                }
                float t = e.Age / Lifetime;
                float rise = RiseHeight * (1f - (1f - t) * (1f - t));
                float alpha = t < FadeFrom ? 1f : 1f - (t - FadeFrom) / (1f - FadeFrom);
                float left = e.Side > 0f ? e.At.x : e.Side < 0f ? e.At.x - e.Width : e.At.x - e.Width * 0.5f;
                // 층 옆이면 층 높이에서, 유닛·타워면 그 위에서 떠오른다
                float lift = e.Side != 0f ? 0f : 0.5f;
                e.Root.transform.position = new Vector3(Snap(left), Snap(e.At.y + lift + rise), 0f);
                var white = new Color(1f, 1f, 1f, alpha);
                e.Icon.color = white;
                foreach (SpriteRenderer outline in e.IconOutline)
                    outline.color = new Color(0f, 0f, 0f, alpha);
                e.Name.color = white;
                e.NameShadow.color = new Color(0f, 0f, 0f, alpha);
                for (int g = 0; g < e.GlyphCount; g++)
                    e.Glyphs[g].color = white;
            }
        }

        // 그림 픽셀 격자에 맞춘다 — 도트가 뭉개지지 않게
        private static float Snap(float value) => Mathf.Round(value / Pixel) * Pixel;

        // 흰 글자 + 검은 테두리 — DamageNumbers.BuildFont와 같은 그림(숫자 10개 + Extra), 글자마다 스프라이트 하나(피벗 왼쪽 아래)
        private static Sprite[] BuildGlyphs()
        {
            int count = 10 + Extra.Length;
            int texW = CellW * count;
            var pixels = new Color32[texW * CellH];
            var outline = new Color32(0, 0, 0, 255);
            var fill = new Color32(255, 255, 255, 255);
            for (int g = 0; g < count; g++)
            {
                string glyph = g < 10 ? DamageNumbers.Glyphs[g] : ExtraGlyphs[g - 10];
                for (int pass = 0; pass < 2; pass++)
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                        {
                            if (glyph[row * 3 + col] != '1')
                                continue;
                            int cx = g * CellW + 1 + col;
                            int cy = CellH - 2 - row;
                            if (pass == 1)
                            {
                                pixels[cy * texW + cx] = fill;
                                continue;
                            }
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                    pixels[(cy + dy) * texW + cx + dx] = outline;
                        }
            }

            var texture = new Texture2D(texW, CellH, TextureFormat.RGBA32, false)
            {
                name = "BuffTextsFont",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprites = new Sprite[count];
            for (int g = 0; g < count; g++)
                sprites[g] = Sprite.Create(texture, new Rect(g * CellW, 0, CellW, CellH), Vector2.zero, 8f / GlyphScale);
            return sprites;
        }
    }
}

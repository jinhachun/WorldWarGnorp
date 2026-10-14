using UnityEngine;

namespace GnorpWar
{
    // 탑 한 층이 지금 받고 있는 효과 하나 — 아이콘 아래에 짧은 숫자(3×5 픽셀 글꼴). 받는 게 없으면 통째로 숨는다.
    // Tower가 층마다 만든다(가속 = 남은 초 「0.4」 · 쿨다운 = 곱해진 값 「×0.8」)
    public class TowerBadge
    {
        private const int PixelsPerUnit = 8;
        private const float Pixel = 1f / PixelsPerUnit;
        // 글꼴 번호 — 0~9는 숫자
        private const int Dot = 10;
        private const int Times = 11;
        private const int MaxGlyphs = 4;
        // 아이콘(8×8) 아래 끝에서 숫자 윗줄까지 2px
        private const int IconBottom = 0;
        private const int LabelBottom = -7;

        private readonly GameObject _root;
        private readonly SpriteRenderer[] _glyphs = new SpriteRenderer[MaxGlyphs];
        private readonly Sprite[] _font;
        private readonly int[] _codes = new int[MaxGlyphs];
        private int _count;
        private int _shownKey = -1;
        // 숫자 아랫줄 — 아이콘이 있으면 아이콘 아래, 없으면 아이콘 자리(수를 세는 기물의 남은 수)
        private readonly int _labelBottom;

        // parent = 층 · x = 층 가운데에서 표시 가운데까지(칸) · sortLike = 정렬을 따라갈 그림(층 판)
        public TowerBadge(Transform parent, string name, float x, Sprite icon, Sprite[] font, Color color, SpriteRenderer sortLike)
        {
            _font = font;
            _root = new GameObject(name);
            _root.transform.SetParent(parent, false);
            _root.transform.localPosition = new Vector3(x, 0f, -0.05f);

            SpriteRenderer iconRenderer = Add("Icon", color, sortLike);
            iconRenderer.sprite = icon;
            if (icon != null)
            {
                // 8×8 그림 = 1칸
                iconRenderer.transform.localScale = Vector3.one / icon.bounds.size.x;
                iconRenderer.transform.localPosition = new Vector3(-0.5f * Pixel, IconBottom * Pixel + 0.5f, 0f);
            }
            for (int i = 0; i < MaxGlyphs; i++)
                _glyphs[i] = Add("Glyph", color, sortLike);
            _labelBottom = icon != null ? LabelBottom : IconBottom + 2;
            _root.SetActive(false);
        }

        private SpriteRenderer Add(string name, Color color, SpriteRenderer sortLike)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_root.transform, false);
            renderer.color = color;
            renderer.sortingLayerID = sortLike.sortingLayerID;
            renderer.sortingOrder = sortLike.sortingOrder;
            return renderer;
        }

        // 남은 시간 — 「0.4」(10초부터는 「12」). 0이면 숨는다
        public void ShowSeconds(float seconds)
        {
            int tenths = Mathf.RoundToInt(seconds * 10f);
            if (tenths <= 0)
                Set(0);
            else if (tenths < 100)
                Set(3, tenths / 10, Dot, tenths % 10);
            else
            {
                int whole = Mathf.Min(99, tenths / 10);
                Set(2, whole / 10, whole % 10);
            }
        }

        // 곱해진 값 — 「×0.8」, 소수 둘째 자리가 있으면 층 벽에 들어가게 「×.64」. 1이면 숨는다
        public void ShowScale(float scale)
        {
            int hundredths = Mathf.Clamp(Mathf.RoundToInt(scale * 100f), 1, 999);
            if (hundredths == 100)
                Set(0);
            else if (hundredths % 10 == 0 || hundredths > 100)
                Set(4, Times, hundredths / 100, Dot, Mathf.Min(9, Mathf.RoundToInt(hundredths % 100 / 10f)));
            else
                Set(4, Times, Dot, hundredths / 10, hundredths % 10);
        }

        // 남은 수 — 「17」(최대 999). 0이면 숨는다. 아이콘 없는 표시(수를 세는 기물)라 숫자를 아이콘 줄에 올린다
        public void ShowCount(int count)
        {
            count = Mathf.Min(count, 999);
            if (count <= 0)
                Set(0);
            else if (count < 10)
                Set(1, count);
            else if (count < 100)
                Set(2, count / 10, count % 10);
            else
                Set(3, count / 100, count / 10 % 10, count % 10);
        }

        // 숫자 없이 아이콘만 — 남은 시간이 없는 효과(오버드라이브 번개)
        public void ShowIcon(bool on)
        {
            int key = on ? -2 : 0;
            if (key == _shownKey)
                return;
            _shownKey = key;
            _root.SetActive(on);
            foreach (SpriteRenderer glyph in _glyphs)
                glyph.enabled = false;
        }

        private void Set(int count, int a = 0, int b = 0, int c = 0, int d = 0)
        {
            int key = count == 0 ? 0 : (((a * 16 + b) * 16 + c) * 16 + d) * 8 + count;
            if (key == _shownKey)
                return;
            _shownKey = key;
            _root.SetActive(count > 0);
            if (count == 0)
                return;
            _codes[0] = a;
            _codes[1] = b;
            _codes[2] = c;
            _codes[3] = d;
            _count = count;

            // 글자 사이 1px — 전체를 가운데에(폭이 늘 홀수 px라, 표시 가운데가 반 픽셀 자리면 격자에 맞는다)
            int width = count - 1;
            for (int i = 0; i < count; i++)
                width += Width(_codes[i]);
            float x = -width * 0.5f;
            for (int i = 0; i < MaxGlyphs; i++)
            {
                _glyphs[i].enabled = i < count;
                if (i >= count)
                    continue;
                _glyphs[i].sprite = _font[_codes[i]];
                _glyphs[i].transform.localPosition = new Vector3(x * Pixel, _labelBottom * Pixel, 0f);
                x += Width(_codes[i]) + 1;
            }
        }

        private static int Width(int code) => code == Dot ? 1 : 3;

        // 3×5 픽셀 글꼴(숫자 그림은 DamageNumbers와 같은 것) + 점(폭 1) + 곱하기. 피벗 = 왼쪽 아래
        public static Sprite[] BuildFont()
        {
            const int cell = 3, rows = 5, count = 12;
            var texture = new Texture2D(cell * count, rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var sprites = new Sprite[count];
            for (int g = 0; g < count; g++)
            {
                string glyph = g == Dot ? "000000000000100" : g == Times ? "000101010101000" : DamageNumbers.Glyphs[g];
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < cell; col++)
                        texture.SetPixel(g * cell + col, rows - 1 - row, glyph[row * cell + col] == '1' ? Color.white : Color.clear);
                sprites[g] = Sprite.Create(texture, new Rect(g * cell, 0, Width(g), rows), Vector2.zero, PixelsPerUnit);
            }
            texture.Apply();
            return sprites;
        }
    }
}

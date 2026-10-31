using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 목숨을 잃은 하트가 깨지는 순간(손맛 J3 — Docs/폴리싱_계획.md) — 새 그림 없이: 그 하트 그림을 가운데에서 두 쪽으로 잘라(잘린 자리 1칸이 금)
    // 양옆으로 1칸 벌어졌다가 튀어 오르며 떨어지고, 작은 네모 조각 몇 개가 흩어진다. 칸(UiHitRings.Pixel) 단위로 움직이고 끝에 계단식으로 사라진다.
    // 결과 창(timeScale 0)에서도 돌게 실제 시간(Juice.RealDeltaTime). 길이는 BattleConfig 「손맛 — 상점 · UI」
    public class HeartBreak : MonoBehaviour
    {
        private const float Pixel = UiHitRings.Pixel;
        // 금이 간 채 머무는 시간 · 두 쪽의 첫 속도(px/초) · 중력 · 조각 수와 속도
        private const float CrackSeconds = 0.12f;
        private const float HalfSpeedX = 60f, HalfSpeedY = 180f, Gravity = 900f;
        private const int Bits = 6;
        private const float BitSpeed = 240f;

        private RectTransform[] _parts;
        private Vector2[] _velocity;
        private Vector2[] _offset;
        private Graphic[] _graphics;
        private float _age;
        private float _seconds;

        // heart = 깨질 하트(그 자리 · 그림 · 크기를 읽는다). 연출은 캔버스 맨 위 — 줄 밖으로 떨어져도 잘리지 않게
        public static void Play(Image heart, Color color)
        {
            Canvas canvas = heart.canvas != null ? heart.canvas.rootCanvas : null;
            if (canvas == null || heart.sprite == null)
                return;
            var root = (RectTransform)new GameObject("HeartBreak", typeof(RectTransform)).transform;
            root.SetParent(canvas.transform, false);
            root.position = heart.rectTransform.TransformPoint(heart.rectTransform.rect.center);
            root.sizeDelta = Vector2.zero;
            root.gameObject.AddComponent<HeartBreak>().Build(heart, color);
            // 소리 자리(J6): 하트 깨짐
        }

        private void Build(Image heart, Color color)
        {
            BattleConfig config = RunState.Config;
            _seconds = config != null ? Mathf.Max(0.05f, config.HeartBreakSeconds) : 0.9f;
            Vector2 size = heart.rectTransform.rect.size;
            // 그림 칸 수 — 가운데 칸(홀수면)은 금으로 비운다
            int columns = Mathf.Max(2, Mathf.RoundToInt(size.x / Pixel));
            int left = columns / 2;
            int right = columns - left - (columns % 2);
            _parts = new RectTransform[2 + Bits];
            _velocity = new Vector2[_parts.Length];
            _offset = new Vector2[_parts.Length];
            _graphics = new Graphic[_parts.Length];
            for (int side = 0; side < 2; side++)
            {
                // 반쪽 = 그 폭만 보이는 가림 칸 + 그 안에 하트 그림 전체
                float width = (side == 0 ? left : right) * Pixel;
                var mask = (RectTransform)new GameObject(side == 0 ? "Left" : "Right", typeof(RectTransform), typeof(RectMask2D)).transform;
                mask.SetParent(transform, false);
                mask.pivot = new Vector2(side == 0 ? 0f : 1f, 0.5f);
                mask.sizeDelta = new Vector2(width, size.y);
                mask.anchoredPosition = new Vector2(side == 0 ? -size.x * 0.5f : size.x * 0.5f, 0f);
                var image = new GameObject("Heart", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.raycastTarget = false;
                image.sprite = heart.sprite;
                image.preserveAspect = true;
                image.color = color;
                image.rectTransform.SetParent(mask, false);
                image.rectTransform.pivot = mask.pivot;
                image.rectTransform.sizeDelta = size;
                image.rectTransform.anchoredPosition = Vector2.zero;
                _parts[side] = mask;
                _graphics[side] = image;
                float dir = side == 0 ? -1f : 1f;
                _velocity[side] = new Vector2(dir * HalfSpeedX, HalfSpeedY);
            }
            for (int i = 0; i < Bits; i++)
            {
                var bit = new GameObject("Bit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bit.raycastTarget = false;
                bit.color = color;
                bit.rectTransform.SetParent(transform, false);
                bit.rectTransform.sizeDelta = Vector2.one * Pixel;
                float angle = Mathf.PI * (0.15f + 0.7f * i / (Bits - 1));   // 위쪽 반원으로 흩어진다
                _parts[2 + i] = bit.rectTransform;
                _graphics[2 + i] = bit;
                _velocity[2 + i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BitSpeed * Random.Range(0.7f, 1.1f);
                bit.enabled = false;   // 금이 가는 순간에 튄다
            }
        }

        private void Update()
        {
            float dt = Juice.RealDeltaTime;
            _age += dt;
            if (_age >= _seconds)
            {
                Destroy(gameObject);
                return;
            }
            bool cracked = _age >= CrackSeconds;
            // 마지막 3분의 1 동안 3단으로 흐려진다
            float k = _age / _seconds;
            float alpha = k < 2f / 3f ? 1f : k < 5f / 6f ? 2f / 3f : 1f / 3f;
            for (int i = 0; i < _parts.Length; i++)
            {
                bool half = i < 2;
                if (!half && _graphics[i].enabled != cracked)
                    _graphics[i].enabled = cracked;
                if (cracked)
                {
                    _velocity[i].y -= Gravity * dt;
                    _offset[i] += _velocity[i] * dt;
                }
                // 반쪽은 금이 가는 순간 양옆으로 1칸 벌어진 채 머문다
                Vector2 split = half ? new Vector2(i == 0 ? -Pixel : Pixel, 0f) : Vector2.zero;
                Vector2 at = split + _offset[i];
                Vector2 basePos = half ? new Vector2(i == 0 ? -1f : 1f, 0f) * ((RectTransform)_graphics[i].transform).sizeDelta.x * 0.5f : Vector2.zero;
                _parts[i].anchoredPosition = basePos + new Vector2(Mathf.Round(at.x / Pixel), Mathf.Round(at.y / Pixel)) * Pixel;
                Color c = _graphics[i].color;
                c.a = alpha;
                _graphics[i].color = c;
            }
        }
    }
}

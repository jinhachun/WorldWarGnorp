using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // UI에 서 있는 유닛 그림 — 국가 선택 무대(UI/NationCard)와 상점 짐꾼(UI/OfferCard)이 같이 쓴다
    public static class UnitFigure
    {
        // 유닛 프리팹의 그림 부품(몸 · 말)을 같은 배치로 UI에 — 그림 1px = 화면 k px(정수배), 부품들의 왼쪽 아래 = into의 왼쪽 아래.
        // paint = 열쇠 색(파란 자리)을 칠할 색(Core/LordPaint), 그리는 순서는 정렬 순서대로. 돌려주는 값 = 그린 크기(px)
        public static Vector2 Draw(Unit prefab, RectTransform into, int k, Color paint)
        {
            var parts = new List<SpriteRenderer>();
            foreach (SpriteRenderer part in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                // 무기는 뺀다 — 프리팹의 무기 자리는 전투 중 휘두를 때의 자리라 서 있는 줄에서는 몸과 떨어져 떠 보인다
                if (part.sprite != null && part.enabled && part.gameObject.activeSelf && part.name != "Weapon")
                    parts.Add(part);
            parts.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            if (parts.Count == 0)
                return Vector2.zero;
            // 부품들을 그림 px로 — 자리(프리팹 안 · px) · 크기(px)
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (SpriteRenderer part in parts)
            {
                Sprite sprite = part.sprite;
                Vector2 scale = part.transform.localScale;
                Vector2 at = (Vector2)part.transform.localPosition * sprite.pixelsPerUnit;
                Vector2 lo = at - Vector2.Scale(sprite.pivot, scale), hi = lo + Vector2.Scale(sprite.rect.size, scale);
                min = Vector2.Min(min, lo);
                max = Vector2.Max(max, hi);
            }
            float canvasPpu = into.GetComponentInParent<Canvas>(true).referencePixelsPerUnit;
            foreach (SpriteRenderer part in parts)
            {
                Sprite sprite = part.sprite;
                var image = new GameObject(part.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(into, false);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
                image.sprite = LordPaint.Of(sprite, paint);
                image.color = part.color;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.SetNativeSize();
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                // 원본 크기(SetNativeSize)에서 정수배 k로 — 캔버스 기준은 그림 1px = 100 ÷ PPU
                Vector2 scale = part.transform.localScale;
                float toK = k * sprite.pixelsPerUnit / canvasPpu;
                image.rectTransform.localScale = new Vector3(scale.x * toK, scale.y * toK, 1f);
                image.rectTransform.anchoredPosition = ((Vector2)part.transform.localPosition * sprite.pixelsPerUnit - min) * k;
            }
            return (max - min) * k;
        }

        // 걸음 한 장면 — 가로로 offset(px)만큼 간 자리를 그림 1px(= k px) 단위로 끊고, 걷는 중이면 홀수 걸음마다 1px 들썩인다
        public static Vector2 Step(float offset, int k, bool moving)
        {
            int pixel = Mathf.RoundToInt(offset / k);
            return new Vector2(pixel * k, moving && (pixel & 1) == 1 ? k : 0f);
        }
    }
}

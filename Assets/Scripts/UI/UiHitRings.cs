using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // UI 탑의 맞음 표시(사용자 10/26 A안) — 층 → 층 버프 선이 닿은 칸에 안쪽 섬광 → 창틀 바깥 효과 색 링이 퍼진다. 모양·시간은 전장 탑과 같은 계산(Fx/HitRingShape).
    // 그림 없이 메시로 — 탑 그림의 픽셀 한 칸(3px) 격자. 층들 위에 그린다(마지막 형제). 같은 칸은 RepeatGap 안이면 링만. BoardView가 탑마다 하나 만든다
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiHitRings : Graphic
    {
        // 탑 그림 픽셀 한 칸 — 칸이 튀는 거리도 이 단위(BoardView)
        public const float Pixel = 3f;

        private class Hit
        {
            public int Slot;
            public int X, Y, W, H;
            public Color Color;
            public HitRingMode Mode;
            public float Age;
        }

        private readonly List<Hit> _hits = new List<Hit>();
        private readonly List<HitRect> _rects = new List<HitRect>();

        // box = 이 Graphic 기준 칸 네모(px). weak = 늘 켜진 효과의 재알림. 보통 표시(섬광 · 튐)면 true
        public bool Add(int slot, Rect box, Color color, bool weak)
        {
            HitRingMode mode = weak ? HitRingMode.Weak : HitRingMode.Full;
            foreach (Hit h in _hits)
                if (h.Slot == slot && h.Age < HitRingShape.RepeatGap && mode == HitRingMode.Full)
                    mode = HitRingMode.RingOnly;
            _hits.Add(new Hit
            {
                Slot = slot,
                X = Mathf.RoundToInt(box.xMin / Pixel),
                Y = Mathf.RoundToInt(box.yMin / Pixel),
                W = Mathf.RoundToInt(box.width / Pixel),
                H = Mathf.RoundToInt(box.height / Pixel),
                Color = color,
                Mode = mode,
            });
            SetVerticesDirty();
            return mode == HitRingMode.Full;
        }

        public void Clear()
        {
            if (_hits.Count == 0)
                return;
            _hits.Clear();
            SetVerticesDirty();
        }

        // 꺼진 동안은 나이를 못 먹는다 — 남겨 두면 다시 켜질 때 옛 표시가 뜬다
        protected override void OnDisable()
        {
            base.OnDisable();
            _hits.Clear();
        }

        private void Update()
        {
            if (_hits.Count == 0)
                return;
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                // 배속과 상관없이 같은 길이로
                _hits[i].Age += Time.unscaledDeltaTime;
                if (_hits[i].Age >= HitRingShape.End)
                    _hits.RemoveAt(i);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (Hit hit in _hits)
            {
                HitRingShape.Rects(_rects, hit.X, hit.Y, hit.W, hit.H, hit.Age, hit.Mode);
                foreach (HitRect r in _rects)
                {
                    Color tint = r.White ? Color.white : hit.Color;
                    Color32 color = new Color(tint.r, tint.g, tint.b, r.Alpha);
                    Vector2 min = new Vector2(r.X, r.Y) * Pixel, max = new Vector2(r.X + r.W, r.Y + r.H) * Pixel;
                    int v = vh.currentVertCount;
                    vh.AddVert(min, color, Vector2.zero);
                    vh.AddVert(new Vector2(min.x, max.y), color, Vector2.zero);
                    vh.AddVert(max, color, Vector2.zero);
                    vh.AddVert(new Vector2(max.x, min.y), color, Vector2.zero);
                    vh.AddTriangle(v, v + 1, v + 2);
                    vh.AddTriangle(v, v + 2, v + 3);
                }
            }
        }
    }
}

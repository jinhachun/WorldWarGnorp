using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // UI 탑의 층 → 층 버프 선(BuffLinks 알림) — 탑 옆으로 불룩한 곡선을 머리가 출발에서 대상으로 빠르게 지나가고, 가늘고 옅어지는 꼬리가 따라 빨려 든다.
    // 모양·시간은 전장 선과 같은 계산(BuffLinkShape — 선마다 씨앗이 달라 굴곡이 조금씩 다르다).
    // 그림 없이 메시로 그린다 — 탑 그림의 픽셀 한 칸(3px) 격자에 맞춘 네모를 곡선 따라 찍는다. BoardView가 탑마다 하나 만든다
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiBuffLinkLines : Graphic
    {
        // 탑 그림 픽셀 한 칸
        private const float Pixel = 3f;
        // 같은 선을 이 시간 안에 다시 받으면 건너뛴다(알림은 건 횟수만큼 온다)
        private const float RepeatGap = 0.15f;
        // 불룩함 하한·상한(px)
        private const float BulgeMin = 24f;
        private const float BulgeMax = 96f;

        private class Line
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public Color Color;
            public bool Weak;
            public int Key;
            public float Age;
        }

        private readonly List<Line> _lines = new List<Line>();

        // from·to = 이 Graphic 기준 자리, side = 불룩해지는 쪽(+1 오른쪽 · -1 왼쪽)
        public void Add(Vector2 from, Vector2 to, float side, BuffKind kind, bool weak, int key)
        {
            foreach (Line existing in _lines)
                if (existing.Key == key && existing.Age < RepeatGap)
                    return;
            // 색은 전장 선과 같게
            var line = new Line { Color = BuffLinkFx.ColorOf(kind), Weak = weak, Key = key };
            // 점 간격은 격자 반 칸 — 격자 한 칸을 건너뛰지 않게
            BuffLinkShape.Fill(line.Points, from, to, BuffLinkShape.NewSeed(), new Vector2(side, 0f), BulgeMin, BulgeMax, Pixel * 0.5f);
            _lines.Add(line);
            SetVerticesDirty();
        }

        public void Clear()
        {
            if (_lines.Count == 0)
                return;
            _lines.Clear();
            SetVerticesDirty();
        }

        // 꺼진 동안은 나이를 못 먹는다 — 남겨 두면 다시 켜질 때(상점이 다시 열릴 때) 옛 선이 뜬다
        protected override void OnDisable()
        {
            base.OnDisable();
            _lines.Clear();
        }

        private void Update()
        {
            if (_lines.Count == 0)
                return;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                // 배속과 상관없이 같은 길이로
                _lines[i].Age += Time.unscaledDeltaTime;
                if (_lines[i].Age >= BuffLinkShape.Lifetime)
                    _lines.RemoveAt(i);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (Line line in _lines)
            {
                BuffLinkShape.Visible(line.Age, out float tail, out float head);
                int last = line.Points.Count - 1;
                Vector2 lastCell = new Vector2(float.NaN, 0f);
                int lastPixels = 0;
                for (int i = 0; i <= last; i++)
                {
                    float t = (float)i / last;
                    // 네모 한 변 = 굵기(그림 픽셀 수) × 3px — 머리 9px → 꼬리 3px
                    int pixels = BuffLinkShape.Pixels(t, tail, head, line.Weak);
                    if (pixels == 0)
                        continue;
                    // 점이 네모 가운데 — 왼쪽 아래 칸을 격자에 물린다(전장 선과 같은 법)
                    Vector2 point = line.Points[i] / Pixel;
                    Vector2 cell = new Vector2(Mathf.Round(point.x - pixels * 0.5f), Mathf.Round(point.y - pixels * 0.5f));
                    if (cell == lastCell && pixels == lastPixels)
                        continue;
                    lastCell = cell;
                    lastPixels = pixels;
                    Vector2 min = cell * Pixel;
                    float size = pixels * Pixel;
                    Color32 color = new Color(line.Color.r, line.Color.g, line.Color.b, BuffLinkShape.Alpha(t, tail, head, line.Weak));
                    int v = vh.currentVertCount;
                    vh.AddVert(min, color, Vector2.zero);
                    vh.AddVert(min + new Vector2(0f, size), color, Vector2.zero);
                    vh.AddVert(min + new Vector2(size, size), color, Vector2.zero);
                    vh.AddVert(min + new Vector2(size, 0f), color, Vector2.zero);
                    vh.AddTriangle(v, v + 1, v + 2);
                    vh.AddTriangle(v, v + 2, v + 3);
                }
            }
        }
    }
}

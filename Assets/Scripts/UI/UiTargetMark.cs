using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // UI 탑 층의 대상 표시 — 툴팁을 띄운 기물의 효과를 받는 층(BuildingTargets)의 네 모서리 노란 꺾쇠(모양은 Fx/TargetMarkShape).
    // 그림 없이 메시로 — 탑 그림의 픽셀 한 칸(3px) 격자. SlotView가 처음 필요할 때 자기 자식으로 만든다(칸에 꽉 차게 · 맨 위 · 레이캐스트 끔)
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiTargetMark : Graphic
    {
        private const float Pixel = UiHitRings.Pixel;
        // 칸 테두리에서 안쪽으로 1칸 · 굵기 2칸 · 팔 7칸(층 40칸 중)
        private const int Inset = 1;
        private const int Thick = 2;
        private const int Arm = 7;

        private readonly List<HitRect> _rects = new List<HitRect>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // 검은 받침을 먼저(사방 1칸 크게), 노란 꺾쇠를 그 위에 — 흰 벽 · 벽돌 위에서도 읽히게(전장 유닛 꺾쇠 Fx/HoverMark와 같은 법)
            Draw(vh, Inset - 1, Thick + 2, Arm + 2, Color.black);
            Draw(vh, Inset, Thick, Arm, TargetMarkShape.Color);
        }

        private void Draw(VertexHelper vh, int inset, int thick, int arm, Color32 c)
        {
            Rect box = rectTransform.rect;
            TargetMarkShape.Rects(_rects, Mathf.RoundToInt(box.xMin / Pixel), Mathf.RoundToInt(box.yMin / Pixel),
                Mathf.RoundToInt(box.width / Pixel), Mathf.RoundToInt(box.height / Pixel), inset, thick, arm);
            foreach (HitRect r in _rects)
            {
                Vector2 min = new Vector2(r.X, r.Y) * Pixel, max = new Vector2(r.X + r.W, r.Y + r.H) * Pixel;
                int v = vh.currentVertCount;
                vh.AddVert(min, c, Vector2.zero);
                vh.AddVert(new Vector2(min.x, max.y), c, Vector2.zero);
                vh.AddVert(max, c, Vector2.zero);
                vh.AddVert(new Vector2(max.x, min.y), c, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v, v + 2, v + 3);
            }
        }
    }
}

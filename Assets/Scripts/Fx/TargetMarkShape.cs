using System.Collections.Generic;

namespace GnorpWar
{
    // 대상 표시 모양 한 곳(사용자 10/27 「더 바자르처럼」) — 툴팁을 띄운 기물의 효과를 받는 층의 네 모서리에 노란 꺾쇠(ㄱ자).
    // 버프 선의 맞음 표시(HitRingShape — 효과 색 링이 바깥으로 퍼짐)와 겹치지 않게 모양(모서리만 · 안쪽 · 가만히)으로 가른다.
    // 좌표는 격자 칸(전장 = 그림 1px · UI = 3px). UI 탑(UI/UiTargetMark)과 전장 탑(Fx/TargetMarkFx)이 같이 쓴다
    public static class TargetMarkShape
    {
        // 꺾쇠 색 — 숫자 노랑과 같은 값(사용자 10/30 「대상 나타내는 꺽쇠가 노란색이었으면」). 버프 링(효과 색)과는 모양으로 가른다
        public static readonly UnityEngine.Color32 Color = UnityEngine.ColorUtility.TryParseHtmlString(BuildingTooltip.NumberColor, out UnityEngine.Color c) ? (UnityEngine.Color32)c : (UnityEngine.Color32)UnityEngine.Color.yellow;

        // box = 층 네모(격자 칸 (x0, y0)부터 w×h). inset = 테두리에서 안쪽으로 들인 칸 · thick = 굵기 · arm = 꺾쇠 팔 길이(굵기 포함)
        public static void Rects(List<HitRect> rects, int x0, int y0, int w, int h, int inset, int thick, int arm)
        {
            rects.Clear();
            int left = x0 + inset, bottom = y0 + inset, right = x0 + w - inset, top = y0 + h - inset;
            Corner(rects, left, bottom, 1, 1, thick, arm);
            Corner(rects, right, bottom, -1, 1, thick, arm);
            Corner(rects, left, top, 1, -1, thick, arm);
            Corner(rects, right, top, -1, -1, thick, arm);
        }

        // (x, y) = 모서리 꼭짓점, (dx, dy) = 안쪽 방향 — 가로 팔 하나 + 세로 팔 하나(겹치는 칸 없이)
        private static void Corner(List<HitRect> rects, int x, int y, int dx, int dy, int thick, int arm)
        {
            int hx = dx > 0 ? x : x - arm, hy = dy > 0 ? y : y - thick;
            rects.Add(new HitRect { X = hx, Y = hy, W = arm, H = thick, Alpha = 1f });
            int vx = dx > 0 ? x : x - thick, vy = dy > 0 ? y + thick : y - arm;
            rects.Add(new HitRect { X = vx, Y = vy, W = thick, H = arm - thick, Alpha = 1f });
        }
    }
}

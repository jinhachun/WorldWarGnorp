using UnityEngine;

namespace GnorpWar
{
    // 지도 씬 화면 테두리(UI_WorldFrame — 화면 판 Canvas/Screen 1920×1080) 막대의 자리. 화면 판 왼쪽 위 기준 px, 아래로 +. 막대 굵기 6px.
    // 테두리 그림을 바꾸면 여기만 고친다 — 막대 옆에 자리를 코드로 정하는 UI(왼쪽 패널 · 탑 기둥의 탑 · 국가 고르기의 탑)가 여기서 읽는다.
    // 여백 규칙(Docs/UI_스타일가이드_초안.md §4): 막대 안쪽 끝 → 글 · 아이콘 · 버튼은 Pad 이상, 큰 판(탑 · 지도 · 판 배경)은 겹치지 않게 맞닿음
    public static class FrameBars
    {
        public const float Bar = 6f;
        // 막대의 첫 픽셀 — 바깥 막대(화면 가장자리에서) · 탑 기둥 세로선 · 패널|지도 칸막이
        public const float Outer = 8f;
        public const float Pillar = 276f;
        public const float PanelEdge = 409f;
        // 막대 → 글 · 아이콘 최소 여백
        public const float Pad = 16f;

        // 정보 기둥(바깥 막대 ~ 탑 기둥 선)의 안쪽 — 왼쪽 끝 · 가운데 · 폭
        public const float InfoLeft = Outer + Bar;
        public const float InfoCenter = (InfoLeft + Pillar) * 0.5f;
        public const float InfoWidth = Pillar - InfoLeft;
        // 탑 기둥(세로선 ~ 칸막이)의 가운데 — 정수 px로(도트가 반 픽셀에 걸리지 않게)
        public static float TowerCenter => Mathf.Floor((Pillar + Bar + PanelEdge) * 0.5f);
        // 바닥 막대의 위 끝(화면 판 높이 1080) — 탑은 여기에 선다
        public const float Floor = 1080f - Outer - Bar;
    }
}

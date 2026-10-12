using UnityEngine;

namespace GnorpWar
{
    // 맵 때문에 작동이 꺼진 기물(늪지 — 쿨다운 없는 기물, BoardRunner.StoppedByMap)의 층에 뜨는 빨간 정지 표시(사용자 10/12).
    // 가속 표시와 같은 자리·같은 크기, 숫자 없이 아이콘만 — 그 층의 그림은 회색이 된다.
    // 전장 탑(Tower의 TowerBadge)과 UI 탑 칸(SlotView)이 이 색을 같이 쓴다
    public static class StoppedMark
    {
        // 표시 색 — 적 기물 이름의 빨강(BuildingTooltip)
        public static readonly Color Color = new Color32(0xFF, 0x7A, 0x6E, 0xFF);
        // 꺼진 층의 벽 · 창틀 · 기물 아이콘에 곱하는 회색
        public static readonly Color Gray = new Color(0.4f, 0.4f, 0.4f);
    }
}

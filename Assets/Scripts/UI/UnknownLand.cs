namespace GnorpWar
{
    // 미지의 땅(사용자 10/26) — 이벤트 땅은 방문 장면이 열리기 전까지 정체를 숨긴다(지도 칸 · 툴팁 · 왼쪽 패널 = 「?」 + 이 이름 · 한 줄).
    // 방문 장면이 열리면(RunState.CurrentVisit) 드러나고, 닫힌 뒤에는 무엇이었는지 흐리게 보인다. 화면이 판정한다 — 규칙(RunState)은 그대로
    public static class UnknownLand
    {
        public const string Name = "미지의 땅";
        public const string Line = "주인 없는 땅입니다. 무엇이 있는지는 가 봐야 압니다.";

        // 아직 정체를 모르는 이벤트 땅인가 — 닫히지 않았고, 지금 열린 방문 장면의 땅도 아니다
        public static bool Hidden(int region)
        {
            WorldState state = RunState.World.State;
            return state.Graph.IsEvent[region] && !state.EventClosed[region]
                && !(RunState.CurrentVisit != null && RunState.CurrentVisit.Region == region);
        }
    }
}

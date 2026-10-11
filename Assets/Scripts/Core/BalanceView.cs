namespace GnorpWar
{
    // 밸런싱 창(Editor/BalanceWindow)이 데이터 에셋을 그리는 방식 — 그 창이 자기 인스펙터를 그리는 동안만 켠다.
    // 데이터 클래스의 [HideIf]가 이 값을 읽는다. 게임은 읽지 않는다
    public static class BalanceView
    {
        // 그림 · 프리팹 · 표시용 글 · 색 · 연출 값처럼 게임 결과를 바꾸지 않는 칸을 숨긴다
        public static bool HideNonBalance;
    }
}

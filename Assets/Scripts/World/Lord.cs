namespace GnorpWar
{
    // 영주 하나 — 월드맵 규칙이 쓰는 값만 든다(탑은 영주 번호로 따로 든다). 번호(Id)는 WorldState.Lords의 칸이고 런 동안 다시 쓰이지 않는다
    public class Lord
    {
        public int Id;
        // 빌드 방향 번호(WorldGraph.Builds의 값 중 하나 — 에셋은 WorldMapDefinition.BuildOf) — 시작 지역의 지형이 가진 방향들 중에서 뽑히고, 땅을 먹어도 바뀌지 않는다
        public int Build;
        public LordTemper Temper;
        public int Happiness;
        public bool IsPlayer;
        // 땅을 다 잃었거나 반란으로 멸망하면 false — 땅은 0
        public bool Alive = true;
        // 마지막 침략을 이겼나 — 다음 턴 수입의 승리 보너스가 읽는다
        public bool WonLastInvasion;
    }
}

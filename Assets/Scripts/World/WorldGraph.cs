namespace GnorpWar
{
    // 월드맵 한 장의 모양 — 지역과 길, 지역마다의 지형·업그레이드·시작 영주의 빌드 방향을 번호로만 든다(에셋은 WorldMapDefinition이 이것으로 바꿔 준다).
    // UnityEngine을 쓰지 않는다 — 규칙(WorldTurn)을 Unity 밖에서 수백 판 돌려 검증할 수 있게
    public class WorldGraph
    {
        // [지역] = 이어진 지역들
        public readonly int[][] Links;
        public readonly int[] Terrain;
        public readonly int[] Upgrade;
        public readonly int[] Build;

        public int RegionCount => Links.Length;

        public WorldGraph(int[][] links, int[] terrain, int[] upgrade, int[] build)
        {
            Links = links;
            Terrain = terrain;
            Upgrade = upgrade;
            Build = build;
        }
    }
}

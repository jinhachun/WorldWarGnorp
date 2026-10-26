namespace GnorpWar
{
    // 월드맵 한 장의 모양 — 지역과 길, 지역마다의 지형·시작 영주의 빌드 방향을 번호로만 든다(에셋은 WorldMapDefinition이 이것으로 바꿔 준다).
    // 지역 번호 = 지도의 땅 번호(지도 그림 · 표시 자리와 같은 번호). 이벤트 땅(아웃게임기획 §3-1)이 있으면 그 칸은 영주 판도 밖이다 —
    // 길(Links)은 영주 땅끼리만 잇고, 이벤트 땅의 Links는 비어 있다. 이벤트 땅이 맞댄 영주 땅은 EventBorders에 따로 든다(WithEvents).
    // UnityEngine을 쓰지 않는다 — 규칙(WorldTurn)을 Unity 밖에서 수백 판 돌려 검증할 수 있게
    public class WorldGraph
    {
        // [지역] = 이어진 지역들(영주 땅끼리)
        public readonly int[][] Links;
        public readonly int[] Terrain;
        // [지역] = 그 땅에서 시작하는 영주가 가질 수 있는 빌드 방향 번호들 — 영주마다 그중 하나를 고르게 뽑는다(WorldState.AddLord). 지형 번호와 같은 수가 아니다
        public readonly int[][] Builds;
        // [지역] = 이벤트 땅인가 · [이벤트 땅] = 국경을 맞댄 영주 땅들(영주 땅은 빈 칸)
        public readonly bool[] IsEvent;
        public readonly int[][] EventBorders;

        public int RegionCount => Links.Length;
        // 영주 땅 수 — 통일 = 이만큼을 다 가짐
        public int LordRegionCount { get; }

        public WorldGraph(int[][] links, int[] terrain, int[][] builds)
            : this(links, terrain, builds, new bool[links.Length], null)
        {
        }

        private WorldGraph(int[][] links, int[] terrain, int[][] builds, bool[] isEvent, int[][] eventBorders)
        {
            Links = links;
            Terrain = terrain;
            Builds = builds;
            IsEvent = isEvent;
            EventBorders = eventBorders ?? System.Array.ConvertAll(links, _ => new int[0]);
            foreach (bool e in isEvent)
                if (!e)
                    LordRegionCount++;
        }

        // events 칸을 이벤트 땅으로 뺀 새 그래프 — 영주 땅끼리의 길만 남기고, 이벤트 땅이 맞댄 영주 땅을 EventBorders에 옮긴다(지도에 고정된 이벤트 땅 — WorldMapDefinition.ToGraph)
        public WorldGraph WithEvents(int[] events)
        {
            int count = RegionCount;
            var isEvent = new bool[count];
            foreach (int e in events)
                isEvent[e] = true;
            var links = new int[count][];
            var borders = new int[count][];
            for (int region = 0; region < count; region++)
            {
                int[] lords = System.Array.FindAll(Links[region], next => !isEvent[next]);
                links[region] = isEvent[region] ? new int[0] : lords;
                borders[region] = isEvent[region] ? lords : new int[0];
            }
            return new WorldGraph(links, Terrain, Builds, isEvent, borders);
        }
    }
}

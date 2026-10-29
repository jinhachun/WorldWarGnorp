using UnityEngine;

namespace GnorpWar
{
    // 손으로 짠 월드맵 한 장(기획 §3) — 지역마다 지형 · 이어진 지역, 그리고 지형마다 그 땅 영주의 빌드 방향.
    // 땅의 모양은 그림 한 장(_regionMap — 칸마다 땅 번호)이고, 이어진 지역 · 표시 자리는 그 그림에서 뽑아 같이 구운 값이다
    // (메뉴 GnorpWar/월드 지도 굽기 — Editor/WorldMapBaker. 손으로 고치면 그림과 어긋난다).
    // 규칙(WorldTurn)은 에셋을 모른다 — ToGraph가 번호만 든 WorldGraph로 바꿔 준다
    [CreateAssetMenu(menuName = "GnorpWar/World Map")]
    public class WorldMapDefinition : ScriptableObject
    {
        // 땅 번호 지도의 칸 값 = (땅 번호 + 1) × 이 값, 바다는 0 — 그림으로 열어 봐도 땅이 갈려 보이게 띄운다. 칸 값은 255까지라 땅은 255 ÷ 이 값 − 1 = 41개까지(40칸 지도 — 10/25)
        public const int RegionMapStep = 6;
        public const int Sea = -1;

        [System.Serializable]
        public class Region
        {
            [Tooltip("이 땅을 침략하면 전투가 벌어지는 맵")]
            public MapDefinition Terrain;
            [Tooltip("국경을 맞댄 지역의 칸 번호(0부터) — 침략할 수 있는 이웃. 양쪽 지역에 서로 적는다")]
            public int[] Links = new int[0];
            [Tooltip("땅 번호 지도에서 이 땅의 아이콘 · 번호를 놓을 칸(위 왼쪽이 0, 0) — 경계에서 가장 먼 자리(띠는 가장 굵은 곳)")]
            public Vector2Int Anchor;
            [Tooltip("이벤트 땅(산맥 · 강 띠 — 영주 판도 밖, 자리 고정 · 종류는 런마다 섞는다). 굽기가 적는다")]
            public bool Event;
        }

        // 바다를 건너 이어진 두 땅(사용자 10/30) — 이웃(Links)에는 이미 들어 있고, 이것은 지도에 점선을 긋고 영주가 건너 걷는 길이다. 굽기가 적는다
        [System.Serializable]
        public class SeaRoute
        {
            public int A, B;
            [Tooltip("바다 칸들(위 왼쪽이 0, 0) — A 해안에 닿은 칸부터 B 해안에 닿은 칸까지")]
            public Vector2Int[] Path = new Vector2Int[0];
        }

        [System.Serializable]
        public class TerrainBuild
        {
            public MapDefinition Terrain;
            [Tooltip("이 지형의 땅에서 시작하는 영주(런 시작 · 반란 뒤 새 영주)의 빌드 방향")]
            public EnemyBuildProfile Build;
            [Tooltip("이 지형의 영주가 대신 가질 수 있는 다른 방향들 — 영주마다 위의 것과 이것들 중 하나를 고르게 뽑는다(얼음 · 초원의 트라이브)")]
            public EnemyBuildProfile[] MoreBuilds = new EnemyBuildProfile[0];
        }

        [Tooltip("땅 번호 지도 — 칸마다 그 칸이 속한 땅(읽기 켬 · 압축 없음 · Point). 화면의 지도는 이것을 정수배로 키워 영주 색으로 칠한다")]
        [SerializeField] private Texture2D _regionMap;
        [Tooltip("칸 번호 = 지역 번호 − 1")]
        [SerializeField] private Region[] _regions = new Region[0];
        [Tooltip("지형마다 한 줄 — 맵에 쓰인 지형이 모두 있어야 한다")]
        [SerializeField] private TerrainBuild[] _terrainBuilds = new TerrainBuild[0];
        [Tooltip("해로 — 굽기가 적는다(손으로 고치지 말 것)")]
        [SerializeField] private SeaRoute[] _seaRoutes = new SeaRoute[0];

        public int RegionCount => _regions.Length;
        public SeaRoute[] SeaRoutes => _seaRoutes;
        public MapDefinition TerrainOf(int region) => _regions[region].Terrain;
        public Vector2Int AnchorOf(int region) => _regions[region].Anchor;

        // 빌드 방향 번호(Lord.Build · WorldGraph.Builds) — 앞쪽은 지형 표의 줄 순서대로 그 줄의 첫 방향, 그 뒤로 줄 순서대로 「다른 방향들」.
        // 그래서 방향이 하나뿐인 지형은 번호가 지형 번호와 같지만, 번호로 지형을 읽지 말 것
        public int BuildCount
        {
            get
            {
                int count = _terrainBuilds.Length;
                foreach (TerrainBuild row in _terrainBuilds)
                    count += row.MoreBuilds.Length;
                return count;
            }
        }

        public EnemyBuildProfile BuildOf(int build)
        {
            if (build < _terrainBuilds.Length)
                return _terrainBuilds[build].Build;
            build -= _terrainBuilds.Length;
            foreach (TerrainBuild row in _terrainBuilds)
            {
                if (build < row.MoreBuilds.Length)
                    return row.MoreBuilds[build];
                build -= row.MoreBuilds.Length;
            }
            return null;
        }

        // 땅 번호 지도를 읽는다 — [y × width + x] = 땅 번호(바다는 Sea), 위 왼쪽이 0번 칸
        public int[] ReadCells(out int width, out int height)
        {
            width = _regionMap.width;
            height = _regionMap.height;
            Color32[] pixels = _regionMap.GetPixels32();
            var cells = new int[pixels.Length];
            // 텍스처는 아래 줄부터 든다
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells[y * width + x] = pixels[(height - 1 - y) * width + x].r / RegionMapStep - 1;
            return cells;
        }

        // 규칙 그래프 — 이벤트 땅(Region.Event)을 뺀 영주 땅끼리의 길(WorldGraph.WithEvents).
        // 지형 번호 = 지형 표(_terrainBuilds)의 줄, 빌드 방향 번호들 = 그 줄의 방향들(BuildOf의 번호)
        public WorldGraph ToGraph()
        {
            // [줄] = 그 지형의 방향 번호들 — 첫 방향(줄 번호) + 다른 방향들(줄 수 뒤로 이어 센다)
            var rowBuilds = new int[_terrainBuilds.Length][];
            int next = _terrainBuilds.Length;
            for (int row = 0; row < rowBuilds.Length; row++)
            {
                rowBuilds[row] = new int[1 + _terrainBuilds[row].MoreBuilds.Length];
                rowBuilds[row][0] = row;
                for (int k = 1; k < rowBuilds[row].Length; k++)
                    rowBuilds[row][k] = next++;
            }
            int count = _regions.Length;
            var links = new int[count][];
            var terrain = new int[count];
            var builds = new int[count][];
            for (int i = 0; i < count; i++)
            {
                Region region = _regions[i];
                links[i] = (int[])region.Links.Clone();
                terrain[i] = System.Array.FindIndex(_terrainBuilds, row => row.Terrain == region.Terrain);
                builds[i] = rowBuilds[terrain[i]];
            }
            var events = new System.Collections.Generic.List<int>();
            for (int i = 0; i < count; i++)
                if (_regions[i].Event)
                    events.Add(i);
            return new WorldGraph(links, terrain, builds).WithEvents(events.ToArray());
        }
    }
}

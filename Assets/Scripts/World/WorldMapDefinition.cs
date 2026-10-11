using UnityEngine;

namespace GnorpWar
{
    // 손으로 짠 월드맵 한 장(기획 §3) — 지역마다 지형 · 업그레이드 · 이어진 지역, 그리고 지형마다 그 땅 영주의 빌드 방향.
    // 땅의 모양은 그림 한 장(_regionMap — 칸마다 땅 번호)이고, 이어진 지역 · 표시 자리는 그 그림에서 뽑아 같이 구운 값이다
    // (메뉴 GnorpWar/월드 지도 굽기 — Editor/WorldMapBaker. 손으로 고치면 그림과 어긋난다).
    // 규칙(WorldTurn)은 에셋을 모른다 — ToGraph가 번호만 든 WorldGraph로 바꿔 준다
    [CreateAssetMenu(menuName = "GnorpWar/World Map")]
    public class WorldMapDefinition : ScriptableObject
    {
        // 땅 번호 지도의 칸 값 = (땅 번호 + 1) × 이 값, 바다는 0 — 그림으로 열어 봐도 땅이 갈려 보이게 띄운다
        public const int RegionMapStep = 8;
        public const int Sea = -1;

        [System.Serializable]
        public class Region
        {
            [Tooltip("이 땅을 침략하면 전투가 벌어지는 맵")]
            public MapDefinition Terrain;
            public RegionUpgradeDefinition Upgrade;
            [Tooltip("국경을 맞댄 지역의 칸 번호(0부터) — 침략할 수 있는 이웃. 양쪽 지역에 서로 적는다")]
            public int[] Links = new int[0];
            [Tooltip("땅 번호 지도에서 이 땅의 아이콘 · 번호를 놓을 칸(위 왼쪽이 0, 0) — 경계에서 가장 먼 자리")]
            public Vector2Int Anchor;
        }

        [System.Serializable]
        public class TerrainBuild
        {
            public MapDefinition Terrain;
            [Tooltip("이 지형의 땅에서 시작하는 영주(런 시작 · 반란 뒤 새 영주)의 빌드 방향")]
            public EnemyBuildProfile Build;
        }

        [Tooltip("땅 번호 지도 — 칸마다 그 칸이 속한 땅(읽기 켬 · 압축 없음 · Point). 화면의 지도는 이것을 정수배로 키워 영주 색으로 칠한다")]
        [SerializeField] private Texture2D _regionMap;
        [Tooltip("칸 번호 = 지역 번호 − 1")]
        [SerializeField] private Region[] _regions = new Region[0];
        [Tooltip("지형마다 한 줄 — 맵에 쓰인 지형이 모두 있어야 한다")]
        [SerializeField] private TerrainBuild[] _terrainBuilds = new TerrainBuild[0];

        public int RegionCount => _regions.Length;
        public MapDefinition TerrainOf(int region) => _regions[region].Terrain;
        public RegionUpgradeDefinition UpgradeOf(int region) => _regions[region].Upgrade;
        public Vector2Int AnchorOf(int region) => _regions[region].Anchor;

        // 그 종류의 업그레이드 에셋 — 이 맵에 없으면 null
        public RegionUpgradeDefinition UpgradeOfKind(RegionUpgradeKind kind)
        {
            foreach (Region region in _regions)
                if (region.Upgrade != null && region.Upgrade.Kind == kind)
                    return region.Upgrade;
            return null;
        }
        // build = WorldGraph.Build · Lord.Build의 번호
        public EnemyBuildProfile BuildOf(int build) => _terrainBuilds[build].Build;

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

        // 지형 번호 · 빌드 방향 번호 = 지형 표(_terrainBuilds)의 칸, 업그레이드 번호 = RegionUpgradeKind
        public WorldGraph ToGraph()
        {
            int count = _regions.Length;
            var links = new int[count][];
            int[] terrain = new int[count], upgrade = new int[count], build = new int[count];
            for (int i = 0; i < count; i++)
            {
                Region region = _regions[i];
                links[i] = (int[])region.Links.Clone();
                terrain[i] = build[i] = System.Array.FindIndex(_terrainBuilds, row => row.Terrain == region.Terrain);
                upgrade[i] = (int)region.Upgrade.Kind;
            }
            return new WorldGraph(links, terrain, upgrade, build);
        }
    }
}

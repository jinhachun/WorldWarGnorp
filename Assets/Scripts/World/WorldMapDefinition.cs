using UnityEngine;

namespace GnorpWar
{
    // 손으로 짠 월드맵 한 장(기획 §3) — 지역마다 지형 · 업그레이드 · 이어진 지역, 그리고 지형마다 그 땅 영주의 빌드 방향.
    // 규칙(WorldTurn)은 에셋을 모른다 — ToGraph가 번호만 든 WorldGraph로 바꿔 준다
    [CreateAssetMenu(menuName = "GnorpWar/World Map")]
    public class WorldMapDefinition : ScriptableObject
    {
        [System.Serializable]
        public class Region
        {
            [Tooltip("이 땅을 침략하면 전투가 벌어지는 맵")]
            public MapDefinition Terrain;
            public RegionUpgradeDefinition Upgrade;
            [Tooltip("길로 이어진 지역의 칸 번호(0부터) — 길은 양쪽 지역에 서로 적는다")]
            public int[] Links = new int[0];
        }

        [System.Serializable]
        public class TerrainBuild
        {
            public MapDefinition Terrain;
            [Tooltip("이 지형의 땅에서 시작하는 영주(런 시작 · 반란 뒤 새 영주)의 빌드 방향")]
            public EnemyBuildProfile Build;
        }

        [Tooltip("칸 번호 = 지역 번호 − 1")]
        [SerializeField] private Region[] _regions = new Region[0];
        [Tooltip("지형마다 한 줄 — 맵에 쓰인 지형이 모두 있어야 한다")]
        [SerializeField] private TerrainBuild[] _terrainBuilds = new TerrainBuild[0];

        public int RegionCount => _regions.Length;
        public MapDefinition TerrainOf(int region) => _regions[region].Terrain;
        public RegionUpgradeDefinition UpgradeOf(int region) => _regions[region].Upgrade;
        // build = WorldGraph.Build · Lord.Build의 번호
        public EnemyBuildProfile BuildOf(int build) => _terrainBuilds[build].Build;

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

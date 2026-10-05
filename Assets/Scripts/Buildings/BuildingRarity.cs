namespace GnorpWar
{
    // 건물 등급 — 건물 종류마다 하나. 가격과 상점 출현 확률이 등급에서 나온다(BattleConfig)
    public enum BuildingRarity { Common, Uncommon, Rare, Epic, Legendary }

    public static class BuildingRarityNames
    {
        private static readonly string[] Names = { "일반", "고급", "희귀", "영웅", "전설" };
        // 기물 테두리 색(사용자 결정) — 회색 · 초록 · 파랑 · 보라 · 주황
        private static readonly UnityEngine.Color[] Colors =
        {
            new UnityEngine.Color(0.62f, 0.62f, 0.62f),
            new UnityEngine.Color(0.30f, 0.78f, 0.35f),
            new UnityEngine.Color(0.30f, 0.55f, 1.00f),
            new UnityEngine.Color(0.68f, 0.35f, 0.95f),
            new UnityEngine.Color(1.00f, 0.60f, 0.15f),
        };

        public static string DisplayName(this BuildingRarity rarity) => Names[(int)rarity];
        public static UnityEngine.Color FrameColor(this BuildingRarity rarity) => Colors[(int)rarity];
    }
}

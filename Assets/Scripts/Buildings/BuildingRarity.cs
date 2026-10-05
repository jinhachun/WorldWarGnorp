namespace GnorpWar
{
    // 건물 등급 — 건물 종류마다 하나. 가격과 상점 출현 확률이 등급에서 나온다(BattleConfig)
    public enum BuildingRarity { Common, Uncommon, Rare, Epic, Legendary }

    public static class BuildingRarityNames
    {
        private static readonly string[] Names = { "일반", "고급", "희귀", "영웅", "전설" };

        public static string DisplayName(this BuildingRarity rarity) => Names[(int)rarity];
    }
}

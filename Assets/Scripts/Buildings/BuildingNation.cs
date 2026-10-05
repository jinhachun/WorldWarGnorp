namespace GnorpWar
{
    // 기물의 국가·세력 — 기획서 §3. 직렬화되므로 맨 뒤에만 추가할 것
    public enum BuildingNation { Common, Kingdom, TickTock }

    public enum BuildingFaction { None, Papal, Royalist, Capitalist, Labor }

    public static class BuildingNationNames
    {
        private static readonly string[] Nations = { "공용", "킹덤", "티크톡" };
        private static readonly string[] Factions = { "", "교황파", "왕당파", "자본가파", "노동자파" };

        public static string DisplayName(this BuildingNation nation) => Nations[(int)nation];
        public static string DisplayName(this BuildingFaction faction) => Factions[(int)faction];
    }
}

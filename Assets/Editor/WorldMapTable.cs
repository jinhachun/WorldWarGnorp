namespace GnorpWar.Editor
{
    // 손으로 짠 월드맵 한 장의 표(기획 §3 배치 예시 · §4 「30지역에 나눠 붙이기」) — WorldAssetsMenu가 이것으로 에셋을 만든다.
    // 6칸 × 5줄, 지역 번호는 왼쪽 위 01부터 오른쪽으로 — 땅의 모양과 이웃은 이 자리를 흐트려 그린 지도(WorldMapBaker)가 정한다.
    // UnityEngine을 쓰지 않는다(Unity 밖 검산에서도 같은 표를 읽는다)
    internal static class WorldMapTable
    {
        public const int Columns = 6;

        // 지형 글자 — TerrainLetters의 칸이 지형 번호: I 얼음 · F 숲 · P 기본 · G 초원 · S 늪지 · J 정글
        public const string TerrainLetters = "IFPGSJ";
        public static readonly string[] TerrainRows =
        {
            "IIIIFF",
            "IFFFFG",
            "PPPGGG",
            "SSPPGJ",
            "SSSJJJ",
        };

        // 업그레이드 숫자 = RegionUpgradeKind(0 무기 · 1 방어구 · 2 보급력 · 3 기술 연구 · 4 세금 증산 · 5 민족주의)
        public static readonly string[] UpgradeRows =
        {
            "012345",
            "234501",
            "450123",
            "012345",
            "234501",
        };

        public static int RegionCount => TerrainRows.Length * Columns;
        public static int TerrainOf(int region) => TerrainLetters.IndexOf(TerrainRows[region / Columns][region % Columns]);
        public static int UpgradeOf(int region) => UpgradeRows[region / Columns][region % Columns] - '0';
    }
}

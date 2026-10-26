namespace GnorpWar.Editor
{
    // 월드맵 한 장의 표 — WorldAssetsMenu가 이것으로 에셋을 만들고, 지도를 구울 때 땅마다 지형을 적는다.
    // 8칸 × 5줄 = 40칸(영주 땅 30 + 이벤트 땅 10 — 어느 칸이 이벤트 땅인지는 런마다 뽑는다, 아웃게임기획 §3-1). 지역 번호는 왼쪽 위 01부터 오른쪽으로 —
    // 땅의 모양과 이웃은 지도(WorldMapBaker)가 정하고, 그 땅들을 위에서부터 다섯 줄 × 왼쪽부터로 나눠 이 표의 칸에 번호를 맞춘다(10/25 — 땅이 자라는 방식).
    // 지형은 임시 배치(지금 6×5의 기후 띠를 옆으로 늘린 것 — 얼음 6 · 숲 7 · 기본 7 · 초원 7 · 늪지 6 · 정글 7, 사용자 10/25 「시트의 임시 배치 그대로」).
    // UnityEngine을 쓰지 않는다(Unity 밖 검산에서도 같은 표를 읽는다)
    internal static class WorldMapTable
    {
        public const int Columns = 8;

        // 지형 글자 — TerrainLetters의 칸이 지형 번호: I 얼음 · F 숲 · P 기본 · G 초원 · S 늪지 · J 정글
        public const string TerrainLetters = "IFPGSJ";
        public static readonly string[] TerrainRows =
        {
            "IIIIFFFF",
            "IIFFFGGG",
            "PPPPGGGJ",
            "SSPPPGJJ",
            "SSSSJJJJ",
        };

        public static int RegionCount => TerrainRows.Length * Columns;
        public static int TerrainOf(int region) => TerrainLetters.IndexOf(TerrainRows[region / Columns][region % Columns]);
    }
}

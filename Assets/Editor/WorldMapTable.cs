using System.Collections.Generic;

namespace GnorpWar.Editor
{
    // 손으로 짠 월드맵 한 장의 표(기획 §3 배치 예시 · §4 「30지역에 나눠 붙이기」) — WorldAssetsMenu가 이것으로 에셋을 만든다.
    // 6칸 × 5줄, 지역 번호는 왼쪽 위 01부터 오른쪽으로. UnityEngine을 쓰지 않는다(Unity 밖 검산에서도 같은 표를 읽는다)
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

        // 가로 길은 전부 있고, 세로 길은 이 지역(번호 1부터)과 그 아래 지역 사이만 끊었다 — 02─08 · 10─16 · 13─19 · 14─20 · 23─29(산맥 · 강 자리)
        public static readonly int[] CutBelow = { 2, 10, 13, 14, 23 };

        public static int RegionCount => TerrainRows.Length * Columns;
        public static int TerrainOf(int region) => TerrainLetters.IndexOf(TerrainRows[region / Columns][region % Columns]);
        public static int UpgradeOf(int region) => UpgradeRows[region / Columns][region % Columns] - '0';

        // [지역(0부터)] = 이어진 지역들
        public static int[][] Links()
        {
            int count = RegionCount;
            var links = new List<int>[count];
            for (int i = 0; i < count; i++)
                links[i] = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (i % Columns < Columns - 1)
                    Link(links, i, i + 1);
                if (i + Columns < count && System.Array.IndexOf(CutBelow, i + 1) < 0)
                    Link(links, i, i + Columns);
            }
            var result = new int[count][];
            for (int i = 0; i < count; i++)
                result[i] = links[i].ToArray();
            return result;
        }

        private static void Link(List<int>[] links, int a, int b)
        {
            links[a].Add(b);
            links[b].Add(a);
        }
    }
}

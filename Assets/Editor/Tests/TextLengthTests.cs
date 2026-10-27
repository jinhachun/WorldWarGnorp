using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 화면 설명 글이 한눈에 읽히는가(CLAUDE.md §5-2-2, 사용자 10/28 「텍스트가 너무 길다」) — NoParenthesesTests와 같은 글을 모아
    // 줄 수 · 줄 길이(색 태그를 뺀 글자 수, 띄어쓰기 포함)의 상한을 본다. 목표는 한 줄 20자 안팎 · 3줄 이하이고, 상한은 넘으면 툴팁에서 한 줄이 두 줄로 감기는 길이다
    public class TextLengthTests
    {
        private const int MaxLines = 4;    // 반군 막사 신화 = 생산 1 + 스탯 2 + 신화 1
        private const int MaxChars = 40;   // 영악한 투자가 「전투 시작 시, 바로 위·아래의 「구매할 때마다,」를 5회 발동시킵니다.」

        private static readonly Regex Tag = new Regex("<[^>]+>");
        private static BattleConfig Config => AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");

        private static void Check(List<string> bad, string where, string text)
        {
            string[] lines = Tag.Replace(text, "").Split('\n');
            if (lines.Length > MaxLines)
                bad.Add($"{where}: {lines.Length}줄 — {string.Join(" / ", lines)}");
            foreach (string line in lines)
                if (line.Length > MaxChars)
                    bad.Add($"{where}: {line.Length}자 — {line}");
        }

        [Test]
        public void ShopBuildings_Short()
        {
            var bad = new List<string>();
            BuildingDefinition[] pool = Config.ShopPool;
            Assert.Greater(pool.Length, 0);
            foreach (BuildingDefinition building in pool)
            {
                for (int level = 1; level <= 5; level++)
                    Check(bad, $"{building.name} {level}", BuildingTooltip.Describe(building, level, building.Cooldown));
                Check(bad, $"{building.name} 신화", BuildingTooltip.Describe(building, 5, building.Cooldown, true));
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void Upgrades_Short()
        {
            var bad = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:UpgradeDefinition", new[] { "Assets/Data" });
            Assert.Greater(guids.Length, 0);
            foreach (string guid in guids)
            {
                var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                for (BuildingRarity rarity = BuildingRarity.Common; rarity <= BuildingRarity.Legendary; rarity++)
                    Check(bad, $"{upgrade.name} {rarity}", upgrade.Describe(upgrade.ValueAt(rarity)));
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void EventLands_Short()
        {
            var bad = new List<string>();
            int count = 0;
            foreach (EventLandKind kind in System.Enum.GetValues(typeof(EventLandKind)))
                if (Config.World.EventOf(kind) is EventLandDefinition definition)
                {
                    count++;
                    Check(bad, kind.ToString(), definition.Describe("1", "1", "1", "1"));
                }
            Assert.Greater(count, 0);
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}

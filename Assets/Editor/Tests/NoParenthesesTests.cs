using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 화면에 보이는 설명 글에 괄호가 없는가(CLAUDE.md §5-2-7, 사용자 10/28 「지저분하다」) —
    // 상점 풀 모든 기물의 등급 1~5 · 신화 · 업그레이드(합치기 보상 · 지역 특수 · 지형 성소) 등급별 · 이벤트 땅 글
    public class NoParenthesesTests
    {
        private static BattleConfig Config => AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");

        private static void Check(List<string> bad, string where, string text)
        {
            if (text.Contains("(") || text.Contains(")"))
                bad.Add($"{where}: {text}");
        }

        [Test]
        public void ShopBuildings_NoParentheses()
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
        public void Upgrades_NoParentheses()
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
        public void EventLands_NoParentheses()
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

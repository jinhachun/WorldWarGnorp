using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 일반 등급 기물 4종(사용자 10/28) — 수호의 종 · 기사 견습소 · 동전 주조기 · 감시 망원경.
    // 등급마다 글(= 값)이 바뀌는가 · 한 줄인가 · 괄호가 없는가(§5-2-7) · 판매가 계산. 전장 동작은 플레이 전투로 확인한다
    public class CommonBuildingTests
    {
        private static readonly string[] Names = { "Building_GuardianBell", "Building_KnightSchool", "Building_CoinMint", "Building_Telescope" };

        private static BuildingDefinition Building(string name)
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/Data/Buildings/{name}.asset");
            Assert.NotNull(building, name);
            return building;
        }

        private static string Text(BuildingDefinition building, int level, bool mythic = false)
            => BuildingTooltip.Describe(building, level, building.Cooldown, mythic);

        [Test]
        public void EveryRankChangesText_OneLine_NoParentheses()
        {
            foreach (string name in Names)
            {
                BuildingDefinition building = Building(name);
                Assert.AreEqual(BuildingRarity.Common, building.Rarity, name);
                for (int level = 1; level <= 5; level++)
                {
                    string text = Text(building, level);
                    Assert.IsFalse(text.Contains("\n"), $"{name} {level}: 일반 기물은 한 줄 — {text}");
                    Assert.IsFalse(text.Contains("(") || text.Contains(")"), $"{name} {level}: 괄호 — {text}");
                    if (level > 1)
                        Assert.AreNotEqual(Text(building, level - 1), text, $"{name}: {level - 1} → {level} 등급에 바뀌는 것이 없다");
                }
                string mythic = Text(building, 5, true);
                Assert.AreNotEqual(Text(building, 5), mythic, $"{name}: 신화 줄이 없다");
                Assert.IsFalse(mythic.Contains("(") || mythic.Contains(")"), $"{name} 신화: 괄호 — {mythic}");
            }
        }

        [Test]
        public void KnightSchool_NeedsEightToFour()
        {
            var effect = AssetDatabase.LoadAssetAtPath<SummonCountSummonEffect>("Assets/Data/Effects/Effect_KnightSchool.asset");
            CollectionAssert.AreEqual(new[] { "8기", "7기", "6기", "5기", "4기" },
                new[] { effect.Headline(1), effect.Headline(2), effect.Headline(3), effect.Headline(4), effect.Headline(5) });
            var melee = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Unit_Melee.asset");
            var cog = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Unit_Cog.asset");
            Assert.IsTrue(effect.AppliesFor(Team.Ally, melee));
            Assert.IsFalse(effect.AppliesFor(Team.Ally, cog), "유닛 이름 병사만 — 톱니병사로 바뀌어 나온 것은 세지 않는다");
        }

        [Test]
        public void GuardianBell_TwoToFourSeconds_FixedCooldown()
        {
            var action = AssetDatabase.LoadAssetAtPath<InvulnerableAllAction>("Assets/Data/Buildings/Actions/Action_GuardianBell.asset");
            CollectionAssert.AreEqual(new[] { 2f, 2.5f, 3f, 3.5f, 4f },
                new[] { action.SecondsAt(1), action.SecondsAt(2), action.SecondsAt(3), action.SecondsAt(4), action.SecondsAt(5) });
            BuildingDefinition bell = Building("Building_GuardianBell");
            Assert.AreEqual(25f, bell.CooldownAt(1));
            Assert.AreEqual(25f, bell.CooldownAt(5), "생산 기물이 아니다 — 등급이 쿨다운을 줄이지 않는다");
        }

        [Test]
        public void Telescope_TenPercentPlusFive()
        {
            var passive = AssetDatabase.LoadAssetAtPath<TowerAndUnitRangePassive>("Assets/Data/Buildings/Passives/Passive_Telescope.asset");
            Assert.AreEqual(0.10f, passive.PercentAt(1), 1e-5f);
            Assert.AreEqual(0.30f, passive.PercentAt(5), 1e-5f);
            StringAssert.Contains("내 타워들과 궁병의 사거리 +10%", passive.Describe(1));
        }

        [Test]
        public void CoinMint_StacksAddToSellValue()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            RunState.StartNew(config);
            BuildingDefinition mint = Building("Building_CoinMint");
            var a = new OwnedBuilding(mint);
            a.AddSpent(4);
            int plain = RunState.SellValue(a);
            a.AddStacks(3);
            Assert.AreEqual(plain + 3, RunState.SellValue(a), "판매가 = 쓴 골드 × 비율 + 쌓인 몫");
        }
    }
}

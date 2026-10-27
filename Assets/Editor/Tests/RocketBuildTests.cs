using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 티크톡 「톱니로켓병」 빌드 기물 4종(사용자 10/28) — 등급 값 · 사격 통제소의 비율 공유 · 총동원 개조령의 마지막 변환 규칙.
    // 전투(땅 · 보드)가 없어도 도는 계산만 본다 — 전장 동작은 플레이 전투로 확인한다
    public class RocketBuildTests
    {
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.NotNull(asset, path);
            return asset;
        }

        private static UnitDefinition Unit(string name) => Load<UnitDefinition>($"Assets/Data/Units/{name}.asset");
        private static T Passive<T>(string name) where T : BuildingPassive => Load<T>($"Assets/Data/Buildings/Passives/{name}.asset");

        [SetUp]
        [TearDown]
        public void Clean()
        {
            TowerBook.ClearBattle();
            UnitAliases.Clear();
            TeamEffects.Clear();
        }

        [Test]
        public void RocketSoldier_IsArcherWithExplodingSplash()
        {
            UnitDefinition rocket = Unit("Unit_CogRocket");
            Assert.AreEqual(Unit("Unit_Ranged").Category, rocket.Category, "궁병만");
            Assert.AreNotEqual(Unit("Unit_Cog").Category, rocket.Category);
            Assert.Greater(rocket.ProjectileSplashRadius, 0f, "착탄 범위 피해");
            var explodes = new SerializedObject(rocket.Projectile).FindProperty("_explodes");
            Assert.IsTrue(explodes.boolValue, "폭발 연출");
        }

        [Test]
        public void Workshop_ConvertsOneMorePerRank()
        {
            var action = Load<ConvertUnitsAction>("Assets/Data/Buildings/Actions/Action_RocketWorkshop.asset");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, new[] { action.CountAt(1), action.CountAt(2), action.CountAt(3), action.CountAt(4) });
        }

        [Test]
        public void Recycling_SummonsTwoAtLegendary()
        {
            var passive = Passive<DeathCountSlotSummonPassive>("Passive_RecyclingPlant");
            Assert.AreEqual(1, passive.CountAt(1));
            Assert.AreEqual(2, passive.CountAt(2));
        }

        [Test]
        public void FireControl_RatioByRank()
        {
            var passive = Passive<TowerGainsToUnitsPassive>("Passive_FireControl");
            Assert.AreEqual(0.6f, passive.RatioAt(1), 1e-4f);
            Assert.AreEqual(0.8f, passive.RatioAt(2), 1e-4f);
            Assert.AreEqual(1.0f, passive.RatioAt(3), 1e-4f);
        }

        [Test]
        public void FireControl_SharesTowerGainsByRatio()
        {
            var passive = Passive<TowerGainsToUnitsPassive>("Passive_FireControl");
            UnitDefinition archer = Unit("Unit_Ranged");
            UnitDefinition cog = Unit("Unit_Cog");
            TowerBook.AddForBattle(Team.Ally, new TowerModifier(TowerStat.Attack, StatOp.Percent, 0.5f));
            TowerBook.AddForBattle(Team.Ally, new TowerModifier(TowerStat.AttackSpeed, StatOp.Multiply, 1.1f));
            TowerBook.AddForBattle(Team.Ally, new TowerModifier(TowerStat.Range, StatOp.Percent, 0.1f));
            TowerBook.ShareWithUnits(Team.Ally, passive, 0.6f);

            StatSum attack = StatSum.Identity;
            TowerBook.AccumulateShared(Team.Ally, archer, UnitStat.Attack, ref attack);
            Assert.AreEqual(0.3f, attack.Percent, 1e-4f, "+50%의 60%");
            StatSum speed = StatSum.Identity;
            TowerBook.AccumulateShared(Team.Ally, archer, UnitStat.AttackSpeed, ref speed);
            Assert.AreEqual(1.06f, speed.Multiply, 1e-4f, "×1.1의 늘어난 몫 60%");
            Assert.AreEqual(7f * 1.06f, TowerBook.SharedCompute(Team.Ally, archer, TowerStat.Range, 7f), 1e-4f);

            StatSum none = StatSum.Identity;
            TowerBook.AccumulateShared(Team.Ally, cog, UnitStat.Attack, ref none);
            Assert.AreEqual(0f, none.Percent, "궁병이 아니면 안 받는다");
            StatSum enemy = StatSum.Identity;
            TowerBook.AccumulateShared(Team.Enemy, archer, UnitStat.Attack, ref enemy);
            Assert.AreEqual(0f, enemy.Percent, "상대 진영은 안 받는다");
        }

        [Test]
        public void FireControl_WithArrowUpgradeMythic_AddsRatios()   // 화살 강화기 신화(100%)와 함께 — 기물마다 따로라 비율이 더해진다
        {
            var control = Passive<TowerGainsToUnitsPassive>("Passive_FireControl");
            var mythic = Passive<TowerEffectsToUnitsPassive>("Passive_ArrowUpgradeMythic");
            TowerBook.AddForBattle(Team.Ally, new TowerModifier(TowerStat.Attack, StatOp.Percent, 0.5f));
            TowerBook.ShareWithUnits(Team.Ally, mythic);
            TowerBook.ShareWithUnits(Team.Ally, control, 1f);
            StatSum attack = StatSum.Identity;
            TowerBook.AccumulateShared(Team.Ally, Unit("Unit_Ranged"), UnitStat.Attack, ref attack);
            Assert.AreEqual(1.0f, attack.Percent, 1e-4f, "+50% × (100% + 100%)");
        }

        [Test]
        public void FireControlMythic_ArcherKillsCountAsTower()
        {
            TowerBook.CountKillsAsTower(Team.Ally, Passive<UnitKillsAsTowerPassive>("Passive_FireControlMythic"));
            Assert.IsTrue(TowerBook.KillCountsAsTower(Team.Ally, Unit("Unit_Ranged")));
            Assert.IsTrue(TowerBook.KillCountsAsTower(Team.Ally, Unit("Unit_CogRocket")));
            Assert.IsFalse(TowerBook.KillCountsAsTower(Team.Ally, Unit("Unit_Cog")));
            Assert.IsFalse(TowerBook.KillCountsAsTower(Team.Enemy, Unit("Unit_Ranged")));
        }

        [Test]
        public void WorkshopMythic_RocketAlsoCountsAsCog()
        {
            var alias = Passive<UnitAliasPassive>("Passive_RocketWorkshopMythic");
            UnitDefinition rocket = Unit("Unit_CogRocket");
            UnitDefinition cog = Unit("Unit_Cog");
            Assert.IsFalse(cog.AppliesFor(Team.Ally, rocket));
            UnitAliases.Add(Team.Ally, rocket, alias.AliasOf(rocket));
            Assert.IsTrue(cog.AppliesFor(Team.Ally, rocket), "톱니병사 대상 효과를 받는다");
        }

        [Test]
        public void Mobilize_ReplacesOnlyBuildingSummons()
        {
            var mobilize = Load<MobilizeEffect>("Assets/Data/Effects/Effect_Mobilize.asset");
            UnitDefinition rocket = Unit("Unit_CogRocket");
            UnitDefinition giant = Unit("Unit_CogGiant");
            int count = 1;
            Assert.AreEqual(rocket, mobilize.ReplaceSummonLast(Team.Ally, giant, 0, ref count), "큰 유닛도");
            Assert.AreEqual(1, count, "신화가 아니면 1기는 1기");
            count = 1;
            Assert.AreEqual(giant, mobilize.ReplaceSummonLast(Team.Ally, giant, -1, ref count), "기물이 아닌 소환은 그대로");
        }

        [Test]
        public void MobilizeMythic_HeavyInfantryBecomesFive()
        {
            var mobilize = Load<MobilizeEffect>("Assets/Data/Effects/Effect_Mobilize.asset");
            var mark = Load<UnitEffect>("Assets/Data/Effects/Effect_MobilizeMythic.asset");
            UnitCategory heavy = Load<UnitCategory>("Assets/Data/Units/Category_HeavyInfantry.asset");
            UnitDefinition heavyUnit = null, otherUnit = null;
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/Data/Units" }))
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (unit.Category == heavy)
                    heavyUnit = heavyUnit ?? unit;
                else if (unit.Category != Unit("Unit_CogRocket").Category)
                    otherUnit = otherUnit ?? unit;
            }
            Assert.NotNull(heavyUnit, "중보병 유닛이 있어야 검사가 된다");
            TeamEffects.Enable(Team.Ally, mark, 1);
            int count = 2;
            mobilize.ReplaceSummonLast(Team.Ally, heavyUnit, 0, ref count);
            Assert.AreEqual(10, count, "중보병 2기 → 로켓병 10기");
            count = 2;
            mobilize.ReplaceSummonLast(Team.Ally, otherUnit, 0, ref count);
            Assert.AreEqual(2, count, "그 밖은 그대로");
        }

        [TestCase("Building_RocketWorkshop")]
        [TestCase("Building_FireControl")]
        [TestCase("Building_RecyclingPlant")]
        public void RankChangesSomething(string file)   // 등급이 올라도 아무것도 안 바뀌는 기물 금지(사용자 10/27)
        {
            var building = Load<BuildingDefinition>($"Assets/Data/Buildings/{file}.asset");
            int top = BuildingRarity.Legendary - building.Rarity + 1;
            string Text(int level)
            {
                var sb = new System.Text.StringBuilder();
                foreach (BuildingAction a in building.Actions)
                    sb.Append(a.Describe(level));
                foreach (BuildingPassive p in building.Passives)
                    sb.Append(p.Describe(level));
                return sb.ToString();
            }
            Assert.AreNotEqual(Text(1), Text(top));
        }
    }
}

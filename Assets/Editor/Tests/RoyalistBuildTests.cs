using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 킹덤 왕당파 「병사 → 무적 처치 → 기사 승급」 기물 4종 · 공주의 축복 등급 성장 · 왕족친위대 대상(사용자 10/28).
    // 전투(땅 · 보드)가 없어도 도는 계산만 본다 — 승급 · 무적 같은 전장 동작은 플레이 전투로 확인한다
    public class RoyalistBuildTests
    {
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.NotNull(asset, path);
            return asset;
        }

        private static UnitDefinition Unit(string name) => Load<UnitDefinition>($"Assets/Data/Units/{name}.asset");
        private static T Effect<T>(string name) where T : UnitEffect => Load<T>($"Assets/Data/Effects/{name}.asset");
        private static BuildingDefinition Building(string name) => Load<BuildingDefinition>($"Assets/Data/Buildings/{name}.asset");

        [SetUp]
        [TearDown]
        public void Clean()
        {
            UnitAliases.Clear();
            TeamEffects.Clear();
        }

        [Test]
        public void Blessing_OneMoreTargetPerRank_OneSecondFixed()
        {
            var action = Load<InvulnerableLowestAction>("Assets/Data/Buildings/Actions/Action_Blessing.asset");
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { action.TargetsAt(1), action.TargetsAt(2), action.TargetsAt(3) });
            StringAssert.Contains("아군 2기에게 1초간", action.Describe(2));
        }

        [Test]
        public void Blessing_MythicSixTargets()   // 사용자 10/28: 신화 = 6기(전설 3기보다 많다)
        {
            var action = Load<InvulnerableLowestAction>("Assets/Data/Buildings/Actions/Action_Blessing.asset");
            var mythic = Effect<InvulnerableTargetsEffect>("Effect_BlessingMythic");
            int legendary = action.TargetsAt(OwnedBuilding.LevelOf(Building("Building_Blessing"), 3));   // 희귀 → 신화(3번 오름) — Level은 전설에서 멈춘다
            Assert.AreEqual(3, legendary);
            Assert.AreEqual(6, System.Math.Max(legendary, mythic.Targets));
        }

        [Test]
        public void Conscription_ThreeKindsOneLine_RankCooldown()
        {
            BuildingDefinition office = Building("Building_RoyalConscription");
            Assert.NotNull(office.Unit, "생산 기물 — 쿨다운이 등급마다 준다(BuildingDefinition.CooldownAt)");
            StringAssert.Contains("방패수 · 병사 · 궁수를", BuildingTooltip.Describe(office, 1, 20f));
            StringAssert.Contains("기사", BuildingTooltip.Describe(office, 4, 20f, true), "신화 [제안]");
        }

        [Test]
        public void ForlornHope_ChanceByRank()
        {
            var effect = Effect<HitChanceInvulnerableEffect>("Effect_ForlornHope");
            StringAssert.Contains("20%", effect.Describe(1));
            StringAssert.Contains("25%", effect.Describe(2));
            StringAssert.Contains("30%", effect.Describe(3));
            Assert.IsTrue(effect.AppliesFor(Team.Ally, Unit("Unit_Melee")), "경보병 전체");
            Assert.IsTrue(effect.AppliesFor(Team.Ally, Unit("Unit_Cog")), "톱니병사도 경보병");
        }

        [Test]
        public void Accolade_ChanceOnlyFromLegendary()
        {
            var effect = Effect<InvulnerableKillPromoteEffect>("Effect_Accolade");
            Assert.IsNull(effect.Headline(1), "영웅은 무적일 때만");
            StringAssert.Contains("25%", effect.Describe(2));
        }

        [Test]
        public void Accolade_FollowsCategoryAlias()   // 군제 개혁 · 하마기병: 내 중기병은 경보병으로도 취급
        {
            var effect = Effect<InvulnerableKillPromoteEffect>("Effect_Accolade");
            UnitDefinition mammoth = Unit("Unit_TribeMammoth");
            Assert.IsFalse(effect.AppliesFor(Team.Ally, mammoth));
            UnitAliases.AddCategory(Team.Ally, mammoth.Category, Unit("Unit_Melee").Category);
            Assert.IsTrue(effect.AppliesFor(Team.Ally, mammoth));
        }

        [Test]
        public void RoyalGuard_KnightAndRoyalKnightTrigger()   // 사용자 10/30: 기사 → 친위기사 · 친위기사 → 기사, 확률은 등급마다 오른다
        {
            var effect = Effect<KillSummonEffect>("Effect_RoyalGuard");
            Assert.IsTrue(effect.AppliesFor(Team.Ally, Unit("Unit_Knight")));
            Assert.IsTrue(effect.AppliesFor(Team.Ally, Unit("Unit_RoyalKnight")));
            Assert.AreNotEqual(effect.Describe(1), effect.Describe(2), "등급마다 확률이 오른다");
            UnitAliases.AddCategory(Team.Ally, Unit("Unit_TribeMammoth").Category, Unit("Unit_Knight").Category);   // 군제 개혁 · 경장기병
            Assert.IsFalse(effect.AppliesFor(Team.Ally, Unit("Unit_TribeMammoth")), "경기병으로 취급돼도 기사는 아니다");
        }

        [Test]
        public void EveryRankChangesSomething()   // 사용자 10/27: 등급이 올라도 아무것도 안 바뀌는 기물 금지
        {
            BuildingDefinition barracks = Building("Building_RebelBarracks");
            Assert.NotNull(barracks.Unit, "생산 기물 — 쿨다운이 등급마다 준다(BuildingDefinition.CooldownAt)");
            var hope = Effect<HitChanceInvulnerableEffect>("Effect_ForlornHope");
            Assert.AreNotEqual(hope.Describe(1), hope.Describe(2));
            Assert.AreNotEqual(hope.Describe(2), hope.Describe(3));
            foreach (string name in new[] { "Building_RebelBarracks", "Building_ForlornHope", "Building_Accolade", "Building_RebelBanner", "Building_RoyalConscription" })
                Assert.Greater(Building(name).MythicEffects.Length + Building(name).MythicActions.Length, 0, name + " 신화 줄");
        }
    }
}

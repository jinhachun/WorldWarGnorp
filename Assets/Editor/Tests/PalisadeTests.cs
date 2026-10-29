using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 목책(사용자 10/29 안 C) — 15초마다 가장 앞 아군 앞에 세운다 · 등급마다 체력 +150 · 동시 3개(넘으면 맨 뒤 것을 걷고 새로) · 신화 반사.
    // 전장 동작(세워지는 자리 · 상한 · 맨 뒤 걷기)은 물리 · 땅 · 성이 필요해 플레이 전투로 잰다(메뉴 「GnorpWar/목책 실측 전투」)
    public class PalisadeTests
    {
        private static BuildingDefinition Building()
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Data/Buildings/Building_Palisade.asset");
            Assert.NotNull(building);
            return building;
        }

        [Test]
        public void CooldownAction_FifteenSeconds_NotProduction()
        {
            BuildingDefinition building = Building();
            Assert.AreEqual(1, building.Actions.Length);
            Assert.IsInstanceOf<BuildPalisadeAction>(building.Actions[0]);
            Assert.AreEqual(0, building.Passives.Length, "전투 시작 패시브는 뺐다");
            Assert.AreEqual(15f, building.CooldownAt(1));
            Assert.AreEqual(15f, building.CooldownAt(4), "생산 기물이 아니다 — 등급이 쿨다운을 줄이지 않는다");
        }

        [Test]
        public void HpPlus150PerRank_MaxThree()
        {
            var action = (BuildPalisadeAction)Building().Actions[0];
            CollectionAssert.AreEqual(new[] { 300f, 450f, 600f, 750f }, new[] { action.HpAt(1), action.HpAt(2), action.HpAt(3), action.HpAt(4) });
            Assert.AreEqual(3, action.Max);
        }

        [Test]
        public void Text_EverySecondsFirst_ThenBlocksOnlyFoes()
        {
            BuildingDefinition building = Building();
            string text = BuildingTooltip.Describe(building, 1, building.Cooldown);
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            Assert.AreEqual("15초마다 가장 앞 아군 앞에 체력 300의 목책을 세웁니다.\n목책은 적만 막습니다.", plain);
            string mythic = System.Text.RegularExpressions.Regex.Replace(BuildingTooltip.Describe(building, 4, building.Cooldown, true), "<[^>]+>", "");
            StringAssert.Contains("체력 750의", mythic);
            StringAssert.EndsWith("목책이 받은 피해의 50%를 돌려줍니다.", mythic);
        }
    }
}

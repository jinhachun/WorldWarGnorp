using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 탑에 아직 없는 기물(상점 진열 · 보관함)의 대상 표시(사용자 10/29) — 자리와 상관없이 대상이 정해지는 능력만 내 탑 층에 뜬다(BuildingTargets.OfUnplaced)
    public class BuildingTargetsTests
    {
        private static OwnedBuilding Load(string name)
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/Data/Buildings/{name}.asset");
            Assert.NotNull(building, name);
            return new OwnedBuilding(building);
        }

        [Test]
        public void SupplyDepot_Unplaced_MarksTopCoolingFloor()
        {
            OwnedBuilding cooling = Load("Building_Palisade");
            Assert.Greater(cooling.Definition.Cooldown, 0f);
            var field = new[] { cooling, Load("Building_Palisade"), null };
            var targets = new List<int>();
            BuildingTargets.OfUnplaced(field, Load("Building_SupplyDepot"), targets);
            CollectionAssert.AreEqual(new[] { 1 }, targets, "쿨다운이 있는 가장 위 기물");
        }

        [Test]
        public void AboveFloorEffect_Unplaced_MarksNothing()
        {
            var field = new[] { Load("Building_MirrorWorkshop"), Load("Building_Palisade") };
            var targets = new List<int>();
            BuildingTargets.Of(field, 0, false, targets);
            CollectionAssert.AreEqual(new[] { 1 }, targets, "탑에 놓이면 바로 위 기물에 표시된다");

            BuildingTargets.OfUnplaced(new[] { Load("Building_Palisade"), Load("Building_Palisade") }, Load("Building_MirrorWorkshop"), targets);
            CollectionAssert.IsEmpty(targets, "자리에 따라 바뀌는 효과는 탑 밖에서 표시하지 않는다");
        }
    }
}

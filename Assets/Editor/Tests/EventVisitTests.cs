using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor.Tests
{
    // 이벤트 땅 방문(아웃게임기획 §3-1 · Run/RunState.Events) — BeginVisit → ChooseVisit / DeclineVisit · 종류마다 결과를 값으로 · 닫힘 · 턴 흐름 · 탑 스냅샷 왕복
    public class EventVisitTests
    {
        private BattleConfig _config;
        private AssetIds _ids;
        private TowerState _player;

        [OneTimeSetUp]
        public void Load()
        {
            _config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            _ids = AssetDatabase.LoadAssetAtPath<AssetIds>(AssetIdsMenu.Path);
            Assert.NotNull(_config);
            Assert.NotNull(_ids);
            foreach (EventLandKind kind in System.Enum.GetValues(typeof(EventLandKind)))
                Assert.NotNull(_config.World.EventOf(kind), $"WorldConfig 「이벤트 땅」에 {kind}");
        }

        // 새 런 — 런 기록 파일은 쓰지 않는다(테스트가 진짜 유령 풀에 기록을 남기지 않게)
        [SetUp]
        public void NewRun()
        {
            RunState.StartNew(_config);
            RunState.ChooseNation(RunState.NationChoices[0]);
            typeof(RunState).GetField("_record", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            _player = (TowerState)typeof(RunState).GetField("Player", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }

        // 이벤트 땅 하나를 그 종류로 바꾸고, 그 국경의 땅 하나를 내 것으로 — 방문할 수 있게
        private int Prepare(EventLandKind kind)
        {
            WorldState state = RunState.World.State;
            int region = RunState.EventRegions()[0];
            state.EventKind[region] = kind;
            Own(state, region);
            Assert.Contains(region, RunState.VisitableEvents());
            return region;
        }

        // 이벤트 땅의 국경 땅 하나를 내 것으로 — 그 땅이 마지막 땅이던 영주는 사라진 것으로
        private static void Own(WorldState state, int region)
        {
            int land = state.Graph.EventBorders[region][0];
            Lord before = state.OwnerOf(land);
            state.Owner[land] = state.Player.Id;
            if (before != null && !before.IsPlayer && state.LandsOf(before) == 0)
                before.Alive = false;
        }

        private BuildingDefinition CommonWithUnit()
            => System.Array.Find(_config.ShopPool, b => b.Rarity == BuildingRarity.Common && b.Unit != null);

        // 흐름 — 고르기 전엔 열려 있고, 고르면 닫힘 · Aftermath · 턴은 EndTurn까지 그대로 · 같은 땅 다시 못 감
        [Test]
        public void FlowClosesAndEndsTurn()
        {
            int region = Prepare(EventLandKind.Treasury);
            int round = RunState.Round;
            EventOffer offer = RunState.BeginVisit(region);
            Assert.NotNull(offer);
            Assert.AreSame(offer, RunState.BeginVisit(region), "같은 땅 = 같은 후보");
            Assert.IsFalse(RunState.World.State.EventClosed[region], "고르기 전엔 안 닫힘");
            Assert.AreEqual(RunState.RunPhase.Map, RunState.Phase);
            Assert.IsFalse(RunState.DeclineVisit(), "보물 창고는 고르지 않고 못 닫는다");
            Assert.IsFalse(RunState.ChooseVisit(5), "범위 밖");
            Assert.IsTrue(RunState.ChooseVisit(0));
            Assert.IsTrue(RunState.World.State.EventClosed[region]);
            Assert.IsNull(RunState.CurrentVisit);
            Assert.AreEqual(RunState.RunPhase.Aftermath, RunState.Phase);
            Assert.AreEqual(RunState.Outcome.None, RunState.LastOutcome);
            Assert.IsNull(RunState.BeginVisit(region), "닫힌 땅");
            Assert.AreEqual(round, RunState.Round);
            RunState.EndTurn();
            Assert.AreEqual(round + 1, RunState.Round, "쉬기와 같은 길 — EndTurn이 턴을 넘긴다");
            Assert.AreEqual(RunState.RunPhase.Map, RunState.Phase);
        }

        [Test]
        public void Treasury()
        {
            int region = Prepare(EventLandKind.Treasury);
            int gold = RunState.Gold, expected = 10 + RunState.World.State.Turn * 2;
            EventOffer offer = RunState.BeginVisit(region);
            Assert.AreEqual(1, offer.Choices.Length);
            Assert.AreEqual(expected, offer.Choices[0].Gold, "10 + 턴 × 2");
            StringAssert.Contains(expected.ToString(), RunState.EventText(region));
            RunState.ChooseVisit(0);
            Assert.AreEqual(gold + expected, RunState.Gold);
        }

        [Test]
        public void Forge()
        {
            BuildingDefinition building = CommonWithUnit();
            _player.Field[0] = new OwnedBuilding(building);
            _player.Storage[0] = new OwnedBuilding(building, BuildingRarity.Mythic - building.Rarity, 0, 0, 1f);
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.Forge));
            Assert.AreEqual(1, offer.Choices.Length, "신화는 후보가 아니다");
            Assert.AreSame(_player.Field[0], offer.Choices[0].Building);
            Assert.IsFalse(offer.CanDecline);
            int pending = RunState.PendingUpgrades.Count;
            RunState.ChooseVisit(0);
            Assert.AreEqual(building.Rarity + 1, _player.Field[0].Rarity);
            Assert.AreEqual(pending, RunState.PendingUpgrades.Count, "합치기 보상은 안 나온다");
        }

        [Test]
        public void ForgeWithNothingToRaise()
        {
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.Forge));
            Assert.AreEqual(0, offer.Choices.Length);
            Assert.IsTrue(offer.CanDecline, "후보가 없으면 닫기");
            Assert.IsTrue(RunState.DeclineVisit());
            Assert.AreEqual(RunState.RunPhase.Aftermath, RunState.Phase);
        }

        [Test]
        public void TempleRaisesMine()
        {
            UpgradeDefinition upgrade = System.Array.Find(_config.Upgrades, u => u.InPool && u.AvailableAt(BuildingRarity.Common) && u.AvailableAt(BuildingRarity.Uncommon));
            Assert.NotNull(upgrade);
            _player.Upgrades.Add(new UpgradeGain { Definition = upgrade, Rarity = BuildingRarity.Common });
            _player.Upgrades.Add(new UpgradeGain { Definition = upgrade, Rarity = BuildingRarity.Common });
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.Temple));
            Assert.AreEqual(1, offer.Choices.Length, "같은 종 · 같은 등급은 하나로");
            Assert.IsFalse(offer.Choices[0].New);
            RunState.ChooseVisit(0);
            Assert.AreEqual(BuildingRarity.Uncommon, _player.Upgrades[0].Rarity);
            Assert.AreEqual(BuildingRarity.Common, _player.Upgrades[1].Rarity, "하나만 오른다");
        }

        [Test]
        public void TempleWithNothingOffersThreeCommon()
        {
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.Temple));
            Assert.AreEqual(3, offer.Choices.Length);
            foreach (EventChoice choice in offer.Choices)
            {
                Assert.IsTrue(choice.New);
                Assert.AreEqual(BuildingRarity.Rare, choice.To);
                Assert.AreEqual(BuildingNation.Common, choice.Upgrade.Nation);
            }
            RunState.ChooseVisit(1);
            Assert.AreEqual(1, _player.Upgrades.Count);
            Assert.AreEqual(offer.Choices[1].Upgrade, _player.Upgrades[0].Definition);
            Assert.AreEqual(BuildingRarity.Rare, _player.Upgrades[0].Rarity);
        }

        [Test]
        public void MercenaryIsFreeAndForeign()
        {
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.Mercenary));
            Assert.AreEqual(3, offer.Choices.Length);
            var seen = new HashSet<BuildingDefinition>();
            foreach (EventChoice choice in offer.Choices)
            {
                Assert.AreNotEqual(BuildingNation.Common, choice.Offer.Nation);
                Assert.AreNotEqual(RunState.Nation, choice.Offer.Nation, "다른 국가만");
                Assert.IsTrue(seen.Add(choice.Offer), "서로 다른 기물");
                Assert.IsTrue(choice.Receivable);
            }
            RunState.ChooseVisit(2);
            Assert.AreEqual(offer.Choices[2].Offer, _player.Field[0].Definition);
            Assert.AreEqual(0, RunState.SellValue(_player.Field[0]), "공짜 — 판매 0골드");
        }

        [Test]
        public void MercenaryWithNoRoom()
        {
            BuildingDefinition filler = CommonWithUnit();
            for (int i = 0; i < _player.Field.Length; i++)
                _player.Field[i] = new OwnedBuilding(filler);
            for (int i = 0; i < _player.Storage.Length; i++)
                _player.Storage[i] = new OwnedBuilding(filler);
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.Mercenary));
            Assert.IsFalse(offer.Choices[0].Receivable);
            Assert.IsTrue(RunState.ChooseVisit(0), "골라도 받지 못하고 닫힌다");
            Assert.IsFalse(System.Array.Exists(_player.Field, b => b.Definition == offer.Choices[0].Offer));
        }

        [Test]
        public void TrainingIsPermanent()
        {
            BuildingDefinition building = CommonWithUnit();
            _player.Field[0] = new OwnedBuilding(building);
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.Training));
            Assert.AreEqual(1, offer.Choices.Length, "탑의 기물이 낳는 유닛만");
            UnitDefinition unit = offer.Choices[0].Unit;
            Assert.AreEqual(building.Unit, unit);
            RunState.ChooseVisit(0);
            Assert.AreEqual(100f + 20f, Run(unit, UnitStat.MaxHp, 100f));
            Assert.AreEqual(10f + 2f, Run(unit, UnitStat.Attack, 10f));
            UnitDefinition other = System.Array.Find(_config.ShopPool, b => b.Unit != null && b.Unit != unit).Unit;
            Assert.AreEqual(100f, Run(other, UnitStat.MaxHp, 100f), "고른 유닛만");
        }

        private static float Run(UnitDefinition unit, UnitStat stat, float baseValue)
        {
            StatSum sum = StatSum.Identity;
            StatBook.AccumulateRun(unit, stat, ref sum);
            return sum.Apply(baseValue);
        }

        [Test]
        public void BloodAltar()
        {
            int lives = RunState.Lives;
            EventOffer offer = RunState.BeginVisit(Prepare(EventLandKind.BloodAltar));
            Assert.AreEqual(3, offer.Choices.Length);
            Assert.IsFalse(offer.Blocked);
            Assert.IsTrue(offer.CanDecline, "「바치지 않기」");
            foreach (EventChoice choice in offer.Choices)
                Assert.Contains(choice.Upgrade, RunState.World.UnplacedSpecials, "지도에 없는 지역 특수");
            RunState.ChooseVisit(0);
            Assert.AreEqual(lives - 1, RunState.Lives);
            Assert.AreEqual(offer.Choices[0].Upgrade, _player.Upgrades[0].Definition);
            Assert.AreEqual(BuildingRarity.Mythic, _player.Upgrades[0].Rarity);
        }

        [Test]
        public void BloodAltarAtOneLife()
        {
            RunState.CheatSetLives(1);
            int region = Prepare(EventLandKind.BloodAltar);
            EventOffer offer = RunState.BeginVisit(region);
            Assert.IsTrue(offer.Blocked);
            Assert.IsFalse(RunState.ChooseVisit(0), "목숨 1에서는 바칠 수 없다");
            Assert.IsFalse(RunState.World.State.EventClosed[region]);
            Assert.IsTrue(RunState.DeclineVisit(), "바치지 않기 — 효과 없이 닫힘 · 턴은 씀");
            Assert.AreEqual(1, RunState.Lives);
            Assert.AreEqual(0, _player.Upgrades.Count);
            Assert.IsTrue(RunState.World.State.EventClosed[region]);
            Assert.AreEqual(RunState.RunPhase.Aftermath, RunState.Phase);
        }

        [Test]
        public void AgitatorCanCauseRebellion()
        {
            int region = Prepare(EventLandKind.Agitator);
            EventOffer offer = RunState.BeginVisit(region);
            Assert.Greater(offer.Choices.Length, 0);
            Assert.LessOrEqual(offer.Choices.Length, 3);
            List<Lord> neighbors = RunState.World.State.Neighbors(RunState.World.State.Player);
            foreach (EventChoice choice in offer.Choices)
                Assert.Contains(choice.Lord, neighbors, "국경을 맞댄 영주만");
            Lord lord = offer.Choices[0].Lord;
            lord.Happiness = 2;
            lord.Temper = LordTemper.Cautious;   // 그 턴 AI 침략에서 이겨 행복도가 다시 오르지 않게(쉬는 성향) — 지도 · 난수에 따라 흔들리던 것
            RunState.ChooseVisit(0);
            Assert.AreEqual(0, lord.Happiness);
            List<WorldEvent> events = RunState.EndTurn();
            Assert.IsFalse(lord.Alive, "0이면 그 턴 정산에 반란(AI 침략에 땅을 다 잃었으면 그 전에 사라진다)");
            Assert.IsTrue(events.Exists(e => e.Rebellion && e.Lord == lord.Id) || RunState.World.State.LandsOf(lord) == 0);
        }

        [Test]
        public void ShrineIsTerrainUpgrade()
        {
            int region = Prepare(EventLandKind.Shrine);
            MapDefinition terrain = RunState.World.Map.TerrainOf(region);
            EventOffer offer = RunState.BeginVisit(region);
            Assert.AreEqual(1, offer.Choices.Length);
            StringAssert.Contains(terrain.DisplayName, RunState.EventText(region));
            RunState.ChooseVisit(0);
            UpgradeDefinition shrine = _player.Upgrades[0].Definition;
            Assert.AreEqual(terrain, shrine.Terrain);
            Assert.IsFalse(System.Array.Exists(_config.Upgrades, u => u == shrine), "합치기 후보에 안 나온다");
            MapDefinition other = System.Array.Find(_config.Maps, m => m != terrain);
            RunState.CheatSetMap(terrain);
            Assert.IsTrue(shrine.UnitModifier(shrine.ValueAt(BuildingRarity.Common), out StatModifier modifier));
            Assert.AreEqual(UnitStat.AttackSpeed, modifier.Stat);
            Assert.AreEqual(StatOp.Percent, modifier.Op);
            Assert.AreEqual(0.1f, modifier.Value, 1e-5f);
            RunState.CheatSetMap(other);
            Assert.IsFalse(shrine.UnitModifier(shrine.ValueAt(BuildingRarity.Common), out _), "다른 지형에서는 안 걸린다");
            RunState.CheatSetMap(null);
        }

        // 스냅샷 ⓐ — 대장간(기물 등급) · 신전(업그레이드 등급) · 훈련장(영구 강화) · 피의 제단 · 지형 성소(업그레이드)가 탑 스냅샷 왕복에 남는다
        [Test]
        public void ResultsSurviveSnapshot()
        {
            BuildingDefinition building = CommonWithUnit();
            _player.Field[0] = new OwnedBuilding(building);
            Visit(EventLandKind.Forge, 0);
            Visit(EventLandKind.Training, 0);
            Visit(EventLandKind.Temple, 0);
            Visit(EventLandKind.BloodAltar, 0);
            Visit(EventLandKind.Shrine, 0);
            Assert.AreEqual(building.Rarity + 1, _player.Field[0].Rarity);
            Assert.AreEqual(3, _player.Upgrades.Count, "신전 · 제단 · 성소");

            string json = TowerSnapshot.ToJson(_player, RunState.Nation, Color.white, RunState.Lives, 3, _ids);
            TowerSnapshot snapshot = TowerSnapshot.FromJson(json, Team.Enemy, _ids, _config);
            Assert.AreEqual(_player.Field[0].Rarity, snapshot.Tower.Field[0].Rarity);
            Assert.AreEqual(_player.Upgrades.Count, snapshot.Tower.Upgrades.Count);
            for (int i = 0; i < _player.Upgrades.Count; i++)
            {
                Assert.AreEqual(_player.Upgrades[i].Definition, snapshot.Tower.Upgrades[i].Definition);
                Assert.AreEqual(_player.Upgrades[i].Rarity, snapshot.Tower.Upgrades[i].Rarity);
            }
            Assert.AreEqual(RunState.Lives, snapshot.Lives);
            var sum = StatSum.Identity;
            StatBook.AccumulateBook(snapshot.Tower.Book, Team.Enemy, building.Unit, UnitStat.MaxHp, ref sum);
            Assert.AreEqual(120f, sum.Apply(100f), "훈련장 — 영구 강화(출처 = 그 유닛 에셋)");
            Assert.AreEqual(json, TowerSnapshot.ToJson(snapshot.Tower, snapshot.Nation, snapshot.LordColor, snapshot.Lives, snapshot.Turn, _ids));
        }

        // 한 턴에 한 번이라 방문마다 지도 단계로 돌린다(EndTurn)
        private void Visit(EventLandKind kind, int choice)
        {
            WorldState state = RunState.World.State;
            int region = RunState.EventRegions().Find(e => !state.EventClosed[e]);
            state.EventKind[region] = kind;
            Own(state, region);
            EventOffer offer = RunState.BeginVisit(region);
            Assert.NotNull(offer, kind.ToString());
            Assert.IsTrue(RunState.ChooseVisit(choice), kind.ToString());
            RunState.EndTurn();
        }
    }
}

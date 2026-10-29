using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 나라 소식 · 외교(아웃게임기획 §3-2 · Run/RunState.News) — 뜨는 턴 · 겹침 없음 · 고르기마다 결과를 값으로 · 통상(이번 턴 침략 불가 · 다음 턴 해제 · 내 진열 무리)
    public class NationNewsTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        private BattleConfig _config;
        private TowerState _player;

        [OneTimeSetUp]
        public void Load()
        {
            _config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            Assert.NotNull(_config);
            Assert.AreEqual(9, _config.World.News.Length, "무작위 소식 9종");
            Assert.NotNull(_config.World.Festival);
        }

        // 새 런 — 런 기록 파일은 쓰지 않는다
        [SetUp]
        public void NewRun()
        {
            RunState.NewsRandom = null;
            RunState.StartNew(_config);
            RunState.ChooseNation(RunState.NationChoices[0]);
            typeof(RunState).GetField("_record", Static).SetValue(null, null);
            _player = (TowerState)typeof(RunState).GetField("Player", Static).GetValue(null);
        }

        [TearDown]
        public void Restore() => RunState.NewsRandom = null;

        // 쉬고 다음 턴을 연다
        private static void NextTurn()
        {
            RunState.Rest();
            RunState.EndTurn();
            RunState.BeginTurn();
        }

        // 이번 턴의 소식을 그 에셋으로
        private NewsOffer Force(string asset)
        {
            var definition = AssetDatabase.LoadAssetAtPath<NationNewsDefinition>($"Assets/Data/News/{asset}.asset");
            Assert.NotNull(definition, asset);
            var offer = new NewsOffer { Definition = definition };
            typeof(RunState).GetField("_news", Static).SetValue(null, offer);
            return offer;
        }

        private BuildingDefinition CommonWithUnit()
            => System.Array.Find(_config.ShopPool, b => b.Rarity == BuildingRarity.Common && b.Unit != null && b.Nation == BuildingNation.Common);

        private static void ClearField(TowerState tower)
        {
            for (int i = 0; i < tower.Field.Length; i++)
                tower.Field[i] = null;
            for (int i = 0; i < tower.Storage.Length; i++)
                tower.Storage[i] = null;
        }

        private static int ShopTier => (int)typeof(RunState).GetProperty("ShopTier", Static).GetValue(null);

        [Test]
        public void NewsAndDiplomacyTurns()
        {
            var seen = new List<string>();
            int diplomacyTurn = RunState.DiplomacyTurn;
            CollectionAssert.Contains(new[] { 3, 6 }, diplomacyTurn);
            RunState.BeginTurn();
            for (int turn = 1; turn <= 13; turn++)
            {
                Assert.AreEqual(turn, RunState.Round + 1);
                bool newsTurn = turn == 3 || turn == 6 || turn == 9 || turn == 12;
                Assert.AreEqual(newsTurn, RunState.CurrentNews != null, $"턴 {turn} 소식");
                if (newsTurn)
                {
                    string title = RunState.CurrentNews.Definition.DisplayName;
                    CollectionAssert.DoesNotContain(seen, title, "소식이 겹쳤다");
                    seen.Add(title);
                    if (turn == 12)
                        Assert.AreSame(_config.World.Festival, RunState.CurrentNews.Definition, "턴 12는 건국 기념제");
                    else
                        Assert.AreNotSame(_config.World.Festival, RunState.CurrentNews.Definition);
                }
                Assert.AreEqual(turn == diplomacyTurn, RunState.CurrentDiplomacy != null, $"턴 {turn} 외교");
                if (RunState.IsOver)
                    break;
                NextTurn();
            }
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void DiplomacyTurnIsThreeOrSix()
        {
            var turns = new HashSet<int>();
            for (int i = 0; i < 40; i++)
            {
                RunState.StartNew(_config);
                turns.Add(RunState.DiplomacyTurn);
            }
            CollectionAssert.AreEquivalent(new[] { 3, 6 }, turns);
        }

        [Test]
        public void DiplomacyPicksNearestOtherNation()
        {
            for (int i = 0; i < 20; i++)
            {
                RunState.StartNew(_config);
                RunState.ChooseNation(RunState.NationChoices[i % RunState.NationChoices.Length]);
                var offer = (DiplomacyOffer)typeof(RunState).GetMethod("RollDiplomacy", Static).Invoke(null, null);
                Assert.NotNull(offer);
                Assert.AreNotEqual(RunState.Nation, offer.Nation);
                Assert.AreNotEqual(BuildingNation.Common, offer.Nation);
                Assert.AreEqual(offer.Nation, RunState.World.NationOf(offer.Lord));
                WorldState state = RunState.World.State;
                List<Lord> neighbors = state.Neighbors(state.Player);
                Assert.AreEqual(neighbors.Contains(offer.Lord), offer.Neighbor);
                bool otherNeighbor = neighbors.Exists(l => l.Alive && !l.IsPlayer && RunState.World.NationOf(l) != RunState.Nation && RunState.World.NationOf(l) != BuildingNation.Common);
                Assert.AreEqual(otherNeighbor, offer.Neighbor, "국경을 맞댄 다른 국가 영주가 있으면 그 영주");
            }
        }

        [Test]
        public void Harvest()
        {
            Force("News_Harvest");
            int gold = RunState.Gold;
            Assert.IsTrue(RunState.ChooseNews(0));
            Assert.AreEqual(gold + 15, RunState.Gold);
            Assert.IsNull(RunState.CurrentNews);
            Force("News_Harvest");
            int income = RunState.TurnGoldNow;
            RunState.ChooseNews(1);
            Assert.AreEqual(income + 2, RunState.TurnGoldNow, "턴 수입 영구히 +2");
            NextTurn();
            NextTurn();
            Assert.AreEqual(RunState.TurnGold(_player, RunState.Lands) + 2, RunState.TurnGoldNow, "몇 턴 뒤에도");
        }

        [Test]
        public void GoldMineNeedsGold()
        {
            Force("News_GoldMine");
            _player.Gold = 9;
            Assert.IsFalse(RunState.CanChooseNews(1), "골드 -10을 낼 수 없다");
            Assert.IsFalse(RunState.ChooseNews(1));
            _player.Gold = 10;
            int income = RunState.TurnGoldNow;
            Assert.IsTrue(RunState.ChooseNews(1));
            Assert.AreEqual(0, RunState.Gold);
            Assert.AreEqual(income + 4, RunState.TurnGoldNow);
        }

        [Test]
        public void LocustPenaltyAndHalfIncome()
        {
            Force("News_Locust");
            _player.Gold = 4;
            Assert.IsTrue(RunState.CanChooseNews(0), "벌만 있는 고르기는 언제나");
            RunState.ChooseNews(0);
            Assert.AreEqual(0, RunState.Gold, "0까지만 깎인다");

            ClearField(_player);
            Force("News_Locust");
            int full = RunState.TurnGoldNow;
            Assert.IsTrue(RunState.ChooseNews(1));
            Assert.AreEqual(UnityEngine.Mathf.RoundToInt(full * 0.5f), RunState.TurnGoldNow, "다음 턴 수입 절반");
            OwnedBuilding gained = _player.Field[0];
            Assert.NotNull(gained, "공용 생산 기물을 얻는다");
            Assert.AreEqual(BuildingNation.Common, gained.Definition.Nation);
            Assert.NotNull(gained.Definition.Unit);
            Assert.AreEqual(0, RunState.SellValue(gained), "공짜");
            int gold = RunState.Gold;
            int half = RunState.TurnGoldNow;
            RunState.Rest();
            RunState.EndTurn();
            RunState.BeginTurn();
            Assert.GreaterOrEqual(RunState.Gold, gold + half);
            Assert.AreEqual(RunState.TurnGold(_player, RunState.Lands), RunState.TurnGoldNow, "그다음 턴은 그대로");
        }

        [Test]
        public void CaravanHeroShopThisTurnOnly()
        {
            RunState.BeginTurn();
            Force("News_Caravan");
            Assert.IsTrue(RunState.ChooseNews(0));
            for (int roll = 0; roll < 30; roll++)
            {
                for (int i = 0; i < RunState.Offers.Length; i++)
                    Assert.GreaterOrEqual(RunState.Offers[i].Rarity + RunState.OfferUpgrades[i], BuildingRarity.Epic, "이번 상점은 전부 영웅 이상");
                _player.Gold = 100;
                RunState.Reroll();
            }
            NextTurn();
            bool low = false;
            for (int roll = 0; roll < 30 && !low; roll++)
            {
                RunState.CheatRollOffers();
                foreach (BuildingDefinition offer in RunState.Offers)
                    low |= offer.Rarity < BuildingRarity.Epic;
            }
            Assert.IsTrue(low, "다음 턴엔 풀린다");

            Force("News_Caravan");
            int free = RunState.FreeRerolls;
            RunState.ChooseNews(1);
            Assert.AreEqual(free + 3, RunState.FreeRerolls);
        }

        [Test]
        public void MasterPickAndRandom()
        {
            BuildingDefinition building = CommonWithUnit();
            ClearField(_player);
            Force("News_Master");
            _player.Gold = 50;
            Assert.IsFalse(RunState.CanChooseNews(0), "고를 기물이 없으면 흐리게");
            _player.Field[0] = new OwnedBuilding(building);
            _player.Field[1] = new OwnedBuilding(building, BuildingRarity.Mythic - building.Rarity, 0, 0, 1f);
            CollectionAssert.AreEqual(new[] { 0 }, RunState.NewsTargets(0), "신화는 고를 수 없다");
            Assert.IsFalse(RunState.ChooseNews(0, 1));
            Assert.IsTrue(RunState.ChooseNews(0, 0));
            Assert.AreEqual(30, RunState.Gold);
            Assert.AreEqual(building.Rarity + 1, _player.Field[0].Rarity);

            Force("News_Master");
            int income = RunState.TurnGoldNow;
            Assert.IsTrue(RunState.ChooseNews(1));
            Assert.AreEqual(building.Rarity + 2, _player.Field[0].Rarity, "오를 수 있는 기물이 하나뿐");
            Assert.AreEqual(income - 5, RunState.TurnGoldNow, "다음 턴 수입 -5");
        }

        [Test]
        public void FireReplacesWithHigher()
        {
            BuildingDefinition building = CommonWithUnit();
            ClearField(_player);
            Force("News_Fire");
            Assert.IsFalse(RunState.CanChooseNews(0), "탑이 비면 흐리게");
            _player.Field[0] = new OwnedBuilding(building);
            Assert.IsTrue(RunState.ChooseNews(0));
            Assert.NotNull(_player.Field[0]);
            Assert.AreEqual(building.Rarity + 1, _player.Field[0].Rarity, "잃은 기물 등급 +1, 그 자리에");
            Assert.IsNull(_player.Field[1]);

            _player.Field[0] = new OwnedBuilding(building, BuildingRarity.Mythic - building.Rarity, 0, 0, 1f);
            Force("News_Fire");
            RunState.ChooseNews(0);
            Assert.AreEqual(BuildingRarity.Mythic, _player.Field[0].Rarity, "신화면 신화");

            Force("News_Fire");
            _player.Gold = 3;
            RunState.ChooseNews(1);
            Assert.AreEqual(0, RunState.Gold);
        }

        [Test]
        public void ScholarAdvancesTier()
        {
            RunState.CheatSetRound(1);
            Force("News_Scholar");
            _player.Gold = 15;
            int tier = ShopTier;
            int expected = UnityEngine.Mathf.Min(_config.World.ShopTier(RunState.Round + 1), _config.World.ShopTierMax);
            Assert.AreNotEqual(tier, expected, "재는 턴에서 단계가 달라야 한다");
            Assert.IsTrue(RunState.ChooseNews(0));
            Assert.AreEqual(0, RunState.Gold);
            Assert.AreEqual(expected, ShopTier, "한 턴 앞당김");
            Force("News_Scholar");
            int free = RunState.FreeRerolls;
            RunState.ChooseNews(1);
            Assert.AreEqual(free + 2, RunState.FreeRerolls);
            Assert.GreaterOrEqual(ShopTier, tier);
        }

        [Test]
        public void MigrantsAddSlot()
        {
            RunState.BeginTurn();
            int slots = RunState.Offers.Length, income = RunState.TurnGoldNow;
            Force("News_Migrants");
            Assert.IsTrue(RunState.ChooseNews(0));
            Assert.AreEqual(slots + 1, RunState.Offers.Length);
            Assert.AreEqual(slots + 1, RunState.OfferUpgrades.Length);
            foreach (BuildingDefinition offer in RunState.Offers)
                Assert.NotNull(offer, "새 칸도 채운다");
            Assert.AreEqual(income - 1, RunState.TurnGoldNow);
            NextTurn();
            Assert.AreEqual(slots + 1, RunState.Offers.Length, "영구히");
            Force("News_Migrants");
            int gold = RunState.Gold;
            RunState.ChooseNews(1);
            Assert.AreEqual(gold + 10, RunState.Gold);
        }

        [Test]
        public void AlchemistChanceBoundary()
        {
            BuildingDefinition building = CommonWithUnit();
            ClearField(_player);
            _player.Field[0] = new OwnedBuilding(building);
            Force("News_Alchemist");
            _player.Gold = 24;
            Assert.IsFalse(RunState.CanChooseNews(0), "골드 25가 없으면 흐리게");
            _player.Gold = 50;
            RunState.NewsRandom = () => 0.25f;
            Assert.IsTrue(RunState.ChooseNews(0, 0));
            Assert.IsFalse(RunState.LastAlchemySucceeded, "25%는 0.25 미만만 성공");
            Assert.AreEqual(building.Rarity, _player.Field[0].Rarity, "실패하면 골드만 잃는다");
            Assert.AreEqual(25, RunState.Gold);

            Force("News_Alchemist");
            RunState.NewsRandom = () => 0.2499f;
            Assert.IsTrue(RunState.ChooseNews(0, 0));
            Assert.IsTrue(RunState.LastAlchemySucceeded);
            Assert.AreEqual(BuildingRarity.Mythic, _player.Field[0].Rarity);
            Assert.AreEqual(0, RunState.Gold);

            Force("News_Alchemist");
            Assert.IsFalse(RunState.CanChooseNews(0), "신화는 고를 수 없다 · 골드도 없다");
            int gold = RunState.Gold;
            Assert.IsTrue(RunState.ChooseNews(1), "아무 일 없음");
            Assert.AreEqual(gold, RunState.Gold);
        }

        [Test]
        public void FestivalLegendaryOrRankUp()
        {
            BuildingDefinition building = CommonWithUnit();
            ClearField(_player);
            Force("News_Festival");
            Assert.IsTrue(RunState.ChooseNews(0));
            Assert.NotNull(_player.Field[0]);
            Assert.AreEqual(BuildingRarity.Legendary, _player.Field[0].Rarity);
            _player.Field[1] = new OwnedBuilding(building);
            Force("News_Festival");
            Assert.IsTrue(RunState.ChooseNews(1, 1));
            Assert.AreEqual(building.Rarity + 1, _player.Field[1].Rarity);
        }

        [Test]
        public void TreatyTruceAndShop()
        {
            WorldState state = RunState.World.State;
            RunState.BeginTurn();
            Lord lord = state.Neighbors(state.Player).Find(l => l.Alive && !l.IsPlayer);
            Assert.NotNull(lord);
            BuildingNation other = System.Array.Find(RunState.NationChoices, n => n != RunState.Nation);
            typeof(RunState).GetField("_diplomacy", Static).SetValue(null, new DiplomacyOffer { Lord = lord, Nation = other, Neighbor = true });

            // 조약 전 — 공용 + 내 국가만
            Dictionary<BuildingNation, int> before = SampleOffers(1000);
            foreach (BuildingNation nation in before.Keys)
                Assert.IsTrue(nation == BuildingNation.Common || nation == RunState.Nation, $"조약 전 {nation} 기물이 나왔다");

            Assert.IsTrue(RunState.InvadableRegions().Exists(r => state.OwnerOf(r) == lord));
            RunState.AnswerDiplomacy(true);
            Assert.IsNull(RunState.CurrentDiplomacy);
            Assert.IsFalse(RunState.InvadableRegions().Exists(r => state.OwnerOf(r) == lord), "이번 턴엔 침략할 수 없다");
            CollectionAssert.Contains(RunState.Treaties, other);

            // 조약 뒤 — 조약 국가가 무리에 들어온다(등급 확률 × 그 등급 안 고르게로 기대값)
            Dictionary<BuildingNation, int> after = SampleOffers(1000);
            int total = 0;
            foreach (int n in after.Values)
                total += n;
            foreach (BuildingNation nation in after.Keys)
                Assert.IsTrue(nation == BuildingNation.Common || nation == RunState.Nation || nation == other, $"조약 없는 {nation}");
            float expected = ExpectedShare(other);
            float observed = after.TryGetValue(other, out int count) ? count / (float)total : 0f;
            Assert.Greater(expected, 0.05f);
            Assert.AreEqual(expected, observed, 0.03f, $"조약 국가 비율 — 기대 {expected:0.000} · 잼 {observed:0.000} · 표본 {total}칸");

            NextTurn();
            if (lord.Alive && state.Neighbors(state.Player).Contains(lord))
                Assert.IsTrue(RunState.InvadableRegions().Exists(r => state.OwnerOf(r) == lord), "다음 턴엔 풀린다");
            CollectionAssert.Contains(RunState.Treaties, other, "조약은 런 끝까지");
        }

        [Test]
        public void DeclineKeepsShopClosed()
        {
            WorldState state = RunState.World.State;
            Lord lord = state.Neighbors(state.Player).Find(l => l.Alive && !l.IsPlayer);
            BuildingNation other = System.Array.Find(RunState.NationChoices, n => n != RunState.Nation);
            typeof(RunState).GetField("_diplomacy", Static).SetValue(null, new DiplomacyOffer { Lord = lord, Nation = other, Neighbor = true });
            RunState.AnswerDiplomacy(false);
            Assert.IsEmpty(RunState.Treaties);
            Assert.IsTrue(RunState.InvadableRegions().Exists(r => state.OwnerOf(r) == lord));
        }

        // AI 영주의 상점은 국가 비율(80 / 20) 그대로 — 조약 목록이 없는 탑
        [Test]
        public void AiShopKeepsNationShare()
        {
            WorldState state = RunState.World.State;
            Lord lord = state.Lords.Find(l => l.Alive && !l.IsPlayer && RunState.World.TowerOf(l) != null);
            TowerState tower = RunState.World.TowerOf(lord);
            Assert.IsNull(tower.Treaties);
            BuildingNation nation = RunState.World.NationOf(lord);
            FieldInfo side = typeof(RunState).GetField("_side", Static), shopNation = typeof(RunState).GetField("_shopNation", Static);
            MethodInfo roll = typeof(RunState).GetMethod("RollOffers", Static);
            object was = side.GetValue(null);
            int home = 0, total = 0;
            try
            {
                side.SetValue(null, tower);
                shopNation.SetValue(null, nation);
                for (int i = 0; i < 1000; i++)
                {
                    roll.Invoke(null, new object[] { false });
                    foreach (BuildingDefinition offer in tower.Offers)
                    {
                        total++;
                        if (offer.Nation == BuildingNation.Common || offer.Nation == nation)
                            home++;
                    }
                }
            }
            finally
            {
                side.SetValue(null, was);
            }
            Assert.AreEqual(_config.HomeNationShare, home / (float)total, 0.04f, "AI 영주 진열의 국가 비율");
        }

        private Dictionary<BuildingNation, int> SampleOffers(int rolls)
        {
            var counts = new Dictionary<BuildingNation, int>();
            for (int i = 0; i < rolls; i++)
            {
                RunState.CheatRollOffers();
                foreach (BuildingDefinition offer in RunState.Offers)
                    counts[offer.Nation] = (counts.TryGetValue(offer.Nation, out int n) ? n : 0) + 1;
            }
            return counts;
        }

        // 지금 단계의 등급 확률(풀 = 공용 + 내 국가 + 조약 국가에 있는 등급만) × 그 등급 안에서 nation 기물의 몫
        private float ExpectedShare(BuildingNation nation)
        {
            BuildingDefinition[] pool = System.Array.FindAll(_config.ShopPool, b => b.Nation == BuildingNation.Common || b.Nation == RunState.Nation || RunState.Treaties.Contains(b.Nation));
            int tier = ShopTier;
            float total = 0f, share = 0f;
            foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
            {
                BuildingDefinition[] group = System.Array.FindAll(pool, b => b.Rarity == rarity);
                if (group.Length == 0)
                    continue;
                float weight = _config.RarityWeight(rarity, tier);
                total += weight;
                share += weight * System.Array.FindAll(group, b => b.Nation == nation).Length / group.Length;
            }
            return share / total;
        }
    }
}

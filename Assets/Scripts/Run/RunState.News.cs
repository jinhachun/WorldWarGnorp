using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 이번 턴의 나라 소식 — 고르기 전까지 남는다(턴이 열릴 때 굴린다 — RunState.BeginTurn)
    public class NewsOffer
    {
        public NationNewsDefinition Definition;
        // 고르기 하나에 기물을 골라야 하나(명장 ① · 연금술사 ① · 건국 기념제 ②) — 고를 수 있는 탑의 층은 RunState.NewsTargets
        public bool NeedsBuilding(int choice) => Definition.Choice(choice).Action == NewsAction.PickRankUp || Definition.Choice(choice).Action == NewsAction.Alchemy;
    }

    // 이번 턴의 외교 제안 — 제안한 영주 · 그 국가 · 내 땅과 국경을 맞댔나(아니면 「이번 턴 침략 불가」가 뜻이 없어 글에서 뺀다)
    public class DiplomacyOffer
    {
        public Lord Lord;
        public BuildingNation Nation;
        public bool Neighbor;
    }

    public static partial class RunState
    {
        // ── 나라 소식 · 외교(아웃게임기획 §3-2) — 플레이어만. 턴이 열릴 때(수입 뒤 · 진열 뒤) 굴려 두고, 지도가 상점보다 먼저 팝업으로 띄운다(UI/EventVisitPopup.OpenNews).
        // 결과는 탑 스냅샷에 이미 있는 것(기물 · 등급) 또는 유령에게 필요 없는 런 상태(골드 · 수입 · 상점)만 남긴다 — 런 기록(RunRecord)에 새 칸이 없다
        private static NewsOffer _news;
        private static DiplomacyOffer _diplomacy;
        private static readonly List<NationNewsDefinition> NewsSeen = new List<NationNewsDefinition>();
        // 영구히: 턴 수입 +N · 상점 등급 단계 N턴 앞당김 / 다음 턴 한 번: 수입 ×N · +N(받으면 되돌린다)
        private static int _newsIncome;
        private static int _tierAdvance;
        private static float _nextIncomeScale = 1f;
        private static int _nextIncomeDelta;
        // 대상단이 걸린 턴(Round) · 통상을 받아들인 턴과 그 영주 · 외교 제안이 오는 턴(런 시작에 정한다)
        private static int _heroShopRound = -1;
        private static int _truceRound = -1;
        private static Lord _truceLord;
        private static int _diplomacyTurn;

        // 테스트용 난수(연금술 성공) — null이면 UnityEngine.Random
        public static System.Func<float> NewsRandom;

        public static NewsOffer CurrentNews => _news;
        public static DiplomacyOffer CurrentDiplomacy => _diplomacy;
        public static int DiplomacyTurn => _diplomacyTurn;
        // 연금술사 — 마지막 고르기가 성공했나(화면이 결과를 보여 준다)
        public static bool LastAlchemySucceeded { get; private set; }
        // 내 조약 국가들(맺은 순서)
        public static IReadOnlyList<BuildingNation> Treaties => Player.Treaties;

        private static bool HeroShopNow => _side == Player && _heroShopRound == Round;

        private static void ResetNews()
        {
            _news = null;
            _diplomacy = null;
            NewsSeen.Clear();
            _newsIncome = _tierAdvance = _nextIncomeDelta = 0;
            _nextIncomeScale = 1f;
            _heroShopRound = _truceRound = -1;
            _truceLord = null;
            int[] turns = _config.World.DiplomacyTurns;
            _diplomacyTurn = turns.Length > 0 ? turns[Random.Range(0, turns.Length)] : -1;
        }

        private static void ClearNextIncome()
        {
            _nextIncomeScale = 1f;
            _nextIncomeDelta = 0;
        }

        // 통상을 받아들인 그 턴의 그 영주 — 침략할 수 없다(InvadableRegions)
        private static bool InTruce(Lord lord) => lord != null && _truceRound == Round && lord == _truceLord;

        // 턴이 열릴 때 — 소식 턴이면 안 본 소식 하나(기념제 턴이면 기념제), 외교 턴이면 제안 하나
        private static void RollTurnNews()
        {
            int turn = Round + 1;
            WorldConfig world = _config.World;
            _news = null;
            _diplomacy = null;
            if (System.Array.IndexOf(world.NewsTurns, turn) >= 0)
            {
                NationNewsDefinition news = turn == world.FestivalTurn ? world.Festival : null;
                if (news == null)
                {
                    var pool = new List<NationNewsDefinition>(System.Array.FindAll(world.News, n => n != null && n != world.Festival && !NewsSeen.Contains(n)));
                    news = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
                }
                if (news != null)
                {
                    NewsSeen.Add(news);
                    _news = new NewsOffer { Definition = news };
                    Debug.Log($"[World] 턴 {turn} — 나라 소식 「{news.DisplayName}」");
                }
            }
            if (turn == _diplomacyTurn)
            {
                _diplomacy = RollDiplomacy();
                Debug.Log(_diplomacy != null ? $"[World] 턴 {turn} — 외교 제안 · {NameOf(_diplomacy.Lord)}({_diplomacy.Nation}){(_diplomacy.Neighbor ? " · 이웃" : " · 이웃 아님")}" : $"[World] 턴 {turn} — 외교 제안할 다른 국가 영주가 없다");
            }
        }

        // 제안하는 영주 — 내 국가와 다른 국가(공용 · 이미 조약을 맺은 국가 제외)의 살아 있는 영주 중 내 땅에서 가장 가까운 영주(국경을 맞댄 영주가 먼저 · 같은 거리면 무작위). 없으면 null
        private static DiplomacyOffer RollDiplomacy()
        {
            WorldState state = World.State;
            bool Eligible(Lord lord)
            {
                if (lord == null || !lord.Alive || lord.IsPlayer)
                    return false;
                BuildingNation nation = World.NationOf(lord);
                return nation != BuildingNation.Common && nation != Nation && !Player.Treaties.Contains(nation);
            }
            // 영주 땅끼리의 길로 내 땅에서 한 걸음씩 넓힌다 — 처음 닿은 거리의 후보들
            var seen = new bool[state.Owner.Length];
            var frontier = new List<int>();
            for (int region = 0; region < state.Owner.Length; region++)
                if (state.OwnerOf(region) == state.Player)
                {
                    seen[region] = true;
                    frontier.Add(region);
                }
            for (int step = 1; frontier.Count > 0; step++)
            {
                var next = new List<int>();
                var found = new List<Lord>();
                foreach (int region in frontier)
                    foreach (int link in state.Graph.Links[region])
                    {
                        if (seen[link])
                            continue;
                        seen[link] = true;
                        next.Add(link);
                        Lord owner = state.OwnerOf(link);
                        if (Eligible(owner) && !found.Contains(owner))
                            found.Add(owner);
                    }
                if (found.Count > 0)
                {
                    Lord lord = found[Random.Range(0, found.Count)];
                    return new DiplomacyOffer { Lord = lord, Nation = World.NationOf(lord), Neighbor = step == 1 };
                }
                frontier = next;
            }
            return null;
        }

        // 통상 — 받아들이면 그 국가가 내 진열 무리에 들어오고(런 끝까지), 이번 턴엔 그 영주를 침략할 수 없다
        public static void AnswerDiplomacy(bool accept)
        {
            if (_diplomacy == null)
                return;
            if (accept)
            {
                Player.Treaties.Add(_diplomacy.Nation);
                _truceLord = _diplomacy.Lord;
                _truceRound = Round;
            }
            Debug.Log($"[World] 턴 {Round + 1} — 통상 {(accept ? "받아들임" : "거절")} · {NameOf(_diplomacy.Lord)}({_diplomacy.Nation})");
            _diplomacy = null;
        }

        // 고를 수 있는 탑의 층 — 명장 · 건국 기념제 = 더 오를 수 있는 기물, 연금술 = 신화가 아닌 기물(보관함 제외)
        public static List<int> NewsTargets(int choice)
        {
            var floors = new List<int>();
            if (_news == null || !_news.NeedsBuilding(choice))
                return floors;
            for (int i = 0; i < Player.Field.Length; i++)
                if (!OwnedBuilding.IsEmpty(Player.Field[i]) && Player.Field[i].CanUpgrade)
                    floors.Add(i);
            return floors;
        }

        // 고를 수 있나 — 골드를 내는 고르기는 그만큼 있어야(벌만 있는 고르기는 언제나), 기물을 고르는 고르기는 고를 기물이, 기물을 얻는 고르기는 자리가 있어야
        public static bool CanChooseNews(int choice)
        {
            if (_news == null || choice < 0 || choice > 1)
                return false;
            NewsChoice c = _news.Definition.Choice(choice);
            if (c.Gold < 0 && !c.PenaltyOnly && Player.Gold < -c.Gold)
                return false;
            switch (c.Action)
            {
                case NewsAction.PickRankUp:
                case NewsAction.Alchemy: return NewsTargets(choice).Count > 0;
                case NewsAction.Fire: return System.Array.Exists(Player.Field, b => !OwnedBuilding.IsEmpty(b));
                case NewsAction.GainCommonProducer:
                case NewsAction.GainLegendary: return FirstEmpty(Player.Field) >= 0 || FirstEmpty(Player.Storage) >= 0;
                default: return true;
            }
        }

        // 둘 다 고를 수 없으면 닫기만
        public static bool CanSkipNews => _news != null && !CanChooseNews(0) && !CanChooseNews(1);

        public static void SkipNews()
        {
            if (CanSkipNews)
                _news = null;
        }

        // choice번째를 고른다 — floor = 고른 탑의 층(기물을 고르는 고르기만). 고를 수 없으면 false(아무것도 안 바뀐다)
        public static bool ChooseNews(int choice, int floor = -1)
        {
            if (!CanChooseNews(choice))
                return false;
            if (_news.NeedsBuilding(choice) && !NewsTargets(choice).Contains(floor))
                return false;
            NewsChoice c = _news.Definition.Choice(choice);
            string what = c.Describe().Replace("\n", " · ");
            if (c.Gold > 0)
                GainGold(c.Gold);
            else if (c.Gold < 0)
                Player.Gold = Mathf.Max(0, Player.Gold + c.Gold);
            _newsIncome += c.Income;
            _nextIncomeScale *= c.NextIncomeScale;
            _nextIncomeDelta += c.NextIncome;
            Player.FreeRerolls += c.FreeRerolls;
            _tierAdvance += c.TierAdvance;
            if (c.OfferSlots > 0)
                AddOfferSlots(c.OfferSlots);
            OwnedBuilding[] field = Player.Field;
            switch (c.Action)
            {
                case NewsAction.HeroShop:
                    // 이번 턴의 진열 · 리롤이 전부 영웅 이상 — 이미 깔린 진열 중 영웅 아래는 지금 새로(잠긴 진열도 영웅 이상은 그대로)
                    _heroShopRound = Round;
                    BuildingDefinition[] pool = OfferPool;
                    for (int i = 0; i < Offers.Length; i++)
                        if (Offers[i] != null && Offers[i].Rarity + OfferUpgrades[i] < BuildingRarity.Epic)
                            RollSlot(i, pool);
                    break;
                case NewsAction.PickRankUp:
                    field[floor].Upgrade();
                    OwnedBuilding.NotifyEffect(field[floor]);
                    what += $" · {field[floor].Definition.name} → {field[floor].Rarity}";
                    break;
                case NewsAction.RandomRankUp:
                {
                    List<int> up = new List<int>();
                    for (int i = 0; i < field.Length; i++)
                        if (!OwnedBuilding.IsEmpty(field[i]) && field[i].CanUpgrade)
                            up.Add(i);
                    if (up.Count > 0)
                    {
                        int at = up[Random.Range(0, up.Count)];
                        field[at].Upgrade();
                        OwnedBuilding.NotifyEffect(field[at]);
                        what += $" · {field[at].Definition.name} → {field[at].Rarity}";
                    }
                    break;
                }
                case NewsAction.Fire:
                {
                    // 잃은 기물 등급 +1(신화면 신화)의 무작위 기물이 그 자리에 — 내 진열에 나올 수 있는 기물 중에서, 그 등급으로 시작하는 기물이 없으면 낮은 기물을 그 등급까지 올려서. 공짜(판매 0골드)
                    List<int> burned = new List<int>();
                    for (int i = 0; i < field.Length; i++)
                        if (!OwnedBuilding.IsEmpty(field[i]))
                            burned.Add(i);
                    int at = burned[Random.Range(0, burned.Count)];
                    BuildingRarity to = (BuildingRarity)Mathf.Min((int)field[at].Rarity + 1, (int)BuildingRarity.Mythic);
                    string lost = $"{field[at].Definition.name}({field[at].Rarity})";
                    BuildingDefinition gained = RandomAt(to);
                    field[at] = new OwnedBuilding(gained, to - gained.Rarity, 0, 0, 1f);
                    OwnedBuilding.NotifyEffect(field[at]);
                    what += $" · {lost} → {gained.name}({to})";
                    break;
                }
                case NewsAction.Alchemy:
                {
                    float roll = NewsRandom != null ? NewsRandom() : Random.value;
                    LastAlchemySucceeded = roll < c.Chance;
                    if (LastAlchemySucceeded)
                    {
                        while (field[floor].CanUpgrade)
                            field[floor].Upgrade();
                        OwnedBuilding.NotifyEffect(field[floor]);
                    }
                    what += $" · {field[floor].Definition.name} {(LastAlchemySucceeded ? "신화가 됨" : "실패")}";
                    break;
                }
                case NewsAction.GainCommonProducer:
                {
                    var producers = new List<BuildingDefinition>(System.Array.FindAll(Pool, b => b.Nation == BuildingNation.Common && b.Unit != null && b.Cooldown > 0f));
                    if (producers.Count > 0)
                    {
                        BuildingDefinition pick = RollByTier(producers);
                        Acquire(pick);
                        what += $" · {pick.name}";
                    }
                    break;
                }
                case NewsAction.GainLegendary:
                {
                    BuildingDefinition pick = RandomAt(BuildingRarity.Legendary);
                    Acquire(pick, BuildingRarity.Legendary - pick.Rarity);
                    what += $" · {pick.name}";
                    break;
                }
            }
            Debug.Log($"[World] 턴 {Round + 1} — 나라 소식 「{_news.Definition.DisplayName}」 · {what}");
            _news = null;
            return true;
        }

        // 내 진열에 나올 수 있는 기물(OfferPool) 중 rarity로 시작하는 무작위 하나 — 없으면 그 아래 등급으로 시작하는 것(부르는 쪽이 rarity까지 올린다)
        private static BuildingDefinition RandomAt(BuildingRarity rarity)
        {
            BuildingDefinition[] pool = OfferPool;
            BuildingDefinition[] exact = System.Array.FindAll(pool, b => b.Rarity == rarity);
            if (exact.Length == 0)
                exact = System.Array.FindAll(pool, b => b.Rarity < rarity);
            return exact[Random.Range(0, exact.Length)];
        }

        // 이번 턴 내 상점 등급 확률로 등급을 굴리고 그 등급의 기물 하나(풀에 있는 등급만)
        private static BuildingDefinition RollByTier(List<BuildingDefinition> pool)
        {
            float total = 0f;
            foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
                if (pool.Exists(b => b.Rarity == rarity))
                    total += _config.RarityWeight(rarity, ShopTier);
            float pick = Random.Range(0f, total);
            BuildingRarity rolled = pool[pool.Count - 1].Rarity;
            foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
            {
                if (!pool.Exists(b => b.Rarity == rarity))
                    continue;
                pick -= _config.RarityWeight(rarity, ShopTier);
                if (pick < 0f)
                {
                    rolled = rarity;
                    break;
                }
            }
            List<BuildingDefinition> group = pool.FindAll(b => b.Rarity == rolled);
            return group[Random.Range(0, group.Count)];
        }

        // 진열 칸 영구히 +N — 내 탑의 진열 배열을 늘리고 새 칸을 지금 채운다(화면은 UI/ShopPanel이 칸 수를 보고 다시 세운다)
        private static void AddOfferSlots(int count)
        {
            int old = Player.Offers.Length;
            System.Array.Resize(ref Player.Offers, old + count);
            System.Array.Resize(ref Player.OfferUpgrades, old + count);
            BuildingDefinition[] pool = OfferPool;
            for (int i = old; i < Player.Offers.Length; i++)
                RollSlot(i, pool);
        }
    }
}

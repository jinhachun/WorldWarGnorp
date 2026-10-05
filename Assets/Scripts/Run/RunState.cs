using UnityEngine;

namespace GnorpWar
{
    // 한 런(상점 → 전투 → … 목숨이 다하거나 클리어할 때까지)의 상태. 전투마다 씬을 다시 불러오므로 static으로 살아남는다.
    // 규칙(구매·판매·리롤·자리 바꾸기)도 여기 — UI는 이 메서드만 부른다
    public static class RunState
    {
        public enum Outcome { None, Win, Lose }

        public static bool Started { get; private set; }
        public static int Gold { get; private set; }
        public static int Lives { get; private set; }
        public static int Wins { get; private set; }
        // 0부터. 전투를 하나 끝낼 때마다 +1 (이기든 지든)
        public static int Round { get; private set; }
        public static Outcome LastOutcome { get; private set; }
        public static OwnedBuilding[] Field { get; private set; }
        public static OwnedBuilding[] Storage { get; private set; }
        public static BuildingDefinition[] Offers { get; private set; }

        private static BattleConfig _config;
        // 상점 골드·진열을 이미 준 라운드 — 전투 없이 씬만 다시 불러와도 두 번 주지 않게
        private static int _shopEnteredRound;

        public static bool IsOver => Started && (Lives <= 0 || Wins >= _config.WinsToClear);
        public static bool Cleared => Started && Wins >= _config.WinsToClear;

        // 플레이 모드를 새로 켤 때(도메인 리로드를 끈 설정에서도) 지난 런이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Started = false;

        public static void StartNew(BattleConfig config)
        {
            _config = config;
            Started = true;
            Gold = 0;
            Lives = config.Lives;
            Wins = 0;
            Round = 0;
            LastOutcome = Outcome.None;
            Field = new OwnedBuilding[config.FieldSlots];
            Storage = new OwnedBuilding[config.StorageSlots];
            Offers = new BuildingDefinition[config.OfferCount];
            _shopEnteredRound = -1;
        }

        // 상점 단계에 들어설 때 — 골드 지급 + 무료로 새 진열 (라운드당 한 번)
        public static void EnterShop()
        {
            if (_shopEnteredRound == Round)
                return;
            _shopEnteredRound = Round;
            Gold += _config.RoundGold + (LastOutcome == Outcome.Win ? _config.WinBonus : 0);
            RollOffers();
        }

        public static void FinishBattle(bool won)
        {
            LastOutcome = won ? Outcome.Win : Outcome.Lose;
            if (won)
                Wins++;
            else
                Lives--;
            Round++;
        }

        public static bool CanReroll => Gold >= _config.RerollCost;

        public static void Reroll()
        {
            if (!CanReroll)
                return;
            Gold -= _config.RerollCost;
            RollOffers();
        }

        public static int PriceOf(BuildingDefinition definition) => _config.PriceOf(definition.Rarity);

        // 칸마다 등급을 가중치로 먼저 뽑고(풀에 없는 등급은 빼고), 그 등급의 건물 중 하나
        private static void RollOffers()
        {
            BuildingDefinition[] pool = _config.ShopPool;
            for (int i = 0; i < Offers.Length; i++)
            {
                float total = 0f;
                foreach (BuildingDefinition b in pool)
                    total += _config.RarityWeight(b.Rarity, Round) / CountOf(pool, b.Rarity);
                float pick = Random.Range(0f, total);
                Offers[i] = pool[pool.Length - 1];
                foreach (BuildingDefinition b in pool)
                {
                    pick -= _config.RarityWeight(b.Rarity, Round) / CountOf(pool, b.Rarity);
                    if (pick < 0f)
                    {
                        Offers[i] = b;
                        break;
                    }
                }
            }
        }

        private static int CountOf(BuildingDefinition[] pool, BuildingRarity rarity)
        {
            int count = 0;
            foreach (BuildingDefinition b in pool)
                if (b.Rarity == rarity)
                    count++;
            return count;
        }

        // 이미 가진 건물(필드·보관함)이면 경험치 +1 — 칸이 필요 없다
        public static OwnedBuilding FindOwned(BuildingDefinition definition)
        {
            foreach (OwnedBuilding[] row in new[] { Field, Storage })
                foreach (OwnedBuilding b in row)
                    if (!OwnedBuilding.IsEmpty(b) && b.Definition == definition)
                        return b;
            return null;
        }

        public static bool CanBuy(int offer)
        {
            BuildingDefinition definition = Offers[offer];
            if (definition == null || Gold < PriceOf(definition))
                return false;
            return FindOwned(definition) != null || FirstEmpty(Field) >= 0 || FirstEmpty(Storage) >= 0;
        }

        public static void Buy(int offer)
        {
            if (!CanBuy(offer))
                return;
            BuildingDefinition definition = Offers[offer];
            Gold -= PriceOf(definition);
            Offers[offer] = null;

            OwnedBuilding owned = FindOwned(definition);
            if (owned != null)
            {
                owned.AddExp();
                return;
            }
            int slot = FirstEmpty(Field);
            if (slot >= 0)
                Field[slot] = new OwnedBuilding(definition);
            else
                Storage[FirstEmpty(Storage)] = new OwnedBuilding(definition);
        }

        // 진열을 끌어다 칸에 놓아 산다 — 가진 건물이면 어느 칸에 놓든 경험치 +1, 아니면 빈 칸에만
        public static bool CanBuyInto(int offer, bool storage, int index)
        {
            BuildingDefinition definition = Offers[offer];
            if (definition == null || Gold < PriceOf(definition))
                return false;
            return FindOwned(definition) != null || OwnedBuilding.IsEmpty((storage ? Storage : Field)[index]);
        }

        public static void BuyInto(int offer, bool storage, int index)
        {
            if (!CanBuyInto(offer, storage, index))
                return;
            BuildingDefinition definition = Offers[offer];
            Gold -= PriceOf(definition);
            Offers[offer] = null;

            OwnedBuilding owned = FindOwned(definition);
            if (owned != null)
                owned.AddExp();
            else
                (storage ? Storage : Field)[index] = new OwnedBuilding(definition);
        }

        public static int SellValue(OwnedBuilding building)
        {
            return Mathf.Max(1, Mathf.FloorToInt(PriceOf(building.Definition) * (building.Exp + 1) * _config.SellRatio));
        }

        public static void Sell(bool storage, int index)
        {
            OwnedBuilding[] row = storage ? Storage : Field;
            if (OwnedBuilding.IsEmpty(row[index]))
                return;
            Gold += SellValue(row[index]);
            row[index] = null;
        }

        // 두 칸을 맞바꾼다 — 필드↔보관함도, 빈 칸과도
        public static void Swap(bool storageA, int indexA, bool storageB, int indexB)
        {
            OwnedBuilding[] rowA = storageA ? Storage : Field;
            OwnedBuilding[] rowB = storageB ? Storage : Field;
            (rowA[indexA], rowB[indexB]) = (rowB[indexB], rowA[indexA]);
        }

        private static int FirstEmpty(OwnedBuilding[] row)
        {
            for (int i = 0; i < row.Length; i++)
                if (OwnedBuilding.IsEmpty(row[i]))
                    return i;
            return -1;
        }
    }
}

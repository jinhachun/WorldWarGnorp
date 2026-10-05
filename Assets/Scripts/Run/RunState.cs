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
            StatBook.ClearRun();
        }

        // 상점 단계에 들어설 때 — 골드 지급 + 무료로 새 진열 (라운드당 한 번)
        public static void EnterShop()
        {
            if (_shopEnteredRound == Round)
                return;
            _shopEnteredRound = Round;
            GainGold(_config.RoundGold + (LastOutcome == Outcome.Win ? _config.WinBonus : 0));
            RollOffers();
        }

        // 골드를 얻는다 — 얻을 때마다(금액과 상관없이 한 번) 필드 기물의 골드 훅
        private static void GainGold(int amount)
        {
            Gold += amount;
            ForEachFieldPassive((passive, owned) => passive.OnGoldGained(owned));
        }

        // 상점 훅은 필드의 기물만 — 보관함은 효과가 없다
        private static void ForEachFieldPassive(System.Action<BuildingPassive, OwnedBuilding> call)
        {
            foreach (OwnedBuilding owned in Field)
                if (!OwnedBuilding.IsEmpty(owned))
                    foreach (BuildingPassive passive in owned.Definition.Passives)
                        call(passive, owned);
        }

        // 「구매할 때마다,」 — 산 기물(합쳐졌으면 합쳐진 결과)에서 한 번
        private static void OnBought(OwnedBuilding owned)
        {
            foreach (BuildingPassive passive in owned.Definition.Passives)
                passive.OnBuy(owned);
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
            ForEachFieldPassive((passive, owned) => passive.OnReroll(owned));
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

        // 가진 것(필드·보관함) 중 같은 기물·같은 등급이면서 더 오를 수 있는 것 — 새로 산 시작 등급 기물이 여기에 합쳐진다
        public static OwnedBuilding FindMergeTarget(BuildingDefinition definition, int upgrades, OwnedBuilding exclude = null)
        {
            foreach (OwnedBuilding[] row in new[] { Field, Storage })
                foreach (OwnedBuilding b in row)
                    if (!OwnedBuilding.IsEmpty(b) && b != exclude && b.Definition == definition && b.Upgrades == upgrades && b.CanUpgrade)
                        return b;
            return null;
        }

        // 합쳐서 오른 결과가 또 같은 등급 짝을 만나면 계속 합친다 — 결과는 먼저 있던 짝의 칸에 남는다(플레이어가 놓은 자리).
        // 스택은 합친다(사용자 결정: 등급이 오르면 스택 유지). 합쳐진 결과를 돌려준다
        private static OwnedBuilding MergeInto(OwnedBuilding target)
        {
            target.Upgrade();
            OwnedBuilding pair = FindMergeTarget(target.Definition, target.Upgrades, target);
            while (pair != null && target.CanUpgrade)
            {
                Remove(target);
                pair.Upgrade();
                pair.AddStacks(target.Stacks);
                target = pair;
                pair = FindMergeTarget(target.Definition, target.Upgrades, target);
            }
            return target;
        }

        private static void Remove(OwnedBuilding building)
        {
            foreach (OwnedBuilding[] row in new[] { Field, Storage })
                for (int i = 0; i < row.Length; i++)
                    if (row[i] == building)
                        row[i] = null;
        }

        public static bool CanBuy(int offer)
        {
            BuildingDefinition definition = Offers[offer];
            if (definition == null || Gold < PriceOf(definition))
                return false;
            return FindMergeTarget(definition, 0) != null || FirstEmpty(Field) >= 0 || FirstEmpty(Storage) >= 0;
        }

        public static void Buy(int offer)
        {
            if (!CanBuy(offer))
                return;
            BuildingDefinition definition = Offers[offer];
            Gold -= PriceOf(definition);
            Offers[offer] = null;

            OwnedBuilding target = FindMergeTarget(definition, 0);
            if (target != null)
            {
                OnBought(MergeInto(target));
                return;
            }
            var bought = new OwnedBuilding(definition);
            int slot = FirstEmpty(Field);
            if (slot >= 0)
                Field[slot] = bought;
            else
                Storage[FirstEmpty(Storage)] = bought;
            OnBought(bought);
        }

        // 진열을 끌어다 칸에 놓아 산다 — 합쳐질 짝이 있으면 어느 칸에 놓든 합쳐지고, 아니면 빈 칸에만
        public static bool CanBuyInto(int offer, bool storage, int index)
        {
            BuildingDefinition definition = Offers[offer];
            if (definition == null || Gold < PriceOf(definition))
                return false;
            return FindMergeTarget(definition, 0) != null || OwnedBuilding.IsEmpty((storage ? Storage : Field)[index]);
        }

        public static void BuyInto(int offer, bool storage, int index)
        {
            if (!CanBuyInto(offer, storage, index))
                return;
            BuildingDefinition definition = Offers[offer];
            Gold -= PriceOf(definition);
            Offers[offer] = null;

            OwnedBuilding target = FindMergeTarget(definition, 0);
            OwnedBuilding bought;
            if (target != null)
                bought = MergeInto(target);
            else
                (storage ? Storage : Field)[index] = bought = new OwnedBuilding(definition);
            OnBought(bought);
        }

        public static int SellValue(OwnedBuilding building)
        {
            return Mathf.Max(1, Mathf.FloorToInt(PriceOf(building.Definition) * building.Copies * _config.SellRatio));
        }

        public static void Sell(bool storage, int index)
        {
            OwnedBuilding[] row = storage ? Storage : Field;
            if (OwnedBuilding.IsEmpty(row[index]))
                return;
            int value = SellValue(row[index]);
            row[index] = null;   // 판 기물은 자기 골드 훅을 받지 않는다 · 스택도 같이 사라진다
            GainGold(value);
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

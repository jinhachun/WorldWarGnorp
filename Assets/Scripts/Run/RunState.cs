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
        // 상점 잠금 — 잠긴 채 다음 라운드 상점에 들어오면 진열이 그대로(산 칸만 새로). 다시 누르거나 리롤하거나 새 런이면 풀린다
        public static bool ShopLocked { get; private set; }
        // 이번 라운드 맵 — 상점에 들어올 때 뽑는다(1라운드 = 첫 맵, 이후 목록에서 균등 무작위). 양 진영에 똑같이
        public static MapDefinition Map { get; private set; }

        private static BattleConfig _config;
        // 상점 골드·진열을 이미 준 라운드 — 전투 없이 씬만 다시 불러와도 두 번 주지 않게
        private static int _shopEnteredRound;

        public static bool IsOver => Started && (Lives <= 0 || Wins >= _config.WinsToClear);
        public static bool Cleared => Started && Wins >= _config.WinsToClear;

        // 플레이 모드를 새로 켤 때(도메인 리로드를 끈 설정에서도) 지난 런이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Started = false;
            CheatMap = null;
            CheatEnemyField = null;
        }

        // ── 치트 훅(Dev/CheatPanel만 부른다) — 꺼 둘 때까지(null) 라운드·런이 바뀌어도 남는다
        // 맵 고정 — 상점에 들어올 때 무작위로 뽑는 대신 이 맵
        public static MapDefinition CheatMap { get; private set; }
        // 적 보드 덮어쓰기 — 라운드 표 대신 이 보드(BattleManager.Awake)
        public static OwnedBuilding[] CheatEnemyField;

        public static void CheatSetMap(MapDefinition map)
        {
            CheatMap = map;
            if (map != null)
                Map = map;
        }

        // 라운드만 바꾼다 — 상점 골드·새 진열은 주지 않는다
        public static void CheatSetRound(int round) => Round = _shopEnteredRound = round;

        // 골드 훅(주식시장 등) 없이
        public static void CheatAddGold(int amount) => Gold += amount;

        // 골드 없이 층 올리기 — 다음 층 비용도 그대로
        public static void CheatAddFloor()
        {
            if (Floors >= MaxFloors)
                return;
            var raised = new OwnedBuilding[Field.Length + 1];
            System.Array.Copy(Field, raised, Field.Length);
            Field = raised;
        }

        public static void StartNew(BattleConfig config)
        {
            _config = config;
            Started = true;
            Gold = 0;
            Lives = config.Lives;
            Wins = 0;
            Round = 0;
            LastOutcome = Outcome.None;
            Field = new OwnedBuilding[config.StartFloors];
            FloorRaises = 0;
            Storage = new OwnedBuilding[config.StorageSlots];
            Offers = new BuildingDefinition[config.OfferCount];
            _shopEnteredRound = -1;
            ShopLocked = false;
            StatBook.ClearRun();
            TowerBook.ClearRun();
        }

        // 상점 단계에 들어설 때 — 골드 지급 + 무료로 새 진열 (라운드당 한 번)
        public static void EnterShop()
        {
            if (_shopEnteredRound == Round)
                return;
            _shopEnteredRound = Round;
            GainGold(_config.RoundGold(Round) + (LastOutcome == Outcome.Win ? _config.WinBonus : 0));
            if (ShopLocked)
                RollOffers(onlyEmpty: true);
            else
                RollOffers();
            Map = CheatMap != null ? CheatMap : _config.MapFor(Round);
        }

        public static void ToggleShopLock() => ShopLocked = !ShopLocked;

        // 골드를 얻는다 — 얻을 때마다(금액과 상관없이 한 번) 필드 기물의 골드 훅. 기물 효과(주식시장)도 이걸로 준다
        public static void GainGold(int amount)
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

        // 「구매할 때마다,」 — 산 기물(합쳐졌으면 합쳐진 결과)에서, 필드의 스팀바론이 있으면 2번. 그다음 필드 기물의 "기물을 샀다" 훅(주식시장)
        private static void OnBought(OwnedBuilding owned)
        {
            BuildingPassive.TriggerBuyEffects(owned, Team.Ally, BuildingPassive.BuyRepeats(Field));
            ForEachFieldPassive((passive, self) => passive.OnAnyBought(self, owned));
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

        // 이번 라운드 성 최대 체력에 더할 값(양 진영) — 런 밖이면 0
        public static float BaseHpBonus => Started ? _config.BaseHpBonus(Round) : 0f;

        // 탑 — 필드 칸 하나가 한 층(칸 0 = 1층, 성 바로 위). 층 올리기로 골드를 내고 맨 위에 빈 층을 하나 더한다
        public static int Floors => Field.Length;
        public static int FloorRaises { get; private set; }
        public static int MaxFloors => _config.MaxFloors;
        public static int NextFloorCost => _config.FloorCost(FloorRaises);
        public static bool CanRaiseFloor => Floors < _config.MaxFloors && Gold >= NextFloorCost;

        public static void RaiseFloor()
        {
            if (!CanRaiseFloor)
                return;
            Gold -= NextFloorCost;
            FloorRaises++;
            var raised = new OwnedBuilding[Field.Length + 1];
            System.Array.Copy(Field, raised, Field.Length);
            Field = raised;
        }

        public static int RerollCost => _config.RerollCost;
        public static bool CanReroll => Gold >= _config.RerollCost;

        public static void Reroll()
        {
            if (!CanReroll)
                return;
            Gold -= _config.RerollCost;
            ShopLocked = false;
            RollOffers();
            ForEachFieldPassive((passive, owned) => passive.OnReroll(owned));
        }

        public static int PriceOf(BuildingDefinition definition) => _config.PriceOf(definition.Rarity);

        // 칸마다 등급을 가중치로 먼저 뽑고(풀에 없는 등급은 빼고), 그 등급의 건물 중 하나. onlyEmpty = 산 칸(빈칸)만 새로(상점 잠금)
        private static void RollOffers(bool onlyEmpty = false)
        {
            BuildingDefinition[] pool = _config.ShopPool;
            for (int i = 0; i < Offers.Length; i++)
            {
                if (onlyEmpty && Offers[i] != null)
                    continue;
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
            CompactField();
        }

        // 탑은 중간 층을 비울 수 없다(사용자 10/8) — 기물은 늘 1층부터 빈틈없이. 빠진 자리는 위층이 내려와 메운다
        private static void CompactField()
        {
            int write = 0;
            for (int read = 0; read < Field.Length; read++)
                if (!OwnedBuilding.IsEmpty(Field[read]))
                    Field[write++] = Field[read];
            for (; write < Field.Length; write++)
                Field[write] = null;
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

        // 진열을 끌어다 칸에 놓아 산다 — 합쳐질 짝이 있으면 어느 칸에 놓든 합쳐지고, 아니면 보관함은 빈 칸에만,
        // 탑은 빈 층이 하나라도 있으면 그 층에 끼워 넣는다(위층들이 한 층씩 올라간다 · 맨 위 빈 층보다 높이 놓으면 맨 위 기물 바로 위로)
        public static bool CanBuyInto(int offer, bool storage, int index)
        {
            BuildingDefinition definition = Offers[offer];
            if (definition == null || Gold < PriceOf(definition))
                return false;
            if (FindMergeTarget(definition, 0) != null)
                return true;
            return storage ? OwnedBuilding.IsEmpty(Storage[index]) : FirstEmpty(Field) >= 0;
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
            else if (storage)
                Storage[index] = bought = new OwnedBuilding(definition);
            else
                InsertIntoField(bought = new OwnedBuilding(definition), index);
            OnBought(bought);
        }

        // 층 사이에 끼워 넣기(사용자 10/8: 1 2 3 4 5의 1층·2층 사이에 A → 1 A 2 3 4 5) — at = 넣은 뒤 그 기물의 칸(0..Floors).
        // at 위 기물은 한 칸씩 올라간다. 합치지 않는다(합치기는 층 가운데에 놓을 때). 밀어 올릴 빈 층이 있어야 한다
        public static bool CanBuyInsert(int offer, int at)
        {
            BuildingDefinition definition = Offers[offer];
            return definition != null && Gold >= PriceOf(definition) && at >= 0 && at <= Floors && FirstEmpty(Field) >= 0;
        }

        public static void BuyInsert(int offer, int at)
        {
            if (!CanBuyInsert(offer, at))
                return;
            BuildingDefinition definition = Offers[offer];
            Gold -= PriceOf(definition);
            Offers[offer] = null;
            var bought = new OwnedBuilding(definition);
            InsertIntoField(bought, at);
            OnBought(bought);
        }

        // 보관함이나 탑의 다른 층에서 옮겨 끼워 넣기 — 탑 안 이동은 원래 자리를 빼고 넣는다(빈 층이 없어도 된다).
        // at은 빼기 전 기준 칸 번호(1 2 3 4 5에서 4를 at 1로 → 1 4 2 3 5)
        public static bool CanMoveInsert(bool fromStorage, int from, int at)
        {
            OwnedBuilding[] row = fromStorage ? Storage : Field;
            if (from < 0 || from >= row.Length || OwnedBuilding.IsEmpty(row[from]) || at < 0 || at > Floors)
                return false;
            return !fromStorage || FirstEmpty(Field) >= 0;
        }

        public static void MoveInsert(bool fromStorage, int from, int at)
        {
            if (!CanMoveInsert(fromStorage, from, at))
                return;
            OwnedBuilding[] row = fromStorage ? Storage : Field;
            OwnedBuilding moved = row[from];
            row[from] = null;
            if (!fromStorage)
            {
                CompactField();
                if (at > from)
                    at--;
            }
            InsertIntoField(moved, at);
        }

        // 탑의 index층에 끼워 넣는다 — 그 위 기물들은 한 층씩 올라간다. 빈 층이 있어야 한다
        private static void InsertIntoField(OwnedBuilding building, int index)
        {
            int top = FirstEmpty(Field);
            index = Mathf.Min(index, top);
            for (int i = top; i > index; i--)
                Field[i] = Field[i - 1];
            Field[index] = building;
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
            OwnedBuilding sold = row[index];
            int value = SellValue(sold);
            row[index] = null;   // 판 기물은 자기 골드 훅을 받지 않는다 · 스택도 같이 사라진다
            CompactField();
            GainGold(value);
            foreach (BuildingPassive passive in sold.Definition.Passives)
                passive.OnSell(sold);
        }

        // 이번 판에 등장하는 기물(상점 풀)
        public static BuildingDefinition[] Pool => _config.ShopPool;

        // 산 게 아니라 얻는다(판도라의 상자) — 「구매할 때마다,」·합치기 없이 필드 → 보관함 빈 칸에. 자리가 없으면 못 얻는다
        public static void Acquire(BuildingDefinition definition)
        {
            var acquired = new OwnedBuilding(definition);
            int slot = FirstEmpty(Field);
            if (slot >= 0)
                Field[slot] = acquired;
            else if ((slot = FirstEmpty(Storage)) >= 0)
                Storage[slot] = acquired;
            else
                return;
            OwnedBuilding.NotifyEffect(acquired);   // 받은 칸이 튄다(판도라의 상자 — 판 기물은 이미 없다)
        }

        // 두 칸을 맞바꾼다 — 필드↔보관함도, 빈 칸과도. 탑에 빈 층이 생기면 위층이 내려온다
        public static void Swap(bool storageA, int indexA, bool storageB, int indexB)
        {
            OwnedBuilding[] rowA = storageA ? Storage : Field;
            OwnedBuilding[] rowB = storageB ? Storage : Field;
            (rowA[indexA], rowB[indexB]) = (rowB[indexB], rowA[indexA]);
            CompactField();
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

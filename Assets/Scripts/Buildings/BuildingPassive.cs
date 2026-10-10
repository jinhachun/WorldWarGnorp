using UnityEngine;

namespace GnorpWar
{
    // 기물의 늘 켜진 능력 하나(쿨다운 발동이 아닌 것) — 능력마다 파일 하나, 쓰는 훅만 덮어쓴다.
    // slot = 이 기물의 칸(이웃은 slot±1). 진영 단위로 유닛에게 거는 효과는 UnitEffect
    public abstract class BuildingPassive : ScriptableObject
    {
        // 전투 시작 — 효과가 켜진 뒤
        public virtual void OnBattleStart(BoardRunner board, int slot, int level) { }
        // 런 동안 쌓은 것을 전투 시작 때 유닛 스탯으로 거는 능력(스패너) — 지금 쌓인 만큼 stat에 걸 값. 대상은 이 능력의 IUnitFilter.
        // OnBattleStart가 이 값으로 걸고, 유닛 카드 미리 보기(RunStats.Preview)도 이 값을 읽는다
        public virtual bool BattleStartStat(OwnedBuilding self, UnitStat stat, out StatModifier modifier)
        {
            modifier = default;
            return false;
        }
        // 전투 시작 때 거는 별칭(조립 라인) — unit이 효과를 함께 받는 유닛. 없으면 null
        public virtual UnitDefinition AliasOf(UnitDefinition unit) => null;
        // 다른 칸의 기물이 유닛을 소환한 직후(한 마리마다, 소환 이벤트 뒤) — sourceSlot = 소환한 칸. 어느 칸을 볼지(바로 위·위 전부)는 능력이 정한다
        public virtual void OnSummonedFrom(BoardRunner board, int slot, int sourceSlot, Unit unit) { }
        // 내 타워(본진)의 화살이 적 유닛을 처치한 직후
        public virtual void OnTowerKill(BoardRunner board, int slot, Unit victim) { }

        // 「구매할 때마다,」 — 이 기물을 샀을 때(합쳐졌으면 합쳐진 결과에서) · 영악한 투자가가 다시 발동시킬 때. team = 효과를 받을 진영
        public virtual void OnBuy(OwnedBuilding self, Team team) { }
        // 「구매할 때마다,」 효과가 있나 — 주식시장·영악한 투자가가 읽는다
        public virtual bool HasBuyEffect => false;
        // 「구매할 때마다,」 효과를 몇 번 발동시키나(스팀바론 2) — 보드에서 가장 큰 값
        public virtual int BuyEffectRepeat => 1;
        // 상점 — 내가 기물을 산 직후(주식시장) · 리롤 · 골드를 얻을 때. 필드의 기물만(RunState)
        public virtual void OnAnyBought(OwnedBuilding self, OwnedBuilding bought) { }
        public virtual void OnReroll(OwnedBuilding self) { }
        // 「판매 시,」 — 이 기물을 판 직후(칸은 이미 비었다)
        public virtual void OnSell(OwnedBuilding self) { }
        public virtual void OnGoldGained(OwnedBuilding self) { }

        // 툴팁의 완결 문장 — 지금 등급 값으로 (CLAUDE.md §5-2)
        public abstract string Describe(int level);
        // 칸 아래에 보이는 대표 숫자(지금 등급 값) — 없으면 null
        public virtual string Headline(int level) => null;
        // 전투 동안 칸이 값을 쌓는가(처치 발동 등) — 쌓으면 그 계산 방식. 쌓은 값은 board.AddBattleGain으로
        public virtual bool BattleGain(out StatOp op)
        {
            op = default;
            return false;
        }

        public static bool HasBuyEffects(BuildingDefinition building)
        {
            foreach (BuildingPassive passive in building.Passives)
                if (passive.HasBuyEffect)
                    return true;
            return false;
        }

        public static int BuyRepeats(OwnedBuilding[] board)
        {
            int repeats = 1;
            foreach (OwnedBuilding owned in board)
                if (!OwnedBuilding.IsEmpty(owned))
                    foreach (BuildingPassive passive in owned.Definition.Passives)
                        repeats = Mathf.Max(repeats, passive.BuyEffectRepeat);
            return repeats;
        }

        // 한 기물의 「구매할 때마다,」 효과를 repeats번
        public static void TriggerBuyEffects(OwnedBuilding owned, Team team, int repeats)
        {
            if (HasBuyEffects(owned.Definition))
                OwnedBuilding.NotifyEffect(owned);
            for (int i = 0; i < repeats; i++)
                foreach (BuildingPassive passive in owned.Definition.Passives)
                    passive.OnBuy(owned, team);
        }
    }
}

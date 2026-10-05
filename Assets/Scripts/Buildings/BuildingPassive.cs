using UnityEngine;

namespace GnorpWar
{
    // 기물의 늘 켜진 능력 하나(쿨다운 발동이 아닌 것) — 능력마다 파일 하나, 쓰는 훅만 덮어쓴다.
    // slot = 이 기물의 칸(이웃은 slot±1). 진영 단위로 유닛에게 거는 효과는 UnitEffect
    public abstract class BuildingPassive : ScriptableObject
    {
        // 전투 시작 — 효과가 켜진 뒤
        public virtual void OnBattleStart(BoardRunner board, int slot, int level) { }
        // 인접 기물이 유닛을 소환한 직후(한 마리마다, 소환 이벤트 뒤)
        public virtual void OnNeighborSummoned(BoardRunner board, int slot, Unit unit) { }
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
            for (int i = 0; i < repeats; i++)
                foreach (BuildingPassive passive in owned.Definition.Passives)
                    passive.OnBuy(owned, team);
        }
    }
}

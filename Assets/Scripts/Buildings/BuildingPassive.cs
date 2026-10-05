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
        // 상점 — 「구매할 때마다,」(이 기물을 샀을 때, 합쳐졌으면 합쳐진 결과에서) · 리롤 · 골드를 얻을 때. 리롤·골드는 필드의 기물만(RunState)
        public virtual void OnBuy(OwnedBuilding self) { }
        public virtual void OnReroll(OwnedBuilding self) { }
        public virtual void OnGoldGained(OwnedBuilding self) { }
        // 툴팁의 완결 문장 — 지금 등급 값으로 (CLAUDE.md §5-2)
        public abstract string Describe(int level);
    }
}

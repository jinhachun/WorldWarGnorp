using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 유닛(고른 것)이 죽을 때 확률로 이 기물에서 유닛을 소환한다 (재활용 공장 신화: 내 톱니병사가 죽을 때 10% 확률로 톱니로켓병 1기 — 사용자 10/28).
    // 소환은 죽은 자리가 아니라 이 기물의 층에서(사용자 10/28). 「~로도 취급」되는 유닛도 센다(AppliesFor).
    // 연계 소환이다 — 낸 유닛은 chained, 연계로 태어난 유닛의 죽음 · 다시 일어나는 죽음에는 굴리지 않는다(사용자 10/26)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Death Chance Slot Summon")]
    public class DeathChanceSlotSummonPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _victim;
        [SuffixLabel("%", true)]
        [SerializeField] private float _chance = 10f;
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;

        public override void OnUnitDied(BoardRunner board, int slot, Unit victim)
        {
            if (victim.Team != board.Team || victim.ChainBorn || victim.Reviving || !_victim.AppliesFor(victim.Team, victim.Definition))
                return;
            if (Random.value * 100f >= _chance)
                return;
            board.Summon(_summon, _count, slot, chained: true);
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
        }

        public override string Describe(int level) => $"{_victim.DisplayName}가 죽으면 {_chance:0.##}% 확률로 {_summon.DisplayName} {_count}기를 소환합니다.";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 인접 기물이 이 유닛을 소환할 때마다 이 기물이 다른 유닛을 소환한다 (성당기사단: 사제 → 방패수)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Neighbor Summon Summon")]
    public class NeighborSummonSummonPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _when;
        [SerializeField] private UnitDefinition _summon;
        [SerializeField] private int _count = 1;

        public override void OnNeighborSummoned(BoardRunner board, int slot, Unit unit)
        {
            if (unit.Definition == _when)
                board.Summon(_summon, _count, slot);
        }

        public override string Describe(int level) => $"인접 기물이 {_when.DisplayName}를 소환할 때마다 {_summon.DisplayName} {_count}기를 소환합니다.";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 바로 위 기물이 이 유닛을 소환할 때마다 이 기물이 다른 유닛을 소환한다 (성당기사단: 사제 → 방패수)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Neighbor Summon Summon")]
    public class NeighborSummonSummonPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _when;
        [SerializeField] private UnitDefinition _summon;
        [SerializeField] private int _count = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private int _countPerTier = 1;

        private int Count(int level) => OwnedBuilding.Scaled(_count, _countPerTier, level);

        public override void OnSummonedFrom(BoardRunner board, int slot, int sourceSlot, Unit unit)
        {
            if (sourceSlot == slot + 1 && unit.Definition == _when)
            {
                board.Summon(_summon, Count(board.Slots[slot].Building.Level), slot);
                BuffLinks.ToSlot(board.Team, sourceSlot, slot, BuffKind.Trigger);
            }
        }

        public override string Describe(int level) => $"바로 위 기물이 {_when.DisplayName}를 소환할 때마다 {_summon.DisplayName} {Count(level)}기를 소환합니다.";
    }
}

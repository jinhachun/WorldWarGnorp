using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛을 소환할 때 대신 다른 유닛을 소환한다 (노동의 기계화 · 교황청)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Convert Summon")]
    public class ConvertSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _into;
        [Tooltip("켜면 기물이 소환할 때만 바꾼다(교황청 \"내 기물이 궁수를 소환할 때\")")]
        [SerializeField] private bool _buildingsOnly;

        public override UnitDefinition ConvertSummon(UnitDefinition unit, int sourceSlot, int stacks)
            => _buildingsOnly && sourceSlot < 0 ? unit : _into;
    }
}

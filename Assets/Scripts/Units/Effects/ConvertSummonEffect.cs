using Sirenix.OdinInspector;
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
        [Tooltip("대신 소환한 유닛에 거는 스탯 — 그 등급의 값이 0이면 걸지 않는다")]
        [SerializeField] private StatModifier _modifier;
        [Tooltip("등급이 오를 때마다 _modifier 값에 더한다")]
        [SerializeField] private float _perTier;
        [Tooltip("스탯이 걸리는 등급의 설명 — {0}에 스탯 표기(체력 +10%)")]
        [TextArea]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _modifierDescription = "";

        public override UnitDefinition ConvertSummon(UnitDefinition unit, int sourceSlot, int stacks)
            => _buildingsOnly && sourceSlot < 0 ? unit : _into;

        public override void OnConverted(Unit unit)
        {
            StatModifier modifier = _modifier.Scaled(_perTier, Level(unit.Team));
            if (modifier.Value > 0f)
                unit.AddModifier(modifier);
        }

        public override string Describe(int level)
        {
            StatModifier modifier = _modifier.Scaled(_perTier, level);
            return modifier.Value > 0f ? string.Format(_modifierDescription, modifier.Describe()) : Description;
        }
    }
}

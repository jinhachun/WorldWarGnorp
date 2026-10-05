using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 회복받을 때마다 크기·체력·공격력이 커진다 (순례지: 중보병 각 +5%)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Heal Growth")]
    public class HealGrowthEffect : UnitEffect
    {
        [Tooltip("기준 크기 대비 — 0.05 = +5%")]
        [SerializeField] private float _size = 0.05f;
        [Tooltip("합연산 — 0.05 = +5%")]
        [SerializeField] private float _stats = 0.05f;

        public override void OnHealReceived(Unit unit, float amount, int stacks)
        {
            unit.Grow(_size);
            unit.AddModifier(new StatModifier(UnitStat.MaxHp, StatOp.Percent, _stats));
            unit.AddModifier(new StatModifier(UnitStat.Attack, StatOp.Percent, _stats));
        }
    }
}

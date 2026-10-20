using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(사제)의 회복이 최대 체력을 넘으면, 넘은 만큼 회복받은 아군의 최대 체력이 늘어난다 (성수 우물 신화 — 기획서 §7-1)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Heal Overflow Max Hp")]
    public class HealOverflowMaxHpEffect : UnitEffect
    {
        public override void OnHealLanded(Team team, Unit patient, float amount, float overflow, int stacks)
        {
            if (overflow <= 0f)
                return;
            patient.AddModifier(new StatModifier(UnitStat.MaxHp, StatOp.Flat, overflow));
            BuffLinks.EffectToPoint(team, this, patient.Position, BuffKind.Stat, BuffLabel.Of(new StatModifier(UnitStat.MaxHp, StatOp.Flat, overflow)));
        }
    }
}

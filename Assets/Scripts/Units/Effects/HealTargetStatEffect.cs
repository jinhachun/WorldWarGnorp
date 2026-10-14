using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(사제)의 회복이 아군에 닿을 때마다 그 아군에게 스탯을 건다 (교회 신화: 회복받은 아군의 공격력 +5%).
    // 유닛 하나에 걸리므로 그 유닛이 사는 동안 남는다(전투 동안)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Heal Target Stat")]
    public class HealTargetStatEffect : UnitEffect
    {
        [SerializeField] private StatModifier _stat = new StatModifier(UnitStat.Attack, StatOp.Percent, 0.05f);

        // 던진 순간이 아니라 구슬이 닿아 체력이 실제로 찼을 때 — 빗나간 구슬은 걸리지 않는다(수도원과 같은 자리)
        public override void OnHealLanded(Team team, Unit patient, float amount, float overflow, int stacks)
        {
            patient.AddModifier(_stat);
            BuffLinks.EffectToPoint(team, this, patient.Position, BuffKind.Stat);
        }

        public override string Describe(int level) => string.Format(Description, _stat.Describe());
    }
}

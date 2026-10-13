using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛의 회복이 아군에 닿을 때마다 그 아군의 공격력 +N%(전투 동안 · 닿을 때마다 겹친다) (합치기 보상 업그레이드 「축복의 손길」) — N은 업그레이드의 합쳐진 값
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Heal Target Attack")]
    public class UpgradeHealTargetAttackEffect : UnitEffect
    {
        public override void OnHealLanded(Team team, Unit patient, float amount, float overflow, int stacks)
            => patient.AddModifier(new StatModifier(UnitStat.Attack, StatOp.Percent, Value(team) / 100f));
    }
}

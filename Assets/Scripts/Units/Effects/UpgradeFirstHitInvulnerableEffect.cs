using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 처음 공격받으면 N초간 무적 — 첫 공격의 피해는 그대로 받는다 (합치기 보상 업그레이드 「야전 병원」) — N은 업그레이드의 합쳐진 값
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade First Hit Invulnerable")]
    public class UpgradeFirstHitInvulnerableEffect : UnitEffect
    {
        public override bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks)
        {
            if (unit.UseOnce(this))
                unit.GrantInvulnerable(Value(unit.Team));
            return false;
        }
    }
}

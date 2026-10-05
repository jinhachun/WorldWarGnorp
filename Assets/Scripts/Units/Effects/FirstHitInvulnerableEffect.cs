using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 처음 공격받으면 N초간 무적 — 첫 공격의 피해는 그대로 받는다 (공주 루니카)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/First Hit Invulnerable")]
    public class FirstHitInvulnerableEffect : UnitEffect
    {
        [SerializeField] private float _seconds = 3f;

        public override bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks)
        {
            if (unit.UseOnce(this))
                unit.GrantInvulnerable(_seconds);
            return false;
        }
    }
}

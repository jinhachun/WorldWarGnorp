using UnityEngine;

namespace GnorpWar
{
    // 돌격형(돌격 배율 > 1)에게 맞으면 나는 버티고 공격한 쪽이 튕겨난다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Reflect Charge")]
    public class ReflectChargeEffect : UnitEffect
    {
        public override bool OnHit(Unit unit, Unit attacker, Vector2 hitDirection, float push, int stacks)
        {
            if (attacker == null || !attacker.IsAlive || attacker.Definition.ChargeDamageMultiplier <= 1f)
                return false;
            attacker.TakeDamage(0f, -hitDirection, push, null);
            return true;
        }
    }
}

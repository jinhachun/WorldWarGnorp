using UnityEngine;

namespace GnorpWar
{
    // 원거리 공격 때 서로 다른 적에게 한 발 더 (적이 하나뿐이면 한 발)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Double Shot")]
    public class DoubleShotEffect : UnitEffect
    {
        public override void OnRangedShot(Unit unit, IDamageable target, int stacks)
        {
            if (unit.FindOtherTarget(target, out Vector2 point))
                unit.FireProjectile(point);
        }
    }
}

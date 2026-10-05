using UnityEngine;

namespace GnorpWar
{
    // 적 머리 위에 새로 내려앉으면 그 적에게 피해 (계속 서 있는 동안은 다시 안 준다)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Stomp")]
    public class StompEffect : UnitEffect
    {
        [SerializeField] private float _damage = 24f;

        public override void OnLandedOnEnemy(Unit unit, Unit enemy, int stacks)
        {
            enemy.TakeDamage(_damage * unit.DamageScale, Vector2.down, 1f, unit);
        }
    }
}

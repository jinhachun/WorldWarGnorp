using UnityEngine;

namespace GnorpWar
{
    [CreateAssetMenu(menuName = "GnorpWar/Tower Definition")]
    public class TowerDefinition : ScriptableObject
    {
        [SerializeField] private float _maxHp = 300f;
        [SerializeField] private float _attackDamage = 8f;
        [SerializeField] private float _attackInterval = 1f;
        [Tooltip("탑 중심에서 적 콜라이더 가장자리까지")]
        [SerializeField] private float _attackRange = 10f;
        [SerializeField] private float _pushPower = 0.5f;
        [SerializeField] private Projectile _projectile;
        [SerializeField] private float _projectileArcHeight = 2.5f;

        public float MaxHp => _maxHp;
        public float AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float AttackRange => _attackRange;
        public float PushPower => _pushPower;
        public Projectile Projectile => _projectile;
        public float ProjectileArcHeight => _projectileArcHeight;
    }
}

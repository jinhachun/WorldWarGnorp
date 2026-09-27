using UnityEngine;

namespace GnorpWar
{
    [CreateAssetMenu(menuName = "GnorpWar/Unit Definition")]
    public class UnitDefinition : ScriptableObject
    {
        [SerializeField] private float _moveSpeed = 1.5f;
        [Tooltip("발바닥이 올라가는 높이(월드 유닛). 키 1짜리 유닛 머리 위에 올라서려면 1보다 조금 커야 한다.")]
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _maxHp = 100f;
        [SerializeField] private float _attackDamage = 10f;
        [SerializeField] private float _attackInterval = 1f;
        [Tooltip("내 중심에서 상대 콜라이더 가장자리까지. 몸 반폭이 0.5라 0.5 이하면 맞닿아도 못 때린다.")]
        [SerializeField] private float _attackRange = 0.8f;
        [Tooltip("맞았을 때 뒤로 밀려나는 속도")]
        [SerializeField] private float _hitKnockback = 2f;
        [Tooltip("산에서의 층 순위. 낮을수록 아래(탱커 0 · 근접 1 · 원딜 2). 위 유닛 순위가 아래보다 낮으면 자리를 바꾼다")]
        [SerializeField] private int _stackRank = 1;
        [SerializeField] private AttackType _attackType = AttackType.Melee;
        [Tooltip("때린 상대가 밀려나는 배율 (상대의 Hit Knockback × 이 값)")]
        [SerializeField] private float _pushPower = 1f;
        [Tooltip("원거리 전용")]
        [SerializeField] private Projectile _projectile;
        [Tooltip("원거리 전용 — 화살이 발사 지점·목표 중 높은 쪽보다 이만큼 더 솟았다가 떨어진다. 거리와 상관없이 늘 포물선")]
        [SerializeField] private float _projectileArcHeight = 2.5f;
        [SerializeField] private float _cost = 50f;
        [Tooltip("이 유닛이 적으로 나와 죽었을 때 플레이어가 받는 자원")]
        [SerializeField] private float _killReward = 20f;
        [SerializeField] private float _summonCooldown = 1.5f;

        public float MoveSpeed => _moveSpeed;
        public float JumpHeight => _jumpHeight;
        public float MaxHp => _maxHp;
        public float AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float AttackRange => _attackRange;
        public float HitKnockback => _hitKnockback;
        public int StackRank => _stackRank;
        public AttackType AttackType => _attackType;
        public float PushPower => _pushPower;
        public Projectile Projectile => _projectile;
        public float ProjectileArcHeight => _projectileArcHeight;
        public float Cost => _cost;
        public float KillReward => _killReward;
        public float SummonCooldown => _summonCooldown;
    }
}

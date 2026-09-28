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
        [Tooltip("큰 넉백(냥코식) — 체력이 이 비율 아래로 처음 떨어질 때 한 번 크게 튕겨난다")]
        [Range(0f, 1f)]
        [SerializeField] private float _bigKnockbackAt = 0.5f;
        [Tooltip("큰 넉백의 후방 속도")]
        [SerializeField] private float _bigKnockbackSpeed = 6f;
        [Tooltip("산에서의 층 순위. 낮을수록 아래(탱커 0 · 근접 1 · 원딜 2). 위 유닛 순위가 아래보다 낮으면 자리를 바꾼다")]
        [SerializeField] private int _stackRank = 1;
        [SerializeField] private AttackType _attackType = AttackType.Melee;
        [Tooltip("때린 상대가 밀려나는 배율 (상대의 Hit Knockback × 이 값)")]
        [SerializeField] private float _pushPower = 1f;
        [Tooltip("돌격 — 적 없이 이 시간 이상 달려온 뒤의 첫 타격이 돌격이 된다. 배율이 1이면 돌격 없음")]
        [SerializeField] private float _chargeReadySeconds = 1f;
        [SerializeField] private float _chargeDamageMultiplier = 1f;
        [SerializeField] private float _chargePushMultiplier = 1f;
        [Tooltip("원거리 전용")]
        [SerializeField] private Projectile _projectile;
        [Tooltip("원거리 전용 — 화살이 발사 지점·목표 중 높은 쪽보다 이만큼 더 솟았다가 떨어진다. 거리와 상관없이 늘 포물선")]
        [SerializeField] private float _projectileArcHeight = 2.5f;
        [Tooltip("원거리 전용 — 0이면 단일 대상. 0보다 크면 착탄 지점 반경 안의 적 전부에게 피해 + 바깥·위로 날림")]
        [SerializeField] private float _projectileSplashRadius = 0f;
        [Tooltip("화염 전용(AttackType.Flame) — 불길 띠의 두께. 길이는 Attack Range")]
        [SerializeField] private float _flameThickness = 2f;
        [Tooltip("회복 전용(AttackType.Heal) — 회복 투사체 한 번에 채우는 체력")]
        [SerializeField] private float _healAmount = 0f;
        [Tooltip("이 유닛의 업그레이드 (아군에게만 적용)")]
        [SerializeField] private UpgradeKind _upgrade = UpgradeKind.None;
        [SerializeField] private float _upgradeCost = 100f;
        [Tooltip("업그레이드 수치 — SwordEscort: 함께 소환될 확률(0~1) · KnightVaultToArchers: 뛰어넘는 높이")]
        [SerializeField] private float _upgradeValue = 0f;
        [SerializeField] private float _cost = 50f;
        [SerializeField] private float _summonCooldown = 1.5f;

        public float MoveSpeed => _moveSpeed;
        public float JumpHeight => _jumpHeight;
        public float MaxHp => _maxHp;
        public float AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float AttackRange => _attackRange;
        public float HitKnockback => _hitKnockback;
        public float BigKnockbackAt => _bigKnockbackAt;
        public float BigKnockbackSpeed => _bigKnockbackSpeed;
        public int StackRank => _stackRank;
        public AttackType AttackType => _attackType;
        public float PushPower => _pushPower;
        public float ChargeReadySeconds => _chargeReadySeconds;
        public float ChargeDamageMultiplier => _chargeDamageMultiplier;
        public float ChargePushMultiplier => _chargePushMultiplier;
        public Projectile Projectile => _projectile;
        public float ProjectileArcHeight => _projectileArcHeight;
        public float ProjectileSplashRadius => _projectileSplashRadius;
        public float FlameThickness => _flameThickness;
        public float HealAmount => _healAmount;
        public UpgradeKind Upgrade => _upgrade;
        public float UpgradeCost => _upgradeCost;
        public float UpgradeValue => _upgradeValue;
        public float Cost => _cost;
        public float SummonCooldown => _summonCooldown;
    }
}

using UnityEngine;

namespace GnorpWar
{
    [CreateAssetMenu(menuName = "GnorpWar/Unit Definition")]
    public class UnitDefinition : ScriptableObject
    {
        [Tooltip("버튼에 뜨는 이름")]
        [SerializeField] private string _displayName = "";
        [SerializeField] private UnitCategory _category;
        [Tooltip("진영별 프리팹 — 건물이 생산할 때 진영에 맞는 쪽을 꺼낸다")]
        [SerializeField] private Unit _allyPrefab;
        [SerializeField] private Unit _enemyPrefab;
        [SerializeField] private float _moveSpeed = 1.5f;
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
        [Tooltip("점프 착지 충격(코끼리) — 이 간격(초)마다 제자리에서 뛰었다가, 땅에 닿으면 반경 안의 적 유닛(탑·기지 제외)을 띄우며 피해. 0이면 없음")]
        [SerializeField] private float _slamInterval = 0f;
        [Tooltip("점프 착지 충격 — 스스로 뛰는 높이")]
        [SerializeField] private float _slamJumpHeight = 3f;
        [Tooltip("점프 착지 충격 — 착지 지점(발밑)에서의 반경")]
        [SerializeField] private float _slamRadius = 5f;
        [SerializeField] private float _slamDamage = 20f;
        [Tooltip("점프 착지 충격 — 맞은 적이 떠오르는 높이")]
        [SerializeField] private float _slamLiftHeight = 3f;
        [Tooltip("점프 착지 충격 — 맞은 적이 조종 불능인 시간(초)")]
        [SerializeField] private float _slamStun = 0.8f;

        public string DisplayName => _displayName;
        public UnitCategory Category => _category;
        public Unit PrefabFor(Team team) => team == Team.Ally ? _allyPrefab : _enemyPrefab;
        public float MoveSpeed => _moveSpeed;
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
        public float SlamInterval => _slamInterval;
        public float SlamJumpHeight => _slamJumpHeight;
        public float SlamRadius => _slamRadius;
        public float SlamDamage => _slamDamage;
        public float SlamLiftHeight => _slamLiftHeight;
        public float SlamStun => _slamStun;
    }
}

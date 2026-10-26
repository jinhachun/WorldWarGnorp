using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    [CreateAssetMenu(menuName = "GnorpWar/Unit Definition")]
    // IUnitFilter — 이 유닛 하나만 고른다. 「영구히」 강화의 출처가 유닛 자신일 때(훈련장 — 고른 유닛마다 한 줄 · 탑 스냅샷이 이 에셋의 ID로 적는다)
    public class UnitDefinition : ScriptableObject, IUnitFilter
    {
        public bool AppliesTo(UnitDefinition unit) => unit == this;

        [Tooltip("버튼에 뜨는 이름")]
        [BoxGroup("기본")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
        [BoxGroup("기본")]
        [SerializeField] private UnitCategory _category;
        [Tooltip("진영별 프리팹 — 건물이 생산할 때 진영에 맞는 쪽을 꺼낸다")]
        [BoxGroup("참조")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Unit _allyPrefab;
        [BoxGroup("참조")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Unit _enemyPrefab;
        [BoxGroup("전투 수치")]
        [SuffixLabel("칸/초", true)]
        [SerializeField] private float _moveSpeed = 1.5f;
        [BoxGroup("전투 수치")]
        [SerializeField] private float _maxHp = 100f;
        [BoxGroup("전투 수치")]
        [SerializeField] private float _attackDamage = 10f;
        [BoxGroup("전투 수치")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _attackInterval = 1f;
        [Tooltip("내 중심에서 상대 콜라이더 가장자리까지. 몸 반폭이 0.5라 0.5 이하면 맞닿아도 못 때린다.")]
        [BoxGroup("전투 수치")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _attackRange = 0.8f;
        [Tooltip("맞았을 때 뒤로 밀려나는 속도")]
        [BoxGroup("넉백")]
        [SuffixLabel("칸/초", true)]
        [SerializeField] private float _hitKnockback = 2f;
        [Tooltip("큰 넉백(냥코식) — 체력이 이 비율 아래로 처음 떨어질 때 한 번 크게 튕겨난다")]
        [Range(0f, 1f)]
        [BoxGroup("넉백")]
        [SerializeField] private float _bigKnockbackAt = 0.5f;
        [Tooltip("큰 넉백의 후방 속도")]
        [BoxGroup("넉백")]
        [SuffixLabel("칸/초", true)]
        [SerializeField] private float _bigKnockbackSpeed = 6f;
        [Tooltip("산에서의 층 순위. 낮을수록 아래(탱커 0 · 근접 1 · 원딜 2). 위 유닛 순위가 아래보다 낮으면 자리를 바꾼다")]
        [BoxGroup("전투 수치")]
        [SerializeField] private int _stackRank = 1;
        [BoxGroup("전투 수치")]
        [SerializeField] private AttackType _attackType = AttackType.Melee;
        [Tooltip("때린 상대가 밀려나는 배율 (상대의 Hit Knockback × 이 값)")]
        [BoxGroup("전투 수치")]
        [SerializeField] private float _pushPower = 1f;
        [Tooltip("돌격 — 적 없이 이 시간 이상 달려온 뒤의 첫 타격이 돌격이 된다. 배율이 1이면 돌격 없음")]
        [BoxGroup("돌격")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _chargeReadySeconds = 1f;
        [BoxGroup("돌격")]
        [SerializeField] private float _chargeDamageMultiplier = 1f;
        [BoxGroup("돌격")]
        [SerializeField] private float _chargePushMultiplier = 1f;
        [Tooltip("원거리 전용")]
        [BoxGroup("참조")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Projectile _projectile;
        [Tooltip("원거리 전용 — 화살이 발사 지점·목표 중 높은 쪽보다 이만큼 더 솟았다가 떨어진다. 거리와 상관없이 늘 포물선")]
        [BoxGroup("원거리")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _projectileArcHeight = 2.5f;
        [Tooltip("원거리 전용 — 0이면 단일 대상. 0보다 크면 착탄 지점 반경 안의 적 전부에게 피해 + 바깥·위로 날림")]
        [BoxGroup("원거리")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _projectileSplashRadius = 0f;
        [Tooltip("화염 전용(AttackType.Flame) — 불길 띠의 두께. 길이는 Attack Range")]
        [BoxGroup("화염 · 회복")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _flameThickness = 2f;
        [Tooltip("지금 읽는 곳 없음 — 회복량 · 교황청 내려치기 피해 · 전투력 표시가 모두 공격력(Attack Damage)을 따른다(사용자 10/22)")]
        [BoxGroup("화염 · 회복")]
        [SerializeField] private float _healAmount = 0f;
        [Tooltip("점프 착지 충격(코끼리) — 이 간격(초)마다 제자리에서 뛰었다가, 땅에 닿으면 반경 안의 적 유닛(탑·기지 제외)을 띄우며 피해. 0이면 없음")]
        [BoxGroup("점프 착지 충격")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _slamInterval = 0f;
        [Tooltip("점프 착지 충격 — 스스로 뛰는 높이")]
        [BoxGroup("점프 착지 충격")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _slamJumpHeight = 3f;
        [Tooltip("점프 착지 충격 — 착지 지점(발밑)에서의 반경")]
        [BoxGroup("점프 착지 충격")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _slamRadius = 5f;
        [BoxGroup("점프 착지 충격")]
        [SerializeField] private float _slamDamage = 20f;
        [Tooltip("점프 착지 충격 — 맞은 적이 떠오르는 높이")]
        [BoxGroup("점프 착지 충격")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _slamLiftHeight = 3f;
        [Tooltip("점프 착지 충격 — 맞은 적이 조종 불능인 시간(초)")]
        [BoxGroup("점프 착지 충격")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _slamStun = 0.8f;
        [Tooltip("지을 작은 타워 프리팹(양 진영 같은 것 — 진영은 지을 때 정한다)")]
        // 짓기 전용(AttackType.Build) — 건축가
        [BoxGroup("짓기 전용 — 건축가")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Base _buildPrefab;
        [Tooltip("자리에 도착해 짓는 데 걸리는 시간(초)")]
        [BoxGroup("짓기 전용 — 건축가")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _buildSeconds = 2f;
        [Tooltip("평지 — 타워 폭 안의 땅 높이 차가 이 값 이하인 자리만")]
        [BoxGroup("짓기 전용 — 건축가")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _buildFlatness = 0.25f;
        [Tooltip("다른 작은 타워(짓는 중인 자리 포함)와 중심 사이 최소 거리")]
        [BoxGroup("짓기 전용 — 건축가")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _buildSpacing = 2.5f;
        [Tooltip("성문 앞 비움 — 성 앞쪽 끝에서 이만큼 떨어진 곳부터 찾는다(유닛이 나오는 자리)")]
        [BoxGroup("짓기 전용 — 건축가")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _buildGateClearance = 3f;
        [Tooltip("타고난 효과 — 유닛 카드의 특성 줄에 글(효과의 「특성 줄」)을 싣는다. 표시용이다: 효과를 켜는 것은 이 유닛을 내는 기물이다")]
        [BoxGroup("참조")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UnitEffect[] _traitEffects = new UnitEffect[0];
        [Tooltip("늘 켜진 효과 — 이 유닛이 나오면(어느 기물로 나오든) 그 진영에 켠다(Unit.OnEnable). 특성 줄에도 싣는다. 같은 효과를 기물에 또 달지 말 것")]
        [BoxGroup("참조")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UnitEffect[] _innateEffects = new UnitEffect[0];

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        // 유닛 카드의 특성 줄들(타고난 효과의 글, 숫자는 그 효과의 값) — 돌격 · 범위 같은 줄(UI/BuildingTooltip.UnitInfo) 뒤에 붙인다
        public string[] TraitLines => System.Array.ConvertAll(Traits, effect => effect.Trait);
        // effect가 이 유닛의 타고난 효과인가 — 카드의 「기물이 켜 준 효과」 줄에서 같은 내용을 다시 쓰지 않게 거른다
        public bool HasTrait(UnitEffect effect) => System.Array.IndexOf(Traits, effect) >= 0;
        public UnitEffect[] InnateEffects => _innateEffects;
        // 특성 줄에 싣는 효과 = 표시용 타고난 효과 + 늘 켜진 효과
        private UnitEffect[] Traits => _innateEffects.Length == 0 ? _traitEffects : System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(_traitEffects, _innateEffects));

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
        public Base BuildPrefab => _buildPrefab;
        public float BuildSeconds => _buildSeconds;
        public float BuildFlatness => _buildFlatness;
        public float BuildSpacing => _buildSpacing;
        public float BuildGateClearance => _buildGateClearance;
    }
}

using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    [CreateAssetMenu(menuName = "GnorpWar/Base Definition")]
    public class BaseDefinition : ScriptableObject
    {
        [SerializeField] private float _maxHp = 500f;

        [Header("화살 — 본진은 타워다")]
        [SerializeField] private float _attackDamage = 160f;
        [SuffixLabel("초", true)]
        [SerializeField] private float _attackInterval = 1f;
        [Tooltip("본진 중심에서 적 유닛 중심까지")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _attackRange = 10f;
        [SerializeField] private float _pushPower = 0.5f;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Projectile _projectile;
        [SuffixLabel("칸", true)]
        [SerializeField] private float _projectileArcHeight = 2.5f;

        [Header("작은 타워(건축가가 짓는다) — 본진 대비 배율. 공격 간격·사거리는 본진과 같다")]
        [SerializeField] private float _smallAttackScale = 0.5f;
        [Tooltip("본진 최대 체력(라운드 보너스 포함, 기물 보너스 전) × 이 값")]
        [SerializeField] private float _smallHpScale = 0.25f;

        [Tooltip("전투가 끝나면 진 쪽 본진이 땅속으로 꺼지는 연출(Fx/BaseCollapse) 길이 — 다 꺼진 뒤 결과 창이 뜬다. 0이면 바로")]
        [SuffixLabel("초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _collapseSeconds = 1f;
        [Tooltip("무너지기 전에 카메라가 진 쪽 본진으로 옮겨 가며 확대되는 길이(계단식)")]
        [SuffixLabel("초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _collapseCameraSeconds = 0.4f;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public float MaxHp => _maxHp;
        public float AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float AttackRange => _attackRange;
        public float PushPower => _pushPower;
        public Projectile Projectile => _projectile;
        public float ProjectileArcHeight => _projectileArcHeight;
        public float SmallAttackScale => _smallAttackScale;
        public float SmallHpScale => _smallHpScale;
        public float CollapseSeconds => _collapseSeconds;
        public float CollapseCameraSeconds => _collapseCameraSeconds;
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 진영 본진 — 체력이 0이 되면 진다. 본진은 타워라서 사거리 안의 가장 가까운 적 유닛에게 화살을 쏜다
    public class Base : MonoBehaviour, IDamageable
    {
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };

        [SerializeField] private BaseDefinition _definition;
        [SerializeField] private Team _team;
        [Tooltip("화살이 나가는 자리(본진 꼭대기)")]
        [SerializeField] private Transform _muzzle;

        private static readonly System.Collections.Generic.List<Base> All = new System.Collections.Generic.List<Base>();

        private readonly System.Collections.Generic.List<Collider2D> _overlaps = new System.Collections.Generic.List<Collider2D>();
        private float _hp;
        private float _attackCooldown;
        private Collider2D _collider;

        public event System.Action<Base> Destroyed;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public float Hp01 => Mathf.Clamp01(_hp / _definition.MaxHp);

        // 새 유닛이 같은 진영 성을 몸으로 통과하게 한다 — 성문 안에서 나와 걸어 나간다. 성 뒤쪽 BackStop과는 그대로 부딪힌다
        public static void IgnoreOwnBase(Team team, Collider2D unitCollider)
        {
            foreach (Base b in All)
                if (b._team == team)
                    Physics2D.IgnoreCollision(unitCollider, b._collider);
        }

        private void Awake()
        {
            _hp = _definition.MaxHp;
            _collider = GetComponent<Collider2D>();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void FixedUpdate()
        {
            _attackCooldown -= Time.fixedDeltaTime;
            if (_attackCooldown > 0f || !FindTarget(out Vector2 targetPoint))
                return;

            Projectile shot = Pooled.Get(_definition.Projectile, _muzzle.position, Quaternion.identity);
            shot.Launch(_team, _definition.AttackDamage, _definition.PushPower, targetPoint, _definition.ProjectileArcHeight, 0f, this);
            _attackCooldown = _definition.AttackInterval;
        }

        private bool FindTarget(out Vector2 targetPoint)
        {
            Vector2 center = transform.position;
            Physics2D.OverlapCircle(center, _definition.AttackRange, SolidOnly, _overlaps);
            targetPoint = default;
            float best = float.MaxValue;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit unit) || unit.Team == _team || !unit.IsAlive)
                    continue;

                float distance = ((Vector2)col.bounds.center - center).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    targetPoint = col.bounds.center;
                }
            }
            return best < float.MaxValue;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker)
        {
            if (!IsAlive)
                return;

            _hp -= amount;
            DamageNumbers.Damage(this, _team, new Vector2(_collider.bounds.center.x, _collider.bounds.max.y), amount);
            if (!IsAlive)
            {
                Debug.Log($"[Base] {_team} 기지 파괴");
                gameObject.SetActive(false);
                Destroyed?.Invoke(this);
            }
        }
    }
}

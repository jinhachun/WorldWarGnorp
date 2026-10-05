using UnityEngine;

namespace GnorpWar
{
    // 투사체(화살·돌) — 중력으로 포물선을 그리며 날아가 처음 닿은 적에게 피해. 아군은 통과.
    // 착탄 범위가 있으면(돌) 닿은 자리 주변 적 전부에게 피해를 주고 바깥·위로 날린다
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        private const float Lifetime = 4f;
        // 범위 착탄 시 날아가는 방향에 섞는 위쪽 성분 — 옆으로만 밀면 산에 막혀 날아가 보이지 않는다
        private const float SplashLiftBias = 1f;
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };

        private Rigidbody2D _rb;
        private Team _team;
        private float _damage;
        private float _push;
        private float _splashRadius;
        // 0보다 크면 회복 투사체 — 적·던진 본인·체력이 가득 찬 아군은 통과하고, 처음 닿은 다친 아군을 회복
        private float _heal;
        private Unit _owner;
        // 쏜 쪽(처치자 판정) — 유닛은 풀에서 다시 쓰이므로 쏠 때의 생애 번호가 맞을 때만 그 유닛으로 친다
        private IDamageable _shooter;
        private int _shooterLife;
        private bool _spent;
        // 관통 — 적을 맞혀도 멈추지 않고 땅·벽에 닿을 때까지. 한 적은 한 번만
        private bool _pierce;
        private readonly System.Collections.Generic.HashSet<IDamageable> _pierced = new System.Collections.Generic.HashSet<IDamageable>();
        private float _lifeTimer;
        private readonly System.Collections.Generic.List<Collider2D> _splashHits = new System.Collections.Generic.List<Collider2D>();

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        // 풀에서 다시 꺼낼 때마다 — 지난번 비행의 흔적(회복 종류)을 지운다
        private void OnEnable()
        {
            _spent = false;
            _heal = 0f;
            _owner = null;
            _shooter = null;
            _pierce = false;
            _pierced.Clear();
            _splashRadius = 0f;
            _lifeTimer = Lifetime;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        // 꼭짓점 높이를 고정(발사 지점·목표 중 높은 쪽 + arcHeight)하고 거기서 속도를 역산한다.
        // 수평 속도를 고정하면 가깝거나 같은 높이의 적에게는 거의 직선이 되므로 쓰지 않는다
        public void Launch(Team team, float damage, float push, Vector2 targetPoint, float arcHeight, float splashRadius, IDamageable shooter)
        {
            _team = team;
            _damage = damage;
            _push = push;
            _splashRadius = splashRadius;
            _shooter = shooter;
            _shooterLife = shooter is Unit unit ? unit.Life : 0;

            Vector2 origin = _rb.position;
            float gravity = -Physics2D.gravity.y * _rb.gravityScale;
            float apex = Mathf.Max(origin.y, targetPoint.y) + arcHeight;
            float riseSpeed = Mathf.Sqrt(2f * gravity * (apex - origin.y));
            _rb.linearVelocity = new Vector2((targetPoint.x - origin.x) / FlightTime(targetPoint, arcHeight), riseSpeed);
            _lifeTimer = Lifetime;
        }

        // Launch 뒤에 — 이번 비행은 적을 관통한다(관통탄 특허국)
        public void Pierce() => _pierce = true;

        // 쏜 유닛이 그사이 죽어 풀에서 다른 유닛으로 다시 쓰였으면 처치자 없음
        private IDamageable Shooter => _shooter is Unit unit && unit.Life != _shooterLife ? null : _shooter;

        private float FlightTime(Vector2 targetPoint, float arcHeight)
        {
            float gravity = -Physics2D.gravity.y * _rb.gravityScale;
            float apex = Mathf.Max(_rb.position.y, targetPoint.y) + arcHeight;
            return Mathf.Sqrt(2f * (apex - _rb.position.y) / gravity) + Mathf.Sqrt(2f * (apex - targetPoint.y) / gravity);
        }

        public void LaunchHeal(Unit owner, float amount, Vector2 targetPoint, float arcHeight)
        {
            Team team = owner.Team;
            _owner = owner;
            _heal = amount;
            Launch(team, 0f, 0f, targetPoint, arcHeight, 0f, null);
        }

        private void FixedUpdate()
        {
            _lifeTimer -= Time.fixedDeltaTime;
            if (_lifeTimer <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            // 그림은 오른쪽을 향해 그려져 있다 — 날아가는 방향으로 머리를 돌린다
            Vector2 v = _rb.linearVelocity;
            if (v.sqrMagnitude > 0.0001f)
                _rb.MoveRotation(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent || other.isTrigger)
                return;

            if (_heal > 0f)
            {
                if (other.TryGetComponent(out IDamageable anyone) && anyone.Team != _team)
                    return;   // 적은 통과
                if (other.TryGetComponent(out Unit ally))
                {
                    if (ally == _owner || !ally.IsDamaged)
                        return;   // 던진 본인·멀쩡한 아군은 통과
                    ally.Heal(_heal);
                }
                _spent = true;
                gameObject.SetActive(false);
                return;
            }

            if (other.TryGetComponent(out IDamageable damageable))
            {
                if (damageable.Team == _team || !damageable.IsAlive)
                    return;
                if (_pierce)
                {
                    if (_pierced.Add(damageable))
                        damageable.TakeDamage(_damage, _rb.linearVelocity.normalized, _push, Shooter);
                    return;
                }
                if (_splashRadius <= 0f)
                    damageable.TakeDamage(_damage, _rb.linearVelocity.normalized, _push, Shooter);
            }

            if (_splashRadius > 0f)
                Splash(_rb.position);

            // 적이든 바닥·벽이든 닿으면 끝 (같은 스텝에 여러 번 불려도 한 번만)
            _spent = true;
            gameObject.SetActive(false);
        }

        private void Splash(Vector2 center)
        {
            Physics2D.OverlapCircle(center, _splashRadius, SolidOnly, _splashHits);
            foreach (Collider2D col in _splashHits)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive)
                    continue;

                Vector2 away = (Vector2)col.bounds.center - center;
                Vector2 direction = (away.normalized + Vector2.up * SplashLiftBias).normalized;
                damageable.TakeDamage(_damage, direction, _push, Shooter);
            }
        }
    }
}

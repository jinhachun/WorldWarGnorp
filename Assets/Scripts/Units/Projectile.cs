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
        // 0보다 크면 버프 투사체 — 회복처럼 적·던진 본인은 통과하고, 처음 닿은 아군의 공격력을 올린다
        private float _buffBonus;
        private float _buffSeconds;
        private Unit _owner;
        private bool _spent;
        private float _lifeTimer;
        private float _gravityScale;
        private readonly System.Collections.Generic.List<Collider2D> _splashHits = new System.Collections.Generic.List<Collider2D>();

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _gravityScale = _rb.gravityScale;
        }

        // 풀에서 다시 꺼낼 때마다 — 지난번 비행의 흔적(회복·버프 종류, 던진 칼의 무중력)을 지운다
        private void OnEnable()
        {
            _spent = false;
            _heal = 0f;
            _buffBonus = 0f;
            _owner = null;
            _splashRadius = 0f;
            _lifeTimer = Lifetime;
            _rb.gravityScale = _gravityScale;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        // 꼭짓점 높이를 고정(발사 지점·목표 중 높은 쪽 + arcHeight)하고 거기서 속도를 역산한다.
        // 수평 속도를 고정하면 가깝거나 같은 높이의 적에게는 거의 직선이 되므로 쓰지 않는다
        public void Launch(Team team, float damage, float push, Vector2 targetPoint, float arcHeight, float splashRadius)
        {
            _team = team;
            _damage = damage;
            _push = push;
            _splashRadius = splashRadius;

            Vector2 origin = _rb.position;
            float gravity = -Physics2D.gravity.y * _rb.gravityScale;
            float apex = Mathf.Max(origin.y, targetPoint.y) + arcHeight;
            float riseSpeed = Mathf.Sqrt(2f * gravity * (apex - origin.y));
            _rb.linearVelocity = new Vector2((targetPoint.x - origin.x) / FlightTime(targetPoint, arcHeight), riseSpeed);
            _lifeTimer = Lifetime;
        }

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
            Launch(team, 0f, 0f, targetPoint, arcHeight, 0f);
        }

        // 버프는 걷고 있는 아군에게도 던진다 — 떨어질 때 그 아군이 가 있을 자리를 노린다
        public void LaunchBuff(Unit owner, float bonus, float seconds, Vector2 targetPoint, Vector2 targetVelocity, float arcHeight)
        {
            _owner = owner;
            _buffBonus = bonus;
            _buffSeconds = seconds;
            targetPoint.x += targetVelocity.x * FlightTime(targetPoint, arcHeight);
            Launch(owner.Team, 0f, 0f, targetPoint, arcHeight, 0f);
        }

        // 중력 없이 곧게 날아간다(던진 칼)
        public void LaunchStraight(Team team, float damage, float push, Vector2 velocity)
        {
            _team = team;
            _damage = damage;
            _push = push;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = velocity;
            _lifeTimer = Lifetime;
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

            if (_heal > 0f || _buffBonus > 0f)
            {
                if (other.TryGetComponent(out IDamageable anyone) && anyone.Team != _team)
                    return;   // 적은 통과
                if (other.TryGetComponent(out Unit ally))
                {
                    if (ally == _owner || (_heal > 0f && !ally.IsDamaged))
                        return;   // 던진 본인·(회복이면) 멀쩡한 아군은 통과
                    if (_heal > 0f)
                        ally.Heal(_heal);
                    else
                        ally.Buff(_buffBonus, _buffSeconds);
                }
                _spent = true;
                gameObject.SetActive(false);
                return;
            }

            if (other.TryGetComponent(out IDamageable damageable))
            {
                if (damageable.Team == _team || !damageable.IsAlive)
                    return;
                if (_splashRadius <= 0f)
                    damageable.TakeDamage(_damage, _rb.linearVelocity.normalized, _push, null);
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
                damageable.TakeDamage(_damage, direction, _push, null);
            }
        }
    }
}

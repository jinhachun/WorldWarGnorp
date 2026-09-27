using UnityEngine;

namespace GnorpWar
{
    // 화살 — 중력으로 포물선을 그리며 날아가 처음 닿은 적에게 피해. 아군은 통과
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        private const float Lifetime = 4f;

        private Rigidbody2D _rb;
        private Team _team;
        private float _damage;
        private float _push;
        private bool _spent;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        // 꼭짓점 높이를 고정(발사 지점·목표 중 높은 쪽 + arcHeight)하고 거기서 속도를 역산한다.
        // 수평 속도를 고정하면 가깝거나 같은 높이의 적에게는 거의 직선이 되므로 쓰지 않는다
        public void Launch(Team team, float damage, float push, Vector2 targetPoint, float arcHeight)
        {
            _team = team;
            _damage = damage;
            _push = push;

            Vector2 origin = _rb.position;
            float gravity = -Physics2D.gravity.y * _rb.gravityScale;
            float apex = Mathf.Max(origin.y, targetPoint.y) + arcHeight;
            float riseSpeed = Mathf.Sqrt(2f * gravity * (apex - origin.y));
            float flightTime = riseSpeed / gravity + Mathf.Sqrt(2f * (apex - targetPoint.y) / gravity);
            _rb.linearVelocity = new Vector2((targetPoint.x - origin.x) / flightTime, riseSpeed);
            Destroy(gameObject, Lifetime);
        }

        private void FixedUpdate()
        {
            // 그림은 오른쪽을 향해 그려져 있다 — 날아가는 방향으로 머리를 돌린다
            Vector2 v = _rb.linearVelocity;
            if (v.sqrMagnitude > 0.0001f)
                _rb.MoveRotation(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent || other.isTrigger)
                return;

            if (other.TryGetComponent(out IDamageable damageable))
            {
                if (damageable.Team == _team || !damageable.IsAlive)
                    return;
                damageable.TakeDamage(_damage, _rb.linearVelocity.normalized, _push);
            }

            // 적이든 바닥·벽이든 닿으면 끝 (같은 스텝에 여러 번 불려도 한 번만)
            _spent = true;
            Destroy(gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Unit : MonoBehaviour, IDamageable
    {
        private const float DeathDestroyDelay = 3f;
        // 사망 시 뒤로 살짝 튀어 올랐다가 화면 아래로 떨어진다 (x는 후방 기준)
        private static readonly Vector2 DeathPop = new Vector2(1.5f, 4f);
        // 죽은 유닛은 모든 것 위에 그린다
        private const int DeathSortingOrder = 100;
        // 피격 연출 — 밀려나는 동안은 이동·공격을 쉰다(안 그러면 다음 스텝 속도 덮어쓰기에 지워진다)
        private const float KnockbackDuration = 0.15f;
        private const float SquashDuration = 0.3f;
        private const float SquashAmount = 0.3f;
        private const float FlashDuration = 0.08f;
        // 칼 찌르기 — 몸 중심 근처에서 타겟 방향으로 뻗었다가 돌아온다
        private const float ThrustDuration = 0.2f;
        private const float ThrustDistance = 0.3f;
        private static readonly Vector2 WeaponAnchor = new Vector2(0f, -0.1f);
        private const float WeaponReach = 0.45f;

        [SerializeField] private UnitDefinition _definition;
        [SerializeField] private Team _team;
        [Tooltip("그림(몸통·무기)만 담은 자식. 찌그러짐은 여기에만 건다 — 충돌 박스가 찌그러지면 산이 흔들린다")]
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _weapon;

        private Rigidbody2D _rb;
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private float _lastX;
        private float _advanceSpeed;
        private float _hp;
        private float _attackCooldown;
        private bool _onBase;
        private SpriteRenderer _bodyRenderer;
        private SpriteRenderer _weaponRenderer;
        private Color _bodyColor;
        private Vector3 _weaponRestPosition;
        private float _knockbackTimer;
        private float _squashTime = SquashDuration;
        private bool _squashVertical = true;
        private float _flashTimer;
        private float _thrustTime = ThrustDuration;
        private Vector2 _thrustDirection;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public UnitDefinition Definition => _definition;
        private float Forward => _team == Team.Ally ? 1f : -1f;
        // 같이 걸어가는 앞 유닛은 막은 게 아니다.
        // 속도값은 매 스텝 전진 속도로 덮어쓰므로 못 믿는다 — 실제로 움직인 거리로 판정
        private bool IsStopped => _advanceSpeed < _definition.MoveSpeed * 0.5f;
        private bool IsHeadFree
        {
            get
            {
                Vector2 above = (Vector2)transform.position + Vector2.up;
                return Physics2D.OverlapBox(above, new Vector2(0.8f, 0.8f), 0f) == null;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _lastX = _rb.position.x;
            _hp = _definition.MaxHp;
            _bodyRenderer = _visual.GetComponent<SpriteRenderer>();
            _weaponRenderer = _weapon.GetComponent<SpriteRenderer>();
            _bodyColor = _bodyRenderer.color;
            _weaponRestPosition = _weapon.localPosition;
            // 그림은 오른쪽(아군 전방)을 보고 그려져 있다 — 전방이 -x면 자식(무기)까지 통째로 뒤집는다
            if (Forward < 0f)
                transform.localScale = Vector3.Scale(transform.localScale, new Vector3(-1f, 1f, 1f));
        }

        private void FixedUpdate()
        {
            if (!IsAlive)
                return;

            _advanceSpeed = (_rb.position.x - _lastX) * Forward / Time.fixedDeltaTime;
            _lastX = _rb.position.x;
            _attackCooldown -= Time.fixedDeltaTime;

            // 접촉 법선은 상대 → 나 방향: 위를 향하면 발밑, 전방 반대를 향하면 앞에서 막힌 것
            bool grounded = false;
            bool canClimb = false;
            _onBase = false;
            int count = _rb.GetContacts(_contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = _contacts[i];
                if (contact.normal.y > 0.5f)
                {
                    grounded = true;
                    if (contact.collider.TryGetComponent(out Base _))
                        _onBase = true;
                }
                else if (contact.normal.x * Forward < -0.5f
                         && contact.collider.TryGetComponent(out Unit other)
                         && other._team == _team
                         && other.IsStopped
                         && other.IsHeadFree
                         && !other._onBase)
                    canClimb = true;
            }

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.fixedDeltaTime;
                return;
            }

            Vector2 velocity = _rb.linearVelocity;

            // 싸움이 점프·전진보다 우선
            if (FindTarget(out IDamageable target, out Vector2 targetPoint))
            {
                velocity.x = 0f;
                _rb.linearVelocity = velocity;
                if (_attackCooldown <= 0f)
                {
                    Vector2 toTarget = targetPoint - _rb.position;
                    Vector2 hitDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : new Vector2(Forward, 0f);
                    target.TakeDamage(_definition.AttackDamage, hitDirection);
                    StartThrust(hitDirection);
                    _attackCooldown = _definition.AttackInterval;
                }
                return;
            }

            // 기지 위는 한 층만 — 기지 위 유닛 머리로 걸어 올라가지도 않는다
            velocity.x = IsAheadOnBaseUnit() ? 0f : Forward * _definition.MoveSpeed;
            // 못 올라탈 땐 뛰지 않고 서 있어야 뒤 유닛의 발판이 된다 — 계속 뛰면 계단(산)이 안 생긴다
            if (grounded && canClimb)
                velocity.y = Mathf.Sqrt(2f * -Physics2D.gravity.y * _rb.gravityScale * _definition.JumpHeight);
            _rb.linearVelocity = velocity;
        }

        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (!IsAlive)
                return;

            _hp -= amount;
            if (!IsAlive)
            {
                Die();
                return;
            }

            // 맞은 방향으로 밀리고, 맞은 축으로 찌그러진다 (위에서 맞으면 납작, 옆에서 맞으면 홀쭉)
            _knockbackTimer = KnockbackDuration;
            _rb.linearVelocity = hitDirection * _definition.HitKnockback;
            _squashVertical = Mathf.Abs(hitDirection.y) > Mathf.Abs(hitDirection.x);
            _squashTime = 0f;
            _flashTimer = FlashDuration;
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            // 맞은 축으로 눌렸다가 출렁이며 원래 모양으로. 발바닥 높이는 고정
            float t = Mathf.Min(_squashTime / SquashDuration, 1f);
            float wobble = SquashAmount * (1f - t) * Mathf.Cos(t * Mathf.PI * 3f);
            float scaleX = _squashVertical ? 1f + wobble : 1f - wobble;
            float scaleY = _squashVertical ? 1f - wobble : 1f + wobble;
            _visual.localScale = new Vector3(scaleX, scaleY, 1f);
            _visual.localPosition = new Vector3(0f, (scaleY - 1f) * 0.5f, 0f);
            _squashTime += Time.deltaTime;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                _bodyRenderer.color = _flashTimer > 0f ? Color.white : _bodyColor;
            }

            UpdateThrust();
        }

        private void StartThrust(Vector2 worldDirection)
        {
            // 좌우 반전된 몸 기준으로 바꿔야 칼끝이 실제 타겟을 향한다
            Vector2 local = _visual.InverseTransformDirection(worldDirection);
            _thrustDirection = local.sqrMagnitude > 0.0001f ? local.normalized : Vector2.right;
            _thrustTime = 0f;
        }

        private void UpdateThrust()
        {
            if (_thrustTime >= ThrustDuration)
                return;

            _thrustTime += Time.deltaTime;
            if (_thrustTime >= ThrustDuration)
            {
                _weapon.localPosition = _weaponRestPosition;
                _weapon.localRotation = Quaternion.identity;
                return;
            }

            float lunge = Mathf.Sin(_thrustTime / ThrustDuration * Mathf.PI) * ThrustDistance;
            _weapon.localPosition = WeaponAnchor + _thrustDirection * (WeaponReach + lunge);
            float angle = Mathf.Atan2(_thrustDirection.y, _thrustDirection.x) * Mathf.Rad2Deg;
            _weapon.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private bool FindTarget(out IDamageable target, out Vector2 targetPoint)
        {
            Physics2D.OverlapCircle(_rb.position, _definition.AttackRange, ContactFilter2D.noFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (col.TryGetComponent(out IDamageable damageable) && damageable.Team != _team && damageable.IsAlive)
                {
                    target = damageable;
                    targetPoint = col.ClosestPoint(_rb.position);
                    return true;
                }
            }
            target = null;
            targetPoint = default;
            return false;
        }

        private bool IsAheadOnBaseUnit()
        {
            // 한 칸 앞, 발밑 높이에 기지 위 유닛이 있으면 그 머리로 걸어 들어가게 된다
            Vector2 aheadBelow = _rb.position + new Vector2(Forward, -1f);
            Collider2D col = Physics2D.OverlapBox(aheadBelow, new Vector2(0.8f, 0.8f), 0f);
            return col != null && col.TryGetComponent(out Unit unit) && unit._onBase;
        }

        private void Die()
        {
            // 물리 제거 — 콜라이더가 꺼지면 위에 서 있던 유닛들이 빈자리로 내려앉는다
            foreach (Collider2D col in GetComponents<Collider2D>())
                col.enabled = false;
            _rb.linearVelocity = new Vector2(-Forward * DeathPop.x, DeathPop.y);

            // 마리오처럼 뒤집힌 채 맨 앞에 그려지며 퇴장
            _bodyRenderer.color = _bodyColor;
            _bodyRenderer.sortingOrder = DeathSortingOrder;
            _weaponRenderer.sortingOrder = DeathSortingOrder + 1;
            _weapon.localPosition = _weaponRestPosition;
            _weapon.localRotation = Quaternion.identity;
            _visual.localPosition = Vector3.zero;
            _visual.localScale = new Vector3(1f, -1f, 1f);

            Destroy(gameObject, DeathDestroyDelay);
        }
    }
}

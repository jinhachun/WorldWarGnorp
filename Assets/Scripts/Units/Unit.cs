using System.Collections;
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
        // 큰 넉백 — 뒤로 튕겨 오르며 날아가는 동안은 경직
        private const float BigKnockbackDuration = 0.6f;
        private const float BigKnockbackHopRatio = 0.6f;
        // 타격감 연출 세기 (FxDirector)
        private static readonly Color SparkColor = new Color(1f, 0.9f, 0.25f);
        private const float SparkSurfaceOffset = 0.4f;
        private const float DeathShake = 0.08f;
        private const float BigKnockbackShake = 0.35f;
        private const float BigKnockbackHitStop = 0.06f;
        // 칼 찌르기 — 몸 중심 근처에서 타겟 방향으로 뻗었다가 돌아온다
        private const float ThrustDuration = 0.2f;
        private const float ThrustDistance = 0.3f;
        private static readonly Vector2 WeaponAnchor = new Vector2(0f, -0.1f);
        private const float WeaponReach = 0.45f;
        // 칸 검사는 몸(고체)만 본다 — 날아가는 화살(트리거) 때문에 칸이 막힌 걸로 보이면 안 된다
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };
        private static readonly List<Collider2D> CellProbe = new List<Collider2D>();
        // 층 정렬 — 아래 유닛이 뒤쪽으로 호를 그리며 타고 올라가고, 위 유닛은 앞쪽으로 미끄러져 내려온다
        private const float SwapDuration = 0.35f;
        private const float SwapClimbArc = 0.7f;
        private const float SwapSlideArc = 0.3f;
        private const float SwapMaxOffsetX = 0.5f;

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
        private bool _swapping;
        private bool _bigKnockbackUsed;
        // 돌격 충전 — 적 없이 실제로 달린 시간. 막혀 서 있으면 0으로
        private float _runTime;

        // 유닛이 죽는 순간 (처치 보상 등). static이라 구독자는 OnDisable에서 반드시 해제할 것
        public static event System.Action<Unit> Died;

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
                return Physics2D.OverlapBox(above, new Vector2(0.8f, 0.8f), 0f, SolidOnly, CellProbe) == 0;
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
            if (!IsAlive || _swapping)
                return;

            _advanceSpeed = (_rb.position.x - _lastX) * Forward / Time.fixedDeltaTime;
            _lastX = _rb.position.x;
            _attackCooldown -= Time.fixedDeltaTime;

            // 접촉 법선은 상대 → 나 방향: 위를 향하면 발밑, 전방 반대를 향하면 앞에서 막힌 것
            bool grounded = false;
            bool canClimb = false;
            Unit allyBelow = null;
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
                    else if (contact.collider.TryGetComponent(out Unit below) && below._team == _team)
                        allyBelow = below;
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

            // 층 정렬: 내가 바로 아래 아군보다 아래층 역할이면(예: 원딜 위의 탱커) 자리를 바꾼다
            if (allyBelow != null && CanSwapDownWith(allyBelow))
            {
                StartCoroutine(SwapDownWith(allyBelow));
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
                    if (_definition.AttackType == AttackType.Ranged)
                    {
                        Projectile arrow = Instantiate(_definition.Projectile, _weapon.position, Quaternion.identity);
                        arrow.Launch(_team, _definition.AttackDamage, _definition.PushPower, targetPoint, _definition.ProjectileArcHeight, _definition.ProjectileSplashRadius);
                    }
                    else
                    {
                        // 달려와서 치는 첫 타격은 돌격 — 피해·밀치기에 배율
                        bool charge = _runTime >= _definition.ChargeReadySeconds;
                        float damage = _definition.AttackDamage * (charge ? _definition.ChargeDamageMultiplier : 1f);
                        float push = _definition.PushPower * (charge ? _definition.ChargePushMultiplier : 1f);
                        target.TakeDamage(damage, hitDirection, push);
                    }
                    _runTime = 0f;
                    StartThrust(hitDirection);
                    _attackCooldown = _definition.AttackInterval;
                }
                return;
            }

            _runTime = IsStopped ? 0f : _runTime + Time.fixedDeltaTime;

            // 기지 위는 한 층만 — 기지 위 유닛 머리로 걸어 올라가지도 않는다
            velocity.x = IsAheadOnBaseUnit() ? 0f : Forward * _definition.MoveSpeed;
            // 못 올라탈 땐 뛰지 않고 서 있어야 뒤 유닛의 발판이 된다 — 계속 뛰면 계단(산)이 안 생긴다
            if (grounded && canClimb)
                velocity.y = Mathf.Sqrt(2f * -Physics2D.gravity.y * _rb.gravityScale * _definition.JumpHeight);
            _rb.linearVelocity = velocity;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push)
        {
            if (!IsAlive)
                return;

            _hp -= amount;
            // 맞은 면(공격이 들어온 쪽)에서 공격 방향으로 파편이 튄다
            if (FxDirector.Instance != null)
                FxDirector.Instance.HitSpark(_rb.position - hitDirection * SparkSurfaceOffset, hitDirection, SparkColor);
            if (!IsAlive)
            {
                Die();
                return;
            }

            _squashTime = 0f;
            _flashTimer = FlashDuration;

            // 냥코식 큰 넉백 — 생명당 한 번, 맞은 방향과 상관없이 후방으로 튕겨 오른다
            if (!_bigKnockbackUsed && _hp <= _definition.MaxHp * _definition.BigKnockbackAt)
            {
                _bigKnockbackUsed = true;
                _knockbackTimer = BigKnockbackDuration;
                float speed = _definition.BigKnockbackSpeed;
                _rb.linearVelocity = new Vector2(-Forward * speed, speed * BigKnockbackHopRatio);
                _squashVertical = false;
                if (FxDirector.Instance != null)
                {
                    FxDirector.Instance.HitStop(BigKnockbackHitStop);
                    FxDirector.Instance.Shake(BigKnockbackShake);
                }
                return;
            }

            // 맞은 방향으로 밀리고, 맞은 축으로 찌그러진다 (위에서 맞으면 납작, 옆에서 맞으면 홀쭉).
            // 세게 맞을수록 오래 조종 불능 — 짧으면 다음 스텝에 걷기가 속도를 덮어써 날아가다 끊긴다
            _knockbackTimer = KnockbackDuration * Mathf.Max(1f, push);
            _rb.linearVelocity = hitDirection * (_definition.HitKnockback * push);
            _squashVertical = Mathf.Abs(hitDirection.y) > Mathf.Abs(hitDirection.x);
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
            // 사거리 안에서 가장 가까운 적 — 사거리가 긴 원거리딜이 먼 적부터 쏘지 않게
            Physics2D.OverlapCircle(_rb.position, _definition.AttackRange, SolidOnly, _overlaps);
            target = null;
            targetPoint = default;
            float best = float.MaxValue;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive)
                    continue;

                Vector2 point = col.ClosestPoint(_rb.position);
                float distance = (point - _rb.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = damageable;
                    targetPoint = point;
                }
            }
            return target != null;
        }

        private bool IsAheadOnBaseUnit()
        {
            // 한 칸 앞, 발밑 높이에 기지 위 유닛이 있으면 그 머리로 걸어 들어가게 된다
            Vector2 aheadBelow = _rb.position + new Vector2(Forward, -1f);
            Physics2D.OverlapBox(aheadBelow, new Vector2(0.8f, 0.8f), 0f, SolidOnly, CellProbe);
            foreach (Collider2D col in CellProbe)
            {
                if (col.TryGetComponent(out Unit unit) && unit._onBase)
                    return true;
            }
            return false;
        }

        private bool CanSwapDownWith(Unit below)
        {
            return _definition.StackRank < below._definition.StackRank
                   && below.IsAlive && !below._swapping && below._knockbackTimer <= 0f
                   && Mathf.Abs(below._rb.position.x - _rb.position.x) <= SwapMaxOffsetX;
        }

        // 교환 동안 두 칸 자리에 보이지 않는 받침대를 세워 위의 산을 받친다(ARCHITECTURE 「위치 교환」)
        private IEnumerator SwapDownWith(Unit below)
        {
            Vector2 upperStart = _rb.position;
            Vector2 lowerStart = below._rb.position;

            var support = new GameObject("SwapSupport");
            support.transform.position = (upperStart + lowerStart) * 0.5f;
            support.AddComponent<BoxCollider2D>().size = new Vector2(1f, upperStart.y - lowerStart.y + 1f);

            BeginSwap();
            below.BeginSwap();
            for (float t = 0f; t < SwapDuration; t += Time.fixedDeltaTime)
            {
                if (!IsAlive || !below.IsAlive)
                    break;

                float k = t / SwapDuration;
                float arc = Mathf.Sin(k * Mathf.PI);
                below._rb.MovePosition(Vector2.Lerp(lowerStart, upperStart, k) + new Vector2(-Forward * SwapClimbArc * arc, 0f));
                _rb.MovePosition(Vector2.Lerp(upperStart, lowerStart, k) + new Vector2(Forward * SwapSlideArc * arc, 0f));
                yield return new WaitForFixedUpdate();
            }

            Destroy(support);
            if (IsAlive)
                EndSwap(lowerStart);
            if (below.IsAlive)
                below.EndSwap(upperStart);
        }

        private void BeginSwap()
        {
            _swapping = true;
            foreach (Collider2D col in GetComponents<Collider2D>())
                col.enabled = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }

        private void EndSwap(Vector2 position)
        {
            _rb.position = position;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
            foreach (Collider2D col in GetComponents<Collider2D>())
                col.enabled = true;
            _lastX = position.x;
            _swapping = false;
        }

        private void Die()
        {
            // 교환 중이었다면 운동학 상태 — 중력을 받게 되돌려야 떨어진다
            _rb.bodyType = RigidbodyType2D.Dynamic;
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

            if (FxDirector.Instance != null)
            {
                FxDirector.Instance.DeathPuff(_rb.position);
                FxDirector.Instance.Shake(DeathShake);
            }

            Destroy(gameObject, DeathDestroyDelay);
            Died?.Invoke(this);
        }
    }
}

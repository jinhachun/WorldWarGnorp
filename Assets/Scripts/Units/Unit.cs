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
        // 몸통 그림들(무기 제외) — 보통 1장, 기사는 말 + 탄 gnorp 2장
        private SpriteRenderer[] _bodyRenderers;
        private Color[] _bodyColors;
        private SpriteRenderer _weaponRenderer;
        // 키의 절반 — 충돌 박스 높이에서 읽는다(보통 0.5, 기사 0.75). 칸 검사·발 고정이 이 값을 쓴다
        private float _halfHeight;
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
        public bool IsDamaged => IsAlive && _hp < _definition.MaxHp;
        public UnitDefinition Definition => _definition;

        // 소환 — 소환 지점 칸이 (적이든 아군이든) 차 있으면 한 층씩 올라가 비어 있는 가장 낮은 층에 만든다.
        // 그냥 겹쳐 만들면 1층에 끼인 채 쌓인다
        private const int SpawnMaxFloors = 30;
        private static readonly Vector2 SpawnCellProbe = new Vector2(0.9f, 0.9f);

        public static Unit Spawn(Unit prefab, Vector2 groundPoint)
        {
            // groundPoint는 1층 칸의 중심. 키가 큰 유닛은 발을 같은 높이에 맞추도록 중심을 올리고, 키만큼 빈 공간을 찾는다
            Vector2 box = prefab.GetComponent<BoxCollider2D>().size;
            float height = box.y;
            // 폭이 큰 유닛(공룡)도 몸 전체가 들어갈 공간을 찾는다
            Vector2 probe = new Vector2(box.x - (1f - SpawnCellProbe.x), height - (1f - SpawnCellProbe.y));
            Vector2 baseCenter = groundPoint + Vector2.up * ((height - 1f) * 0.5f);
            Vector2 position = baseCenter;
            for (int floor = 0; floor < SpawnMaxFloors; floor++)
            {
                position = baseCenter + Vector2.up * floor;
                if (Physics2D.OverlapBox(position, probe, 0f, SolidOnly, CellProbe) == 0)
                    break;
            }
            return Instantiate(prefab, position, Quaternion.identity);
        }
        private float Forward => _team == Team.Ally ? 1f : -1f;
        // 같이 걸어가는 앞 유닛은 막은 게 아니다.
        // 속도값은 매 스텝 전진 속도로 덮어쓰므로 못 믿는다 — 실제로 움직인 거리로 판정
        private bool IsStopped => _advanceSpeed < _definition.MoveSpeed * 0.5f;
        private float Feet => _rb.position.y - _halfHeight;
        private float Top => _rb.position.y + _halfHeight;
        private bool IsHeadFree
        {
            get
            {
                // 머리 바로 위 한 칸
                Vector2 above = new Vector2(_rb.position.x, Top + 0.5f);
                return Physics2D.OverlapBox(above, new Vector2(0.8f, 0.8f), 0f, SolidOnly, CellProbe) == 0;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _lastX = _rb.position.x;
            _hp = _definition.MaxHp;
            _weaponRenderer = _weapon.GetComponent<SpriteRenderer>();
            var bodies = new List<SpriteRenderer>();
            foreach (SpriteRenderer sr in _visual.GetComponentsInChildren<SpriteRenderer>())
                if (sr != _weaponRenderer)
                    bodies.Add(sr);
            _bodyRenderers = bodies.ToArray();
            _bodyColors = new Color[_bodyRenderers.Length];
            for (int i = 0; i < _bodyRenderers.Length; i++)
                _bodyColors[i] = _bodyRenderers[i].color;
            _halfHeight = GetComponent<BoxCollider2D>().size.y * 0.5f;
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
            bool blockedByEnemy = false;
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
                         && !other._onBase
                         && other.Top - Feet <= _definition.JumpHeight)   // 닿지도 않는 높이(키 큰 기사)에 계속 뛰지 않게
                    canClimb = true;
                if (contact.normal.x * Forward < -0.5f
                    && contact.collider.TryGetComponent(out Unit blocker) && blocker._team != _team)
                    blockedByEnemy = true;
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

            // 프리스트 — 적은 공격하지 않는다. 다친 아군을 회복하고, 적이 사거리에 들면 뒤에 멈춰 선다(앞으로 걸어가 죽지 않게)
            if (_definition.AttackType == AttackType.Heal)
            {
                bool enemyNear = FindTarget(out _, out _);
                Unit patient = FindHealTarget(null);
                if (patient != null && _attackCooldown <= 0f)
                {
                    Vector2 toPatient = patient._rb.position - _rb.position;
                    ThrowHeal(patient);
                    // Priest 업그레이드: 두 번째로 많이 다친 아군에게도
                    if (UpgradeState.IsActive(this, UpgradeKind.PriestDoubleHeal))
                    {
                        Unit second = FindHealTarget(patient);
                        if (second != null)
                            ThrowHeal(second);
                    }
                    StartThrust(toPatient.sqrMagnitude > 0.0001f ? toPatient.normalized : new Vector2(Forward, 0f));
                    _attackCooldown = _definition.AttackInterval;
                }
                if (enemyNear || patient != null)
                {
                    velocity.x = 0f;
                    _rb.linearVelocity = velocity;
                    return;
                }
            }
            // Knight 업그레이드: 앞쪽에 원거리 적이 있으면 원거리가 아닌 적은 상대하지 않고 뛰어넘는다
            bool hunting = UpgradeState.IsActive(this, UpgradeKind.KnightVaultToArchers) && HasRangedEnemyAhead();

            // 싸움이 점프·전진보다 우선 (프리스트는 위에서 처리 — 회복할 대상도 적도 없으면 여기로 내려와 전진)
            if (_definition.AttackType != AttackType.Heal && FindTarget(out IDamageable target, out Vector2 targetPoint, null, hunting))
            {
                velocity.x = 0f;
                _rb.linearVelocity = velocity;
                if (_attackCooldown <= 0f)
                {
                    Vector2 toTarget = targetPoint - _rb.position;
                    Vector2 hitDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : new Vector2(Forward, 0f);
                    if (_definition.AttackType == AttackType.Flame)
                    {
                        BreatheFire();
                    }
                    else if (_definition.AttackType == AttackType.Ranged)
                    {
                        FireProjectile(targetPoint);
                        // Bow 업그레이드: 서로 다른 적에게 한 발 더 (적이 하나뿐이면 한 발)
                        if (UpgradeState.IsActive(this, UpgradeKind.BowDoubleShot)
                            && FindTarget(out _, out Vector2 secondPoint, target, false))
                            FireProjectile(secondPoint);
                    }
                    else
                    {
                        // 달려와서 치는 첫 타격은 돌격 — 피해·밀치기에 배율
                        bool charge = _runTime >= _definition.ChargeReadySeconds;
                        float damage = _definition.AttackDamage * (charge ? _definition.ChargeDamageMultiplier : 1f);
                        float push = _definition.PushPower * (charge ? _definition.ChargePushMultiplier : 1f);
                        target.TakeDamage(damage, hitDirection, push, this);
                    }
                    _runTime = 0f;
                    if (_definition.AttackType != AttackType.Flame)   // 화염은 입(무기 자리)이 움직이면 안 된다
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
            // Knight 업그레이드: 원거리가 아닌 적에게 막히면 높이 뛰어 넘는다(적 머리 위로 착지해 계속 전진)
            else if (grounded && hunting && blockedByEnemy)
                velocity.y = Mathf.Sqrt(2f * -Physics2D.gravity.y * _rb.gravityScale * _definition.UpgradeValue);
            _rb.linearVelocity = velocity;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, Unit attacker)
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

            // Shield 업그레이드: 돌격형(기사)에게 맞으면 방패는 버티고 공격한 쪽이 튕겨난다
            if (attacker != null && attacker.IsAlive && attacker._definition.ChargeDamageMultiplier > 1f
                && UpgradeState.IsActive(this, UpgradeKind.ShieldReflectCharge))
            {
                attacker.TakeDamage(0f, -hitDirection, push, null);
                return;
            }

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
            _visual.localPosition = new Vector3(0f, (scaleY - 1f) * _halfHeight, 0f);
            _squashTime += Time.deltaTime;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                for (int i = 0; i < _bodyRenderers.Length; i++)
                    _bodyRenderers[i].color = _flashTimer > 0f ? Color.white : _bodyColors[i];
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
            // 찌르기 기준점 = 무기를 든 손(쉬는 위치에서 팔 길이만큼 몸 쪽) — 기사는 말 위, 보통은 몸 가운데
            Vector2 anchor = (Vector2)_weaponRestPosition - new Vector2(WeaponReach, 0f);
            _weapon.localPosition = anchor + _thrustDirection * (WeaponReach + lunge);
            float angle = Mathf.Atan2(_thrustDirection.y, _thrustDirection.x) * Mathf.Rad2Deg;
            _weapon.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // exclude: 이미 고른 대상(두 번째 대상을 찾을 때) · rangedOnly: 원거리 유닛만(기사 업그레이드)
        private bool FindTarget(out IDamageable target, out Vector2 targetPoint, IDamageable exclude = null, bool rangedOnly = false)
        {
            // 사거리 안에서 가장 가까운 적 — 사거리가 긴 원거리딜이 먼 적부터 쏘지 않게
            Physics2D.OverlapCircle(_rb.position, _definition.AttackRange, SolidOnly, _overlaps);
            target = null;
            targetPoint = default;
            float best = float.MaxValue;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive || damageable == exclude)
                    continue;
                if (rangedOnly && !(damageable is Unit unit && unit._definition.AttackType == AttackType.Ranged))
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

        // 원거리 무기(화살·돌) 발사
        private void FireProjectile(Vector2 targetPoint)
        {
            Projectile shot = Instantiate(_definition.Projectile, _weapon.position, Quaternion.identity);
            shot.Launch(_team, _definition.AttackDamage, _definition.PushPower, targetPoint, _definition.ProjectileArcHeight, _definition.ProjectileSplashRadius);
        }

        private void ThrowHeal(Unit patient)
        {
            Projectile orb = Instantiate(_definition.Projectile, _weapon.position, Quaternion.identity);
            orb.LaunchHeal(this, _definition.HealAmount, patient._rb.position, _definition.ProjectileArcHeight);
        }

        // 기사 업그레이드 — 앞쪽 일정 거리 안에 적 원거리 유닛이 있나
        private const float HuntSearchRange = 15f;

        private bool HasRangedEnemyAhead()
        {
            Physics2D.OverlapCircle(_rb.position, HuntSearchRange, SolidOnly, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (col.TryGetComponent(out Unit enemy) && enemy._team != _team && enemy.IsAlive
                    && enemy._definition.AttackType == AttackType.Ranged
                    && (enemy._rb.position.x - _rb.position.x) * Forward > 0f)
                    return true;
            }
            return false;
        }

        // 사거리 안에서 체력 비율이 가장 낮은 다친 아군(자기·exclude 제외)
        private Unit FindHealTarget(Unit exclude)
        {
            Physics2D.OverlapCircle(_rb.position, _definition.AttackRange, SolidOnly, _overlaps);
            Unit best = null;
            float bestRatio = 1f;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit ally) || ally == this || ally == exclude || ally._team != _team || !ally.IsAlive || ally._swapping)
                    continue;

                float ratio = ally._hp / ally._definition.MaxHp;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = ally;
                }
            }
            return best;
        }

        // 화염방사 — 입(무기 자리)에서 앞으로 뻗은 띠 안의 적 전부에게 피해
        private void BreatheFire()
        {
            Vector2 mouth = _weapon.position;
            float length = _definition.AttackRange;
            Vector2 center = mouth + new Vector2(Forward * length * 0.5f, 0f);
            Physics2D.OverlapBox(center, new Vector2(length, _definition.FlameThickness), 0f, SolidOnly, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (col.TryGetComponent(out IDamageable damageable) && damageable.Team != _team && damageable.IsAlive)
                    damageable.TakeDamage(_definition.AttackDamage, new Vector2(Forward, 0f), _definition.PushPower, null);
            }
            if (FxDirector.Instance != null)
                FxDirector.Instance.Flame(mouth, Forward, length);
        }

        // 피해 없이 밀어낸다(보스 등장 충격파 등). 그동안 조종 불능
        public void Shove(Vector2 velocity, float stunSeconds)
        {
            if (!IsAlive || _swapping)
                return;
            _knockbackTimer = stunSeconds;
            _rb.linearVelocity = velocity;
        }

        public static void ShockwaveAll(Team team, Vector2 velocity, float stunSeconds)
        {
            foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit._team == team)
                    unit.Shove(velocity, stunSeconds);
        }

        public void Heal(float amount)
        {
            if (!IsAlive)
                return;
            _hp = Mathf.Min(_hp + amount, _definition.MaxHp);
        }

        private bool IsAheadOnBaseUnit()
        {
            // 한 칸 앞, 발밑 높이에 기지 위 유닛이 있으면 그 머리로 걸어 들어가게 된다
            Vector2 aheadBelow = new Vector2(_rb.position.x + Forward, Feet - 0.5f);
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
            // 키가 다르면 자리를 맞바꿀 때 발 높이가 어긋난다 — 같은 키끼리만
            return _definition.StackRank < below._definition.StackRank
                   && Mathf.Approximately(_halfHeight, below._halfHeight)
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
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                _bodyRenderers[i].color = _bodyColors[i];
                _bodyRenderers[i].sortingOrder = DeathSortingOrder;
            }
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

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
        // 등반 — 앞 아군이 내 속도의 이 비율보다 느리면 넘는다(더 빠르면 공중에 있는 동안 다시 걸어가 버려 헛점프) · 앞 기둥은 이만큼까지만 위로 훑는다
        private const float ClimbSlowerThan = 0.5f;
        private const int ClimbMaxColumn = 4;
        // 기둥 꼭대기가 점프 높이에 딱 걸리면 꼭짓점에서 못 넘어가 떨어진다 — 이만큼 여유가 있어야 노린다
        private const float ClimbReachMargin = 0.2f;
        // 넘쳐흐르기 — 근접은 머리가 내 발 높이(+이만큼) 아래인 적을 치지 않고 밟고 넘어간다
        private const float StandOnMargin = 0.1f;

        [SerializeField] private UnitDefinition _definition;
        [SerializeField] private Team _team;
        [Tooltip("그림(몸통·무기)만 담은 자식. 찌그러짐은 여기에만 건다 — 충돌 박스가 찌그러지면 산이 흔들린다")]
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _weapon;

        private Rigidbody2D _rb;
        private BoxCollider2D _body;
        // 검색 필터 — 적 쪽(내 진영 유닛 레이어 제외: 적 유닛·기지·탑)과 아군 쪽(내 진영 유닛 레이어만)
        private ContactFilter2D _enemyFilter;
        private ContactFilter2D _allyFilter;
        private int[] _bodySortingOrders;
        private int _weaponSortingOrder;
        // 죽은 뒤 화면 밖으로 떨어지는 동안 남은 시간 — 0이 되면 풀로 돌아간다
        private float _despawnTimer;
        // 적 찾기 캐시(FindTargetCached) — 다음 새 검색까지 남은 시간과 그때 찾은 적
        private float _targetScanTimer;
        private IDamageable _cachedTarget;
        private Collider2D _cachedTargetCollider;
        private Collider2D _foundCollider;
        // 프리스트의 회복 대상 캐시 — 같은 간격으로만 새로 찾는다
        private float _supportScanTimer;
        private Unit _cachedPatient;
        // 기지 위 유닛 검사 캐시(걷기) — 같은 간격으로만 새로 본다
        private float _walkScanTimer;
        private bool _aheadOnBaseUnit;
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
        private bool _bigKnockbackUsed;
        // 돌격 충전 — 적 없이 실제로 달린 시간. 막혀 서 있으면 0으로
        private float _runTime;
        // 밟기 효과: 지금 밟고 서 있는 적 — 새로 내려앉은 적에게만 피해
        private Unit _stompedOn;
        // 점프 착지 충격(코끼리) — 다음 점프까지 남은 시간 · 뛰어오른 뒤 착지 전인가 · 그 사이 발이 땅에서 떨어진 적이 있나
        private float _slamTimer;
        private bool _slamJumping;
        private bool _slamLeftGround;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public bool IsDamaged => IsAlive && _hp < _definition.MaxHp;
        public UnitDefinition Definition => _definition;

        // 소환 — 소환 지점 칸이 (적이든 아군이든) 차 있으면 한 층씩 올라가 비어 있는 가장 낮은 층에 만든다.
        // 그냥 겹쳐 만들면 1층에 끼인 채 쌓인다
        private const int SpawnMaxFloors = 30;
        private static readonly Vector2 SpawnCellProbe = new Vector2(0.9f, 0.9f);

        // 한꺼번에 여러 마리가 나올 때 — 소환 지점부터 앞쪽으로 이만큼의 칸을 먼저 채우고, 다 차면 위층으로
        private const int SpawnSpreadSlots = 5;
        // 주변 검색(적 찾기·사냥·칼 던질 띠) 간격 — 매 물리 스텝(0.02초)마다 하면 유닛이 수백일 때 프레임이 무너진다
        private const float TargetScanInterval = 0.1f;
        // 프로파일러 구간 — Dev/PerfProbe가 이 이름으로 시간을 읽는다
        private static readonly Unity.Profiling.ProfilerMarker ContactsMarker = new Unity.Profiling.ProfilerMarker("Unit.Contacts");
        private static readonly Unity.Profiling.ProfilerMarker ScanMarker = new Unity.Profiling.ProfilerMarker("Unit.Scan");
        private static readonly Unity.Profiling.ProfilerMarker SupportMarker = new Unity.Profiling.ProfilerMarker("Unit.Support");
        private static readonly Unity.Profiling.ProfilerMarker WalkMarker = new Unity.Profiling.ProfilerMarker("Unit.Walk");
        // 땅에 박혀 나오지 않게 발을 땅보다 살짝 위에
        private const float SpawnGroundGap = 0.02f;

        public static Unit Spawn(Unit prefab, Vector2 groundPoint)
        {
            // groundPoint는 1층 칸의 중심. 키가 큰 유닛은 발을 같은 높이에 맞추도록 중심을 올리고, 키만큼 빈 공간을 찾는다
            Vector2 box = prefab.GetComponent<BoxCollider2D>().size;
            float height = box.y;
            // 폭이 큰 유닛(공룡)도 몸 전체가 들어갈 공간을 찾는다
            Vector2 probe = new Vector2(box.x - (1f - SpawnCellProbe.x), height - (1f - SpawnCellProbe.y));
            float forward = prefab._team == Team.Ally ? 1f : -1f;
            float slotWidth = Mathf.Max(1f, box.x);
            // 칸마다 땅 높이가 다를 수 있다 — groundPoint는 가로 위치만 쓰고, 각 칸의 땅 위에 발이 닿게 세운다
            float lift = height * 0.5f + SpawnGroundGap;
            // 층 정렬을 지키는 칸을 먼저 찾고(위층은 나보다 아래·같은 역할 위에만), 없으면 그냥 첫 빈칸
            Vector2 position = groundPoint;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int floor = 0; floor < SpawnMaxFloors; floor++)
                {
                    for (int slot = 0; slot < SpawnSpreadSlots; slot++)
                    {
                        float x = groundPoint.x + forward * slot * slotWidth;
                        position = new Vector2(x, Ground.Instance.HeightAt(x) + lift + floor);
                        if (IsSpawnCellFree(position, probe, prefab._team)
                            && (pass == 1 || floor == 0 || CanStackOn(position, height, prefab)))
                            return Pooled.Get(prefab, position, Quaternion.identity);
                    }
                }
            }
            return Pooled.Get(prefab, position, Quaternion.identity);
        }

        // 층 정렬: 소환 칸 바로 아래가 나보다 아래·같은 역할의 아군으로 차 있어야 그 위에 낸다.
        // 비어 있으면 떨어져서 결국 더 아래 아무 유닛 위에나 앉으므로 쓰지 않는다
        private static bool CanStackOn(Vector2 position, float height, Unit prefab)
        {
            Vector2 below = position + Vector2.down * (height * 0.5f + 0.5f);
            Physics2D.OverlapBox(below, SpawnCellProbe, 0f, SolidOnly, CellProbe);
            bool supported = false;
            foreach (Collider2D col in CellProbe)
            {
                if (!col.TryGetComponent(out Unit unit) || unit._team != prefab._team)
                    continue;
                if (prefab._definition.StackRank < unit._definition.StackRank)
                    return false;
                supported = true;
            }
            return supported;
        }

        // 같은 진영 기지는 몸이 통과하므로 빈칸 검사에서 뺀다 — 안 빼면 성문 안 소환 칸이 늘 막힌 걸로 보인다.
        // 땅도 뺀다 — 굽은 땅에서는 1층 칸 아래쪽이 경사에 걸려 늘 막힌 걸로 보인다
        private static bool IsSpawnCellFree(Vector2 position, Vector2 probe, Team team)
        {
            Physics2D.OverlapBox(position, probe, 0f, SolidOnly, CellProbe);
            foreach (Collider2D col in CellProbe)
            {
                if (col.TryGetComponent(out Ground _))
                    continue;
                if (col.TryGetComponent(out Base b) && b.Team == team)
                    continue;
                return false;
            }
            return true;
        }
        public float Forward => _team == Team.Ally ? 1f : -1f;
        // 같이 걸어가는 앞 유닛은 막은 게 아니다.
        // 속도값은 매 스텝 전진 속도로 덮어쓰므로 못 믿는다 — 실제로 움직인 거리로 판정
        private bool IsStopped => _advanceSpeed < MoveSpeed * 0.5f;
        private float MoveSpeed
        {
            get
            {
                float scale = 1f;
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesTo(_definition))
                        scale *= e.Effect.MoveSpeedScale(e.Stacks);
                return _definition.MoveSpeed * scale;
            }
        }
        // 사거리·포물선 높이 배율
        private float LongRangeScale
        {
            get
            {
                float scale = 1f;
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesTo(_definition))
                        scale *= e.Effect.RangeScale(e.Stacks);
                return scale;
            }
        }
        private float AttackRange => _definition.AttackRange * LongRangeScale;
        // 전투 공격력 가속
        public float DamageScale => BattleManager.DamageMultiplier;

        // 건물이 이 유닛을 생산했다 — 생산 훅(호위 등)
        public void NotifyProduced()
        {
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesTo(_definition))
                    e.Effect.OnProduced(this, e.Stacks);
        }
        private float Feet => _rb.position.y - _halfHeight;
        private float Top => _rb.position.y + _halfHeight;
        // 점프 높이(발이 올라가는 높이) = 내 키 + 1칸 — 모든 유닛 공통
        private float JumpHeight => _halfHeight * 2f + 1f;

        // 머리 바로 위 한 칸 — 거기 선 유닛(없으면 null), 칸이 완전히 비었나
        private Unit OnHead(out bool free)
        {
            Vector2 above = new Vector2(_rb.position.x, Top + 0.5f);
            free = Physics2D.OverlapBox(above, new Vector2(0.8f, 0.8f), 0f, SolidOnly, CellProbe) == 0;
            foreach (Collider2D col in CellProbe)
                if (col.TryGetComponent(out Unit unit) && unit != this)
                    return unit;
            return null;
        }

        // 앞 아군 위로 쌓인 기둥의 꼭대기 — 점프로 닿고, 그 위가 비었고, 층 정렬상 그 위에 서도 되면 그 유닛. 아니면 null
        private Unit ClimbTarget(Unit front)
        {
            Unit top = front;
            for (int i = 0; i < ClimbMaxColumn; i++)
            {
                if (top.Top - Feet > JumpHeight - ClimbReachMargin)
                    return null;   // 닿지도 않는 높이에 계속 뛰지 않게
                Unit above = top.OnHead(out bool free);
                if (free)
                    // 층 정렬: 나보다 위층 역할(예: 탱커 앞의 원딜) 위에는 오르지 않고 뒤에서 기다린다
                    return BelongsBelow(top) ? null : top;
                if (above == null || above._team != _team || !above.IsAlive)
                    return null;   // 유닛 아닌 것(기지 등)·적이 막고 있다
                top = above;
            }
            return null;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _body = GetComponent<BoxCollider2D>();
            _weaponRenderer = _weapon.GetComponent<SpriteRenderer>();
            _weaponSortingOrder = _weaponRenderer.sortingOrder;
            var bodies = new List<SpriteRenderer>();
            foreach (SpriteRenderer sr in _visual.GetComponentsInChildren<SpriteRenderer>())
                if (sr != _weaponRenderer)
                    bodies.Add(sr);
            _bodyRenderers = bodies.ToArray();
            _bodyColors = new Color[_bodyRenderers.Length];
            _bodySortingOrders = new int[_bodyRenderers.Length];
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                _bodyColors[i] = _bodyRenderers[i].color;
                _bodySortingOrders[i] = _bodyRenderers[i].sortingOrder;
            }
            _halfHeight = _body.size.y * 0.5f;
            _weaponRestPosition = _weapon.localPosition;
            // 진영별 유닛 레이어 — 충돌 규칙은 그대로 두고 검색에서만 거른다. 적 찾기가 산 속 아군 수백 마리를 훑지 않게
            int ownLayer = LayerMask.NameToLayer(_team == Team.Ally ? "AllyUnit" : "EnemyUnit");
            gameObject.layer = ownLayer;
            _enemyFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = ~(1 << ownLayer) };
            _allyFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << ownLayer };
            // 그림은 오른쪽(아군 전방)을 보고 그려져 있다 — 전방이 -x면 자식(무기)까지 통째로 뒤집는다
            if (Forward < 0f)
                transform.localScale = Vector3.Scale(transform.localScale, new Vector3(-1f, 1f, 1f));
        }

        // 한 생애의 시작 — 처음 만들 때도, 풀에서 다시 꺼낼 때도(Pooled) 여기서 지난 생애의 흔적을 모두 지운다
        private void OnEnable()
        {
            _hp = _definition.MaxHp;
            _lastX = transform.position.x;
            _advanceSpeed = 0f;
            _attackCooldown = 0f;
            _knockbackTimer = 0f;
            _runTime = 0f;
            _bigKnockbackUsed = false;
            _stompedOn = null;
            _slamTimer = _definition.SlamInterval;
            _slamJumping = false;
            _slamLeftGround = false;
            _onBase = false;
            _despawnTimer = 0f;
            // 검색 시점을 유닛마다 흩어 한 스텝에 몰리지 않게
            _targetScanTimer = Random.Range(0f, TargetScanInterval);
            _cachedTarget = null;
            _cachedTargetCollider = null;
            _supportScanTimer = Random.Range(0f, TargetScanInterval);
            _walkScanTimer = Random.Range(0f, TargetScanInterval);
            _cachedPatient = null;
            _aheadOnBaseUnit = false;

            // 죽을 때 바꾼 물리·그림을 되돌린다(Die)
            foreach (Collider2D col in GetComponents<Collider2D>())
                col.enabled = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                _bodyRenderers[i].color = _bodyColors[i];
                _bodyRenderers[i].sortingOrder = _bodySortingOrders[i];
            }
            _weaponRenderer.sortingOrder = _weaponSortingOrder;
            _weapon.localPosition = _weaponRestPosition;
            _weapon.localRotation = Quaternion.identity;
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
            _squashTime = SquashDuration;
            _flashTimer = 0f;
            _thrustTime = ThrustDuration;

            // 같은 진영 기지 통과 — 콜라이더를 껐다 켜면 무시 설정이 풀릴 수 있어 생애마다 다시 건다
            Base.IgnoreOwnBase(_team, _body);
        }

        private void FixedUpdate()
        {
            if (!IsAlive)
                return;

            _advanceSpeed = (_rb.position.x - _lastX) * Forward / Time.fixedDeltaTime;
            _lastX = _rb.position.x;
            _attackCooldown -= Time.fixedDeltaTime;
            _targetScanTimer -= Time.fixedDeltaTime;
            _supportScanTimer -= Time.fixedDeltaTime;
            _walkScanTimer -= Time.fixedDeltaTime;

            // 접촉 법선은 상대 → 나 방향: 위를 향하면 발밑, 전방 반대를 향하면 앞에서 막힌 것
            bool grounded = false;
            bool canClimb = false;
            bool blockedByEnemy = false;
            Unit allyBelow = null;
            Unit allyAbove = null;
            Unit enemyBelow = null;
            _onBase = false;
            ContactsMarker.Begin();
            int count = _rb.GetContacts(_contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = _contacts[i];
                // 컴포넌트 조회는 접촉 하나에 한 번만 — 산 속 유닛은 접촉이 많아 여기가 제일 자주 돈다
                contact.collider.TryGetComponent(out Unit touched);
                if (contact.normal.y < -0.5f && touched != null && touched._team == _team)
                    allyAbove = touched;
                if (contact.normal.y > 0.5f)
                {
                    grounded = true;
                    if (touched != null)
                    {
                        if (touched._team == _team)
                            allyBelow = touched;
                        else
                            enemyBelow = touched;
                    }
                    else if (contact.collider.TryGetComponent(out Base _))
                        _onBase = true;
                }
                if (contact.normal.x * Forward < -0.5f && touched != null && touched._team != _team)
                    blockedByEnemy = true;   // 발밑 판정과 별개 — 대각선 접촉(발밑이면서 앞)도 막힌 것
                else if (contact.normal.y <= 0.5f && contact.normal.x * Forward < -0.5f && touched != null   // 앞의 아군
                         && !canClimb
                         && touched._advanceSpeed < MoveSpeed * ClimbSlowerThan   // 나보다 느리면(멈추지 않았어도) 넘는다 — 같은 속도로 같이 걷는 줄은 안 넘는다
                         && !touched._onBase
                         && ClimbTarget(touched) != null)   // 물리 검사라 비싸다 — 싼 조건을 다 통과한 뒤 마지막에
                    canClimb = true;
            }
            ContactsMarker.End();

            // 적 머리 위에 새로 내려앉은 순간 (계속 서 있는 동안은 다시 안 부른다)
            if (enemyBelow != null && enemyBelow != _stompedOn && enemyBelow.IsAlive)
            {
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesTo(_definition))
                        e.Effect.OnLandedOnEnemy(this, enemyBelow, e.Stacks);
            }
            _stompedOn = enemyBelow;

            // 점프 착지 충격: 뛰어오른 뒤 한 번이라도 땅에서 떨어졌다가 다시 발이 닿는 순간 (넉백 중이어도 착지는 착지)
            if (_slamJumping)
            {
                if (!grounded)
                    _slamLeftGround = true;
                else if (_slamLeftGround)
                {
                    _slamJumping = false;
                    Slam();
                }
                else
                {
                    // 아직 발을 못 뗐다 — 뛴 스텝에 맞으면 넉백 속도가 점프 속도를 덮어쓴다. 뜰 때까지 다시 준다
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, JumpSpeed(_definition.SlamJumpHeight, _rb));
                    grounded = false;
                }
            }

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.fixedDeltaTime;
                return;
            }

            // 공격·이동과 별개로 매 스텝 도는 효과(칼 던지기 등)
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesTo(_definition))
                    e.Effect.Tick(this, e.Stacks);

            // 점프 착지 충격: 간격마다 제자리에서 뛴다. 이번 스텝의 등반 점프 등이 덮어쓰지 않게 땅에서 떨어진 것으로 친다
            if (_definition.SlamInterval > 0f && !_slamJumping)
            {
                _slamTimer -= Time.fixedDeltaTime;
                if (_slamTimer <= 0f && grounded)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, JumpSpeed(_definition.SlamJumpHeight, _rb));
                    _slamTimer = _definition.SlamInterval;
                    _slamJumping = true;
                    _slamLeftGround = false;
                    grounded = false;
                }
            }

            Vector2 velocity = _rb.linearVelocity;

            // 층 정렬은 등반 조건(BelongsBelow)으로 애초에 뒤집혀 오르지 않게 한다. 넉백·착지 등으로 이미 뒤집혀 올라탔으면
            // 내가 바로 아래 아군보다 아래층 역할일 때(예: 원딜 위의 탱커) 싸움보다 먼저 앞으로 걸어 내려간다.
            // 밑의 아군은 그동안 멈춰 선다(BelongsBelow) — 내가 앞쪽 땅에 떨어지면 그 아군이 뒤에서 내 위로 올라탄다(보통 등반 점프).
            // 앞이 적 몸으로 막혔으면 내려갈 곳이 없으니 그 자리에서 싸운다
            if (allyBelow != null && BelongsBelow(allyBelow) && !blockedByEnemy)
            {
                velocity.x = Forward * MoveSpeed;
                _rb.linearVelocity = velocity;
                return;
            }
            bool holdForAbove = allyAbove != null && allyAbove.BelongsBelow(this);

            // 프리스트 — 적은 공격하지 않는다. 다친 아군을 회복하고, 적이 사거리에 들면 뒤에 멈춰 선다(앞으로 걸어가 죽지 않게)
            if (_definition.AttackType == AttackType.Heal)
            {
                bool enemyNear = FindTargetCached(out _, out _);
                // 회복 대상 찾기도 아군 전부를 훑어 비싸다 — 적 찾기와 같은 간격으로만 새로 찾고, 그 사이엔 찾아 둔 대상이 아직 유효한지만 본다
                SupportMarker.Begin();
                if (_supportScanTimer <= 0f)
                {
                    _cachedPatient = FindHealTarget(null);
                    _supportScanTimer = TargetScanInterval;
                }
                if (_cachedPatient != null && !_cachedPatient.IsDamaged)
                    _cachedPatient = null;
                Unit patient = _cachedPatient;
                SupportMarker.End();
                if (patient != null && _attackCooldown <= 0f)
                {
                    Vector2 toPatient = patient._rb.position - _rb.position;
                    ThrowHeal(patient);
                    foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                        if (e.Effect.AppliesTo(_definition))
                            e.Effect.OnHealed(this, patient, e.Stacks);
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
            // 싸움이 점프·전진보다 우선 (프리스트는 위에서 처리 — 회복할 대상도 적도 없으면 여기로 내려와 전진)
            if (_definition.AttackType != AttackType.Heal && FindTargetCached(out IDamageable target, out Vector2 targetPoint))
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
                    else if (_definition.AttackType == AttackType.Sweep)
                    {
                        Sweep();
                    }
                    else if (_definition.AttackType == AttackType.Ranged)
                    {
                        FireProjectile(targetPoint);
                        foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                            if (e.Effect.AppliesTo(_definition))
                                e.Effect.OnRangedShot(this, target, e.Stacks);
                    }
                    else
                    {
                        // 달려와서 치는 첫 타격은 돌격 — 피해·밀치기에 배율
                        bool charge = _runTime >= _definition.ChargeReadySeconds;
                        float damage = _definition.AttackDamage * (charge ? _definition.ChargeDamageMultiplier : 1f) * DamageScale;
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
            WalkMarker.Begin();
            if (_walkScanTimer <= 0f)
            {
                _aheadOnBaseUnit = IsAheadOnBaseUnit();
                _walkScanTimer = TargetScanInterval;
            }
            velocity.x = _aheadOnBaseUnit || holdForAbove ? 0f : Forward * MoveSpeed;
            WalkMarker.End();
            // 못 올라탈 땐 뛰지 않고 서 있어야 뒤 유닛의 발판이 된다 — 계속 뛰면 계단(산)이 안 생긴다
            if (grounded && canClimb)
                velocity.y = Mathf.Sqrt(2f * -Physics2D.gravity.y * _rb.gravityScale * JumpHeight);
            _rb.linearVelocity = velocity;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, Unit attacker)
        {
            if (!IsAlive)
                return;

            _hp -= amount;
            DamageNumbers.Damage(this, _team, new Vector2(_rb.position.x, Top), amount);
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

            // 피격 효과(돌격 반사 등) — 하나라도 true면 이번 피격에 밀리지 않는다
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesTo(_definition) && e.Effect.OnHit(this, attacker, hitDirection, push, e.Stacks))
                    return;

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
            _squashVertical = Mathf.Abs(hitDirection.y) > Mathf.Abs(hitDirection.x);
            // 밀치는 힘이 0인 공격(화염)은 밀림·경직 없음 — 연속으로 맞아도 반격할 수 있어야 한다
            if (push <= 0f)
                return;
            _knockbackTimer = KnockbackDuration * Mathf.Max(1f, push);
            _rb.linearVelocity = hitDirection * (_definition.HitKnockback * push);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                // 뒤집혀 화면 밖으로 떨어지는 중 — 다 떨어지면 풀로 돌아간다
                _despawnTimer -= Time.deltaTime;
                if (_despawnTimer <= 0f)
                    gameObject.SetActive(false);
                return;
            }

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
                    _bodyRenderers[i].color = _flashTimer > 0f ? SparkColor : _bodyColors[i]; // 몸이 흰색이라 흰 번쩍임은 안 보인다
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

        private bool FindTarget(out IDamageable target, out Vector2 targetPoint)
        {
            // 사거리 안에서 가장 가까운 적 — 사거리가 긴 원거리딜이 먼 적부터 쏘지 않게
            Physics2D.OverlapCircle(_rb.position, AttackRange, _enemyFilter, _overlaps);
            target = null;
            targetPoint = default;
            float best = float.MaxValue;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive)
                    continue;
                if (IsUnderfoot(damageable))
                    continue;

                Vector2 point = col.ClosestPoint(_rb.position);
                float distance = (point - _rb.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = damageable;
                    targetPoint = point;
                    _foundCollider = col;
                }
            }
            return target != null;
        }

        // 산이 넘쳐흐르게 — 적 머리 위에 선 근접은 발밑 적을 치느라 멈추지 않고 머리를 밟고 계속 걷는다.
        // 같은 높이의 적을 만나거나 적 줄 끝에서 떨어지면 다시 싸운다. 원거리·보스·기지·탑은 그대로
        private bool IsUnderfoot(IDamageable damageable)
            => _definition.AttackType == AttackType.Melee && damageable is Unit unit && unit.Top <= Feet + StandOnMargin;

        // 적 찾기는 비싸다(사거리 원 안의 콜라이더를 전부 본다 — 산 속 원딜은 수백 개). 그래서 TargetScanInterval마다만 새로 찾고,
        // 그 사이엔 찾아 둔 적이 아직 살아 있고 사거리 안인지만 본다. 못 찾았으면 다음 검색까지 없는 것으로 친다
        private bool FindTargetCached(out IDamageable target, out Vector2 targetPoint)
        {
            using (ScanMarker.Auto())
                return FindTargetCachedCore(out target, out targetPoint);
        }

        private bool FindTargetCachedCore(out IDamageable target, out Vector2 targetPoint)
        {
            if (_targetScanTimer > 0f)
            {
                target = null;
                targetPoint = default;
                if (_cachedTarget == null)
                    return false;
                if (_cachedTarget.IsAlive && _cachedTargetCollider.enabled && !IsUnderfoot(_cachedTarget))
                {
                    targetPoint = _cachedTargetCollider.ClosestPoint(_rb.position);
                    float range = AttackRange;
                    if ((targetPoint - _rb.position).sqrMagnitude <= range * range)
                    {
                        target = _cachedTarget;
                        return true;
                    }
                }
            }

            _targetScanTimer = TargetScanInterval;
            bool found = FindTarget(out target, out targetPoint);
            _cachedTarget = found ? target : null;
            _cachedTargetCollider = found ? _foundCollider : null;
            return found;
        }

        // 원거리 무기(화살·돌) 발사
        public void FireProjectile(Vector2 targetPoint)
        {
            Projectile shot = Pooled.Get(_definition.Projectile, _weapon.position, Quaternion.identity);
            shot.Launch(_team, _definition.AttackDamage * DamageScale, _definition.PushPower, targetPoint,
                        _definition.ProjectileArcHeight * LongRangeScale, _definition.ProjectileSplashRadius);
        }

        public void ThrowHeal(Unit patient)
        {
            Projectile orb = Pooled.Get(_definition.Projectile, _weapon.position, Quaternion.identity);
            orb.LaunchHeal(this, _definition.HealAmount, patient._rb.position, _definition.ProjectileArcHeight);
        }

        // 사거리 안에서 체력 비율이 가장 낮은 다친 아군(자기·exclude 제외)
        public Unit FindHealTarget(Unit exclude)
        {
            Physics2D.OverlapCircle(_rb.position, _definition.AttackRange, _allyFilter, _overlaps);
            Unit best = null;
            float bestRatio = 1f;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit ally) || ally == this || ally == exclude || ally._team != _team || !ally.IsAlive)
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
            Physics2D.OverlapBox(center, new Vector2(length, _definition.FlameThickness), 0f, _enemyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (col.TryGetComponent(out IDamageable damageable) && damageable.Team != _team && damageable.IsAlive)
                    damageable.TakeDamage(_definition.AttackDamage * DamageScale, new Vector2(Forward, 0f), _definition.PushPower, null);
            }
            if (FxDirector.Instance != null)
                FxDirector.Instance.Flame(mouth, Forward, length);
        }

        // 휩쓸기 — 사거리 안, 몸 중심보다 앞쪽(가장 가까운 점 기준)에 있는 적 전부(탑·기지 포함)
        private void Sweep()
        {
            Physics2D.OverlapCircle(_rb.position, AttackRange, _enemyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive)
                    continue;
                Vector2 point = col.ClosestPoint(_rb.position);
                if ((point.x - _rb.position.x) * Forward < 0f)
                    continue;
                Vector2 toTarget = point - _rb.position;
                Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : new Vector2(Forward, 0f);
                damageable.TakeDamage(_definition.AttackDamage * DamageScale, direction, _definition.PushPower, this);
            }
        }

        // 점프 착지 충격 — 발밑 반경 안의 적 유닛(탑·기지 제외)에게 피해를 주고 위로 띄운다
        private void Slam()
        {
            Vector2 feet = new Vector2(_rb.position.x, Feet);
            Physics2D.OverlapCircle(feet, _definition.SlamRadius, _enemyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit enemy) || enemy._team == _team || !enemy.IsAlive)
                    continue;
                enemy.TakeDamage(_definition.SlamDamage * DamageScale, Vector2.up, 0f, this);
                enemy.Shove(new Vector2(0f, JumpSpeed(_definition.SlamLiftHeight, enemy._rb)), _definition.SlamStun);
            }
        }

        // 이 높이까지 솟는 위쪽 속도
        private static float JumpSpeed(float height, Rigidbody2D body)
        {
            return Mathf.Sqrt(2f * -Physics2D.gravity.y * body.gravityScale * height);
        }

        // 피해 없이 밀어낸다(보스 등장 충격파 등). 그동안 조종 불능
        public void Shove(Vector2 velocity, float stunSeconds)
        {
            if (!IsAlive)
                return;
            _knockbackTimer = stunSeconds;
            _rb.linearVelocity = velocity;
        }

        public void Heal(float amount)
        {
            if (!IsAlive)
                return;
            float before = _hp;
            _hp = Mathf.Min(_hp + amount, _definition.MaxHp);
            DamageNumbers.Heal(this, new Vector2(_rb.position.x, Top), _hp - before);
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

        // 나는 other보다 아래층 역할인가(예: 원딜 위의 탱커) — 층 정렬에서 위에 탄 쪽이 내려갈지 정한다
        private bool BelongsBelow(Unit other)
        {
            return _definition.StackRank < other._definition.StackRank;
        }

        private void Die()
        {
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

            _despawnTimer = DeathDestroyDelay;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 진영 본진 — 체력이 0이 되면 진다. 본진은 타워라서 사거리 안의 적에게 화살을 쏜다.
    // 화살이 여러 발이면 가까운 적부터 한 발씩 나눠 쏘고(사용자 결정), 적이 모자라면 처음부터 다시 돈다. 사거리 안이면 적 본진도 쏜다
    public class Base : MonoBehaviour, IDamageable
    {
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };

        [SerializeField] private BaseDefinition _definition;
        [SerializeField] private Team _team;
        [Tooltip("화살이 나가는 자리(본진 꼭대기)")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("작은 타워(건축가가 짓는다) — 공격력·체력에 정의의 작은 타워 배율. 부서져도 지지 않는다. Base.For에 안 잡힌다")]
        [SerializeField] private bool _small;
        [Tooltip("작은 타워 꼭대기의 진영 깃발 — 지을 때 본진 깃발 색으로 칠한다(본진은 비워 둔다 — 깃발은 Tower가 맡는다)")]
        [SerializeField] private SpriteRenderer _flag;
        [Tooltip("작은 타워의 기계 다리 — 내 작은 타워가 걸어가는 진영(움직이는 성)에서만 보이고, 그 높이만큼 본체가 올라간다. 프리팹에서는 본체 밑에 꺼 둔다(본진은 비워 둔다)")]
        [SerializeField] private SpriteRenderer _legs;
        [Tooltip("다리 걸음 그림 — 걸어간 거리에 따라 번갈아 보인다")]
        [SerializeField] private Sprite[] _legFrames;

        private static readonly List<Base> All = new List<Base>();

        // 작은 타워 레이어 — 같은 진영 유닛·건축가는 통과(본진은 IgnoreOwnBase로 같은 일을 한다 — 작은 타워는 전투 중에 생겨 이미 나와 있는 유닛도 통과해야 하므로 레이어로).
        // 건축가는 같은 진영 유닛과도 부딪히지 않는다 — 산에 끼거나 행군을 거슬러 돌아오지 못하는 일이 없게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SetupLayers()
        {
            for (int t = 0; t < 2; t++)
            {
                int unit = LayerMask.NameToLayer(t == 0 ? "AllyUnit" : "EnemyUnit");
                int builder = LayerMask.NameToLayer(t == 0 ? "AllyBuilder" : "EnemyBuilder");
                int tower = LayerMask.NameToLayer(t == 0 ? "AllyTower" : "EnemyTower");
                Physics2D.IgnoreLayerCollision(unit, tower, true);
                Physics2D.IgnoreLayerCollision(builder, tower, true);
                Physics2D.IgnoreLayerCollision(unit, builder, true);
                Physics2D.IgnoreLayerCollision(builder, builder, true);
            }
        }

        public static int TowerLayer(Team team) => LayerMask.NameToLayer(team == Team.Ally ? "AllyTower" : "EnemyTower");
        public static int BuilderLayer(Team team) => LayerMask.NameToLayer(team == Team.Ally ? "AllyBuilder" : "EnemyBuilder");

        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private readonly List<(float distance, Vector2 point, bool isUnit)> _targets = new List<(float, Vector2, bool)>();
        private float _hp;
        private float _attackCooldown;
        private Collider2D _collider;
        // 계산해 둔 타워 스탯(TowerBook.Version이 바뀔 때만 다시)
        private readonly float[] _stats = new float[5];
        private int _statsVersion = -1;
        // 이 타워 한 채의 사거리 합연산 %(조립식 감시탑의 등급 값)
        private float _rangeBonus;

        public void SetRangeBonus(float percent)
        {
            _rangeBonus = percent;
            RefreshStats();
        }

        // 이 타워 한 채의 최대 체력을 본진의 비율로 정한다(전초 기지 — 0.2 = 본진의 20%). 0이면 정의의 작은 타워 배율 그대로
        private float _hpOfHome;

        public void SetHpOfHome(float ratio)
        {
            _hpOfHome = ratio;
            RefreshStats();
            _hp = MaxHp;
        }

        // 이 타워 한 채의 체력 합연산 %(성벽 보강 — 본진에만 건다). '내 타워' 효과(TowerBook)의 %와 한 번에 더해진다
        private float _hpBonus;

        public void SetHpBonus(float percent)
        {
            _hpBonus = percent;
            RefreshStats();
        }

        public event System.Action<Base> Destroyed;

        // 이 작은 타워를 세운 기물(조립식 감시탑이 적는다) — 「이 작은 타워」를 가리는 능력이 읽는다. 건축가가 지었으면 null
        public OwnedBuilding BuiltBy { get; set; }

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public float Hp => _hp;
        public float MaxHp => Stat(TowerStat.MaxHp);
        public float Hp01 => Mathf.Clamp01(_hp / MaxHp);

        // 치트 훅(Dev/CheatPanel만 부른다) — 지금 체력을 바로 넣는다(1 ~ 최대)
        public void CheatSetHp(float hp) => _hp = Mathf.Clamp(hp, 1f, MaxHp);

        public static Base For(Team team)
        {
            foreach (Base b in All)
                if (b._team == team && !b._small)
                    return b;
            return null;
        }

        public bool IsSmall => _small;
        public BaseDefinition Definition => _definition;
        public Bounds Bounds => _collider.bounds;
        // 화살이 나가는 자리 — 내 타워를 강화하는 버프 선이 여기로 간다(BuffLinks.ToTowers)
        public Vector2 Muzzle => _muzzle.position;

        // 진영의 살아 있는 작은 타워 — 건축가가 빈자리를 고를 때
        public static IEnumerable<Base> SmallTowers()
        {
            foreach (Base b in All)
                if (b._small && b.IsAlive)
                    yield return b;
        }

        // 작은 타워를 짓는다 — 밑면(콜라이더 아래 끝)을 feet에. 양 진영 같은 프리팹(진영은 여기서 정한다)
        public static Base Build(Base prefab, Team team, Vector2 feet)
        {
            var box = prefab.GetComponent<BoxCollider2D>();
            Vector3 scale = prefab.transform.localScale;
            Vector2 bottom = new Vector2(box.offset.x * scale.x, (box.offset.y - box.size.y * 0.5f) * scale.y);
            Base tower = Instantiate(prefab, feet - bottom, Quaternion.identity);
            tower._team = team;
            tower.gameObject.layer = TowerLayer(team);
            if (team == Team.Enemy)
                tower.transform.localScale = Vector3.Scale(scale, new Vector3(-1f, 1f, 1f));
            if (tower._flag != null && Tower.For(team) is Tower main)
                tower._flag.color = main.FlagColor;
            // Start는 다음 프레임 — 그 전에도 살아 있는 타워로 세어지게(같은 프레임에 세우는 두 번째 타워의 자리 고르기 · 소환 자리)
            tower.RefreshStats();
            tower._hp = tower.MaxHp;
            return tower;
        }

        // 작은 타워를 x에 세운다 — 밑면은 타워 폭 안에서 가장 낮은 땅(비탈에서도 뜨지 않는다)
        public static Base BuildOnGround(Base prefab, Team team, float x)
            => Build(prefab, team, new Vector2(x, LowestGround(x, HalfWidthOf(prefab))));

        public static float HalfWidthOf(Base prefab)
            => prefab.GetComponent<BoxCollider2D>().size.x * Mathf.Abs(prefab.transform.localScale.x) * 0.5f;

        private const float GroundSampleStep = 0.25f;

        private static float LowestGround(float x, float half)
        {
            float min = float.MaxValue;
            for (float s = x - half; s <= x + half + 0.001f; s += GroundSampleStep)
                min = Mathf.Min(min, Ground.Instance.HeightAt(s));
            return min;
        }

        // 진영의 살아 있는 작은 타워 중 가장 앞에 있는 것(없으면 null) — 파견 사무소
        public static Base FrontSmallTower(Team team)
        {
            float forward = team == Team.Ally ? 1f : -1f;
            Base front = null;
            foreach (Base b in All)
                if (b._small && b._team == team && b.IsAlive
                    && (front == null || (b.transform.position.x - front.transform.position.x) * forward > 0f))
                    front = b;
            return front;
        }

        // 다리 그림이 바뀌는 걸음 폭(걸어간 거리)
        private const float LegStride = 0.5f;

        // 기계 다리를 펴거나 접는다 — 본체(그림·깃발·발사점)가 다리 그림 높이만큼 오르내리고, 충돌 박스는 아래 끝을 땅에 둔 채 위로만 늘고 준다.
        // 다리는 프리팹에서 본체 밑에 붙어 있다(꺼 둔 채) — 본체를 올리면 다리가 땅을 딛는다
        private void SetLegs(bool on)
        {
            _legs.enabled = on;
            float lift = _legs.sprite.bounds.size.y * (on ? 1f : -1f);
            var box = (BoxCollider2D)_collider;
            box.size += new Vector2(0f, lift);
            box.offset -= new Vector2(0f, lift * 0.5f);
            transform.position += Vector3.up * (lift * transform.localScale.y);
        }

        // 움직이는 성 — 작은 타워가 적 성 앞까지 앞으로만 걸어간다(내 유닛과 상관없이). 앞선 내 타워와는 Spacing을 두고 선다.
        // 옮길 자리에 적(유닛·타워·본진)이 닿아 있으면 이번 스텝은 서 있는다 — 타워가 적을 밀어내지 않게. 걷는 진영의 타워만 기계 다리 위에 올라선다
        private void Follow()
        {
            TowerBook.FollowRule rule = TowerBook.FollowOf(_team);
            if (rule.Speed > 0f != _legs.enabled)
            {
                SetLegs(rule.Speed > 0f);
                return;   // 바뀐 충돌 박스는 다음 물리 스텝부터 bounds에 잡힌다
            }
            Base enemy = For(_team == Team.Ally ? Team.Enemy : Team.Ally);
            if (rule.Speed <= 0f || enemy == null)
                return;
            float forward = _team == Team.Ally ? 1f : -1f;
            Vector3 position = transform.position;
            float half = _collider.bounds.extents.x;
            // 갈 수 있는 끝(전방 거리) — 적 성 앞면, 앞선 내 타워가 있으면 그 Spacing 뒤
            float room = ((forward > 0f ? enemy.Bounds.min.x : enemy.Bounds.max.x) - position.x) * forward - half;
            foreach (Base b in All)
                if (b != this && b._small && b._team == _team && b.IsAlive)
                {
                    float d = (b.transform.position.x - position.x) * forward;
                    if (d > 0f || (d == 0f && b.GetInstanceID() < GetInstanceID()))
                        room = Mathf.Min(room, d - rule.Spacing);
                }
            if (room <= 0f)
                return;
            float x = position.x + forward * Mathf.Min(room, rule.Speed * Time.fixedDeltaTime);
            // 밑면을 새 자리의 땅에 — 지을 때와 같은 규칙(폭 안에서 가장 낮은 땅)
            float bottomOffset = position.y - _collider.bounds.min.y;
            var next = new Vector3(x, LowestGround(x, half) + bottomOffset, position.z);
            Physics2D.OverlapBox((Vector2)(_collider.bounds.center + next - position), _collider.bounds.size, 0f, SolidOnly, _overlaps);
            foreach (Collider2D col in _overlaps)
                if ((col.TryGetComponent(out Unit unit) && unit.Team != _team && unit.IsAlive)
                    || (col.TryGetComponent(out Base other) && other._team != _team && other.IsAlive))
                    return;
            transform.position = next;
            _legs.sprite = _legFrames[Mathf.FloorToInt(Mathf.Abs(x) / LegStride) % _legFrames.Length];
        }

        // 내 타워 전부(본진 + 작은 타워)가 최대 체력 대비 비율로 회복(성채)
        public static void HealTowers(Team team, float ratio)
        {
            foreach (Base b in All)
                if (b._team == team)
                    b.HealRatio(ratio);
        }

        // 새 유닛이 같은 진영 성을 몸으로 통과하게 한다 — 성문 안에서 나와 걸어 나간다. 성 뒤쪽 BackStop과는 그대로 부딪힌다
        public static void IgnoreOwnBase(Team team, Collider2D unitCollider)
        {
            foreach (Base b in All)
                if (b._team == team)
                    Physics2D.IgnoreCollision(unitCollider, b._collider);
        }

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
        }

        // 최대 체력은 라운드에 따라 오른다(RunState.BaseHpBonus) — 런 상태를 BattleManager.Awake가 시작한 뒤에 계산
        private void Start()
        {
            RefreshStats();
            _hp = MaxHp;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private float Stat(TowerStat stat)
        {
            if (_statsVersion != TowerBook.Version)
                RefreshStats();
            return _stats[(int)stat];
        }

        // 최대 체력이 바뀌면 지금 체력도 같은 비율로
        private void RefreshStats()
        {
            float oldMaxHp = _stats[(int)TowerStat.MaxHp];
            float oldRange = _stats[(int)TowerStat.Range];
            // 작은 타워 = 본진 수치 × 배율(정의) — 그 위에 '내 타워' 효과(TowerBook)를 본진과 똑같이
            float attackScale = _small ? _definition.SmallAttackScale : 1f;
            float hpScale = _hpOfHome > 0f ? _hpOfHome : _small ? _definition.SmallHpScale : 1f;
            _stats[(int)TowerStat.Attack] = TowerBook.Compute(_team, TowerStat.Attack, _definition.AttackDamage * attackScale, _small ? TowerBook.SmallAttackOf(_team) : 0f);
            _stats[(int)TowerStat.AttackSpeed] = TowerBook.Compute(_team, TowerStat.AttackSpeed, 1f);
            _stats[(int)TowerStat.Range] = TowerBook.Compute(_team, TowerStat.Range, _definition.AttackRange, _rangeBonus);
            _stats[(int)TowerStat.ArrowCount] = TowerBook.Compute(_team, TowerStat.ArrowCount, 1f);
            _stats[(int)TowerStat.MaxHp] = TowerBook.Compute(_team, TowerStat.MaxHp, (_definition.MaxHp + RunState.BaseHpBonus) * hpScale, _hpBonus);
            _statsVersion = TowerBook.Version;
            // 전투 중에 사거리가 늘었다(조준연산코그) — 이 타워 둘레에 새 사거리 고리(같은 타워는 1초에 한 번)
            if (BattleManager.Fighting && oldRange > 0f && _stats[(int)TowerStat.Range] > oldRange + 0.001f)
                RingFx.Pulse(transform.position, _stats[(int)TowerStat.Range], BuffLinkFx.ColorOf(BuffKind.Stat), this, 1f);
            if (oldMaxHp > 0f && IsAlive)
                _hp *= _stats[(int)TowerStat.MaxHp] / oldMaxHp;
        }

        private void FixedUpdate()
        {
            if (_small)
                Follow();
            if (!IsAlive || !CanAttack)
            {
                _volley.Clear();
                return;
            }
            _volley.Tick(Time.fixedDeltaTime, _team, _muzzle.position, this);
            _attackCooldown -= Time.fixedDeltaTime;
            if (_attackCooldown > 0f || !FindTargets())
                return;

            Shoot(Mathf.Max(1, Mathf.RoundToInt(Stat(TowerStat.ArrowCount))));
            _attackCooldown = Unit.NextCooldown(_attackCooldown, _definition.AttackInterval / Stat(TowerStat.AttackSpeed));
        }

        // 찾아 둔 대상(_targets)에게 화살 arrows발 — 가까운 적부터 한 발씩 나눠 쏜다. 쿨다운은 부르는 쪽이 돌린다.
        // extra = 처치해서 더 쏜 발(노란 꼬리 — Fx/ArrowTrails). 여러 발이면 첫 발 뒤로 짧은 시차를 두고 연달아 나간다(Units/Volley — 대상은 지금 정한 그대로)
        private void Shoot(int arrows, bool extra = false)
        {
            float damage = Stat(TowerStat.Attack);
            // 타워는 늘 높은 고도 — 적 유닛에게 기름투하대 보너스(적 본진엔 없음) · 관통탄
            float highGround = 1f;
            bool pierce = false;
            float pierceBonus = 0f;
            float farBonus = 0f;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
            {
                highGround += e.Effect.HighGroundDamageBonus(e.Level);
                pierce |= e.Effect.TowerArrowsPierce;
                pierceBonus += e.Effect.PierceHitDamageBonus;
                farBonus += e.Effect.FarShotDamageBonus;
            }
            // 사거리 끝 절반의 적(감시 망원경 신화) — 본진 중심에서 사거리 절반보다 먼 대상
            float halfRange = Stat(TowerStat.Range) * 0.5f;
            for (int i = 0; i < arrows; i++)
            {
                var target = _targets[i % _targets.Count];
                bool far = farBonus > 0f && (target.point - (Vector2)transform.position).sqrMagnitude > halfRange * halfRange;
                _volley.Fire(new Volley.Shot
                {
                    Prefab = _definition.Projectile,
                    Target = target.point,
                    Damage = damage * (target.isUnit ? highGround : 1f) * (far ? 1f + farBonus : 1f),
                    Push = _definition.PushPower,
                    Arc = _definition.ProjectileArcHeight,
                    Pierce = pierce,
                    PierceBonus = pierceBonus,
                    HighGround = target.isUnit,   // 타워는 적 유닛에게 늘 높은 고도(기름투하대)
                    Boosted = highGround > 1f,
                    Extra = extra,
                }, i, _team, _muzzle.position, this);
            }
        }

        // 전장에서 가장 가까운 적 유닛 하나에게 화살 arrows발(망루 — 사거리와 상관없이). 쏠 수 없거나(죽음 · 데스매치) 적 유닛이 없으면 false.
        // 대상만 이 자리에서 정하고 쏘는 길은 Shoot 그대로 — 평소 사격의 쿨다운은 건드리지 않는다
        public bool ShootNearestUnit(int arrows)
        {
            if (!IsAlive || !CanAttack || arrows <= 0)
                return false;
            Vector2 center = transform.position;
            Unit nearest = null;
            float best = float.MaxValue;
            foreach (Unit unit in FindObjectsByType<Unit>())
                if (unit.IsAlive && unit.Team != _team && (unit.Position - center).sqrMagnitude < best)
                {
                    nearest = unit;
                    best = (unit.Position - center).sqrMagnitude;
                }
            if (nearest == null)
                return false;
            _targets.Clear();
            _targets.Add((best, nearest.Position, true));
            Shoot(arrows);
            return true;
        }

        // 여러 발의 2번째 발부터 — 짧은 시차로 연달아(Units/Volley)
        private readonly Volley _volley = new Volley();

        // 데스매치에서는 오버드라이브가 있는 진영의 타워만 쏜다
        private bool CanAttack => !BattleManager.TowersStopped || TowerBook.AttacksInDeathmatch(_team);

        // 지금 쏠 수 있는 타워(본진 · 작은 타워) 중 사거리 안에 겨눌 적이 있는 것이 하나라도 있나 — 데스매치 판정(BattleManager)
        public static bool AnyTowerHasTarget()
        {
            foreach (Base b in All)
                if (b.IsAlive && b.CanAttack && b.FindTargets())
                    return true;
            return false;
        }

        // 사거리 안의 적(유닛 · 적 본진)을 가까운 순으로 _targets에
        private bool FindTargets()
        {
            Vector2 center = transform.position;
            float range = Stat(TowerStat.Range);
            Physics2D.OverlapCircle(center, range, SolidOnly, _overlaps);
            _targets.Clear();
            foreach (Collider2D col in _overlaps)
            {
                if (col.TryGetComponent(out Unit unit) && unit.Team != _team && unit.IsAlive)
                    _targets.Add((((Vector2)col.bounds.center - center).sqrMagnitude, col.bounds.center, true));
                else if (col.TryGetComponent(out Base enemyBase) && enemyBase._team != _team && enemyBase.IsAlive)
                {
                    Vector2 point = col.ClosestPoint(center);
                    _targets.Add(((point - center).sqrMagnitude, point, false));
                }
            }
            _targets.Sort((a, b) => a.distance.CompareTo(b.distance));
            return _targets.Count > 0;
        }

        // 최대 체력 대비 비율로 회복(성채)
        public void HealRatio(float ratio)
        {
            if (!IsAlive)
                return;
            float before = _hp;
            _hp = Mathf.Min(_hp + MaxHp * ratio, MaxHp);
            HealFx.Received(this, new Vector2(_collider.bounds.center.x, _collider.bounds.max.y), _hp - before, MaxHp);
        }

        // 이 타워의 화살이 적 유닛을 처치했다 — 조준연산코그 · 오버드라이브.
        // 즉시 더 쏘는 효과(최신 엔진 부품·오버드라이브 신화)가 켜져 있으면 이 타워가 그만큼 더 쏜다 —
        // 한 발이 더 처치하면 다시 돌지만, 발사는 처치 한 번마다 한 번이라 살아 있는 적 수에서 멈춘다
        public void NotifyKill(Unit victim)
        {
            BoardRunner.For(_team).NotifyTowerKill(victim);
            // 훅이 소환하면 그 유닛의 효과가 목록에 붙는다 — 지금 개수까지만(Unit.Perish와 같다)
            System.Collections.Generic.List<TeamEffects.Entry> effects = TeamEffects.For(_team);
            for (int i = 0, n = effects.Count; i < n; i++)
                effects[i].Effect.OnTowerKill(_team, this, victim);
            int extra = TowerBook.ArrowsOnKillOf(_team);
            if (extra > 0 && IsAlive && CanAttack && FindTargets())
                Shoot(extra, true);
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker)
        {
            if (!IsAlive)
                return;

            // 수호 기도 — 본진만, 확률로 이 피해를 받지 않는다(막은 자리에 무적 색 고리)
            if (!_small && amount > 0f)
            {
                float block = 0f;
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    block += e.Effect.BaseBlockChance(_team);
                if (block > 0f && Random.value < block)
                {
                    RingFx.Pulse(_collider.bounds.center, _collider.bounds.extents.y, BuffLinkFx.ColorOf(BuffKind.Invulnerable), this, BlockRingGap);
                    return;
                }
            }

            DamageMeter.Credit(attacker, _team, Mathf.Min(amount, Mathf.Max(0f, _hp)));   // 실제로 깎인 몫만(초과 피해 제외)
            _hp -= amount;
            if (amount > 0f)
                BattleManager.NoteProgress();
            DamageNumbers.Damage(this, _team, new Vector2(_collider.bounds.center.x, _collider.bounds.max.y), amount);
            // 성채 신화 — 내 타워들(본진 · 작은 타워)이 받은 피해를 공격한 쪽에 그대로. 이 한 방에 무너져도 돌려준다
            if (TowerBook.ReflectsDamage(_team))
                DamageReflect.Back(attacker, amount, hitDirection, this);
            if (!IsAlive && _small)
            {
                if (FxDirector.Instance != null)
                    FxDirector.Instance.DeathPuff(_collider.bounds.center);
                BoardRunner.For(_team).NotifySmallTowerDestroyed(this);
                Destroy(gameObject);   // 작은 타워 — 지는 건 아니다
            }
            else if (!IsAlive)
            {
                Debug.Log($"[Base] {_team} 기지 파괴");
                Destroyed?.Invoke(this);   // 그림은 무너짐 연출(Fx/BaseCollapse)이 땅속으로 내린 뒤 끈다
            }
            else if (!_small)
            {
                // 왕성 수비대가 여기서 소환한다 — 지금 개수까지만(Unit.Perish와 같다)
                System.Collections.Generic.List<TeamEffects.Entry> effects = TeamEffects.For(_team);
                for (int i = 0, n = effects.Count; i < n; i++)
                    effects[i].Effect.OnBaseDamaged(this);
            }
        }

        // 막은 고리를 다시 띄우기까지(초) — 화살이 몰려도 고리가 겹쳐 쌓이지 않게
        private const float BlockRingGap = 0.3f;
    }
}

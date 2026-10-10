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

        public event System.Action<Base> Destroyed;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public float Hp => _hp;
        public float MaxHp => Stat(TowerStat.MaxHp);
        public float Hp01 => Mathf.Clamp01(_hp / MaxHp);

        public static Base For(Team team)
        {
            foreach (Base b in All)
                if (b._team == team && !b._small)
                    return b;
            return null;
        }

        public bool IsSmall => _small;
        public Bounds Bounds => _collider.bounds;

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
            return tower;
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
            // 작은 타워 = 본진 수치 × 배율(정의) — 그 위에 '내 타워' 효과(TowerBook)를 본진과 똑같이
            float attackScale = _small ? _definition.SmallAttackScale : 1f;
            float hpScale = _small ? _definition.SmallHpScale : 1f;
            _stats[(int)TowerStat.Attack] = TowerBook.Compute(_team, TowerStat.Attack, _definition.AttackDamage * attackScale);
            _stats[(int)TowerStat.AttackSpeed] = TowerBook.Compute(_team, TowerStat.AttackSpeed, 1f);
            _stats[(int)TowerStat.Range] = TowerBook.Compute(_team, TowerStat.Range, _definition.AttackRange);
            _stats[(int)TowerStat.ArrowCount] = TowerBook.Compute(_team, TowerStat.ArrowCount, 1f);
            _stats[(int)TowerStat.MaxHp] = TowerBook.Compute(_team, TowerStat.MaxHp, (_definition.MaxHp + RunState.BaseHpBonus) * hpScale);
            _statsVersion = TowerBook.Version;
            if (oldMaxHp > 0f && IsAlive)
                _hp *= _stats[(int)TowerStat.MaxHp] / oldMaxHp;
        }

        private void FixedUpdate()
        {
            if (BattleManager.TowersStopped && !TowerBook.AttacksInDeathmatch(_team))
                return;
            _attackCooldown -= Time.fixedDeltaTime;
            if (_attackCooldown > 0f || !FindTargets())
                return;

            int arrows = Mathf.Max(1, Mathf.RoundToInt(Stat(TowerStat.ArrowCount)));
            float damage = Stat(TowerStat.Attack);
            // 타워는 늘 높은 고도 — 적 유닛에게 기름투하대 보너스(적 본진엔 없음) · 관통탄
            float highGround = 1f;
            bool pierce = false;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
            {
                highGround += e.Effect.HighGroundDamageBonus(e.Level);
                pierce |= e.Effect.TowerArrowsPierce;
            }
            for (int i = 0; i < arrows; i++)
            {
                var target = _targets[i % _targets.Count];
                Projectile shot = Pooled.Get(_definition.Projectile, _muzzle.position, Quaternion.identity);
                shot.Launch(_team, damage * (target.isUnit ? highGround : 1f), _definition.PushPower, target.point, _definition.ProjectileArcHeight, 0f, this);
                if (pierce)
                    shot.Pierce();
            }
            _attackCooldown = Unit.NextCooldown(_attackCooldown, _definition.AttackInterval / Stat(TowerStat.AttackSpeed));
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
            DamageNumbers.Heal(this, new Vector2(_collider.bounds.center.x, _collider.bounds.max.y), _hp - before);
        }

        // 이 본진의 화살이 적 유닛을 처치했다 — 조준연산코그 · 오버드라이브
        public void NotifyKill(Unit victim) => BoardRunner.For(_team).NotifyTowerKill(victim);

        public void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker)
        {
            if (!IsAlive)
                return;

            _hp -= amount;
            DamageNumbers.Damage(this, _team, new Vector2(_collider.bounds.center.x, _collider.bounds.max.y), amount);
            if (!IsAlive && _small)
            {
                if (FxDirector.Instance != null)
                    FxDirector.Instance.DeathPuff(_collider.bounds.center);
                Destroy(gameObject);   // 작은 타워 — 지는 건 아니다
            }
            else if (!IsAlive)
            {
                Debug.Log($"[Base] {_team} 기지 파괴");
                gameObject.SetActive(false);
                Destroyed?.Invoke(this);
            }
        }
    }
}

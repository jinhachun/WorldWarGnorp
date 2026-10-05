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

        private static readonly List<Base> All = new List<Base>();

        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private readonly List<(float distance, Vector2 point)> _targets = new List<(float, Vector2)>();
        private float _hp;
        private float _attackCooldown;
        private Collider2D _collider;
        // 계산해 둔 타워 스탯(TowerBook.Version이 바뀔 때만 다시)
        private readonly float[] _stats = new float[5];
        private int _statsVersion = -1;

        public event System.Action<Base> Destroyed;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public float MaxHp => Stat(TowerStat.MaxHp);
        public float Hp01 => Mathf.Clamp01(_hp / MaxHp);

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
            _stats[(int)TowerStat.Attack] = TowerBook.Compute(_team, TowerStat.Attack, _definition.AttackDamage);
            _stats[(int)TowerStat.AttackSpeed] = TowerBook.Compute(_team, TowerStat.AttackSpeed, 1f);
            _stats[(int)TowerStat.Range] = TowerBook.Compute(_team, TowerStat.Range, _definition.AttackRange);
            _stats[(int)TowerStat.ArrowCount] = TowerBook.Compute(_team, TowerStat.ArrowCount, 1f);
            _stats[(int)TowerStat.MaxHp] = TowerBook.Compute(_team, TowerStat.MaxHp, _definition.MaxHp);
            _statsVersion = TowerBook.Version;
            if (oldMaxHp > 0f && IsAlive)
                _hp *= _stats[(int)TowerStat.MaxHp] / oldMaxHp;
        }

        private void FixedUpdate()
        {
            _attackCooldown -= Time.fixedDeltaTime;
            if (_attackCooldown > 0f || !FindTargets())
                return;

            int arrows = Mathf.Max(1, Mathf.RoundToInt(Stat(TowerStat.ArrowCount)));
            float damage = Stat(TowerStat.Attack);
            for (int i = 0; i < arrows; i++)
            {
                Projectile shot = Pooled.Get(_definition.Projectile, _muzzle.position, Quaternion.identity);
                shot.Launch(_team, damage, _definition.PushPower, _targets[i % _targets.Count].point, _definition.ProjectileArcHeight, 0f, this);
            }
            _attackCooldown = _definition.AttackInterval / Stat(TowerStat.AttackSpeed);
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
                    _targets.Add((((Vector2)col.bounds.center - center).sqrMagnitude, col.bounds.center));
                else if (col.TryGetComponent(out Base enemyBase) && enemyBase._team != _team && enemyBase.IsAlive)
                {
                    Vector2 point = col.ClosestPoint(center);
                    _targets.Add(((point - center).sqrMagnitude, point));
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
            if (!IsAlive)
            {
                Debug.Log($"[Base] {_team} 기지 파괴");
                gameObject.SetActive(false);
                Destroyed?.Invoke(this);
            }
        }
    }
}

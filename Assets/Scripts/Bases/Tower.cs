using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 전장에 선 탑 — 같은 진영 유닛은 몸이 통과하고(Unit.Awake), 적은 막혀서 부숴야 지나간다.
    // 사거리 안의 가장 가까운 적 유닛에게 화살을 쏜다
    public class Tower : MonoBehaviour, IDamageable
    {
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };
        private static readonly List<Tower> All = new List<Tower>();

        [SerializeField] private TowerDefinition _definition;
        [SerializeField] private Team _team;
        [Tooltip("화살이 나가는 자리(탑 꼭대기)")]
        [SerializeField] private Transform _muzzle;

        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private Collider2D _collider;
        private float _hp;
        private float _attackCooldown;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;

        // 새 유닛이 같은 진영 탑을 몸으로 통과하게 한다 (탑은 씬에 미리 있고 유닛은 나중에 생긴다)
        public static void IgnoreOwnTowers(Team team, Collider2D unitCollider)
        {
            foreach (Tower tower in All)
                if (tower._team == team)
                    Physics2D.IgnoreCollision(unitCollider, tower._collider);
        }

        private void Awake()
        {
            _hp = _definition.MaxHp;
            _collider = GetComponent<Collider2D>();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void FixedUpdate()
        {
            _attackCooldown -= Time.fixedDeltaTime;
            if (_attackCooldown > 0f || !FindTarget(out Vector2 targetPoint))
                return;

            Projectile shot = Pooled.Get(_definition.Projectile, _muzzle.position, Quaternion.identity);
            shot.Launch(_team, _definition.AttackDamage, _definition.PushPower, targetPoint, _definition.ProjectileArcHeight, 0f);
            _attackCooldown = _definition.AttackInterval;
        }

        private bool FindTarget(out Vector2 targetPoint)
        {
            Vector2 center = transform.position;
            Physics2D.OverlapCircle(center, _definition.AttackRange, SolidOnly, _overlaps);
            targetPoint = default;
            float best = float.MaxValue;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit unit) || unit.Team == _team || !unit.IsAlive)
                    continue;

                float distance = ((Vector2)col.bounds.center - center).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    targetPoint = col.bounds.center;
                }
            }
            return best < float.MaxValue;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, Unit attacker)
        {
            if (!IsAlive)
                return;

            _hp -= amount;
            DamageNumbers.Damage(this, _team, new Vector2(_collider.bounds.center.x, _collider.bounds.max.y), amount);
            if (!IsAlive)
            {
                Debug.Log($"[Tower] {_team} {name} 파괴");
                gameObject.SetActive(false);
            }
        }
    }
}

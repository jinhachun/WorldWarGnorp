using UnityEngine;

namespace GnorpWar
{
    public class Base : MonoBehaviour, IDamageable
    {
        [SerializeField] private BaseDefinition _definition;
        [SerializeField] private Team _team;

        private static readonly System.Collections.Generic.List<Base> All = new System.Collections.Generic.List<Base>();

        private float _hp;
        private Collider2D _collider;

        public event System.Action<Base> Destroyed;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public float Hp01 => Mathf.Clamp01(_hp / _definition.MaxHp);

        // 새 유닛이 같은 진영 성을 몸으로 통과하게 한다 — 성문 안에서 나와 걸어 나간다. 성 뒤쪽 BackStop과는 그대로 부딪힌다
        public static void IgnoreOwnBase(Team team, Collider2D unitCollider)
        {
            foreach (Base b in All)
                if (b._team == team)
                    Physics2D.IgnoreCollision(unitCollider, b._collider);
        }

        private void Awake()
        {
            _hp = _definition.MaxHp;
            _collider = GetComponent<Collider2D>();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public void TakeDamage(float amount, Vector2 hitDirection, float push, Unit attacker)
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

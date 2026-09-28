using UnityEngine;

namespace GnorpWar
{
    public class Base : MonoBehaviour, IDamageable
    {
        [SerializeField] private BaseDefinition _definition;
        [SerializeField] private Team _team;

        private float _hp;

        public event System.Action<Base> Destroyed;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public float Hp01 => Mathf.Clamp01(_hp / _definition.MaxHp);

        private void Awake()
        {
            _hp = _definition.MaxHp;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, Unit attacker)
        {
            if (!IsAlive)
                return;

            // 기지 앞면에서 튀어나온다
            if (CoinField.Instance != null)
                CoinField.Instance.OnDamaged(_team, transform.position - (Vector3)(hitDirection * 0.75f), Mathf.Min(amount, _hp));
            _hp -= amount;
            if (!IsAlive)
            {
                Debug.Log($"[Base] {_team} 기지 파괴");
                gameObject.SetActive(false);
                Destroyed?.Invoke(this);
            }
        }
    }
}

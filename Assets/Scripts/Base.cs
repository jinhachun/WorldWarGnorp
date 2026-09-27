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

        private void Awake()
        {
            _hp = _definition.MaxHp;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push)
        {
            if (!IsAlive)
                return;

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

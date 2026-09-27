using UnityEngine;

namespace GnorpWar
{
    [CreateAssetMenu(menuName = "GnorpWar/Unit Definition")]
    public class UnitDefinition : ScriptableObject
    {
        [SerializeField] private float _moveSpeed = 1.5f;
        [Tooltip("발바닥이 올라가는 높이(월드 유닛). 키 1짜리 유닛 머리 위에 올라서려면 1보다 조금 커야 한다.")]
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _maxHp = 100f;
        [SerializeField] private float _attackDamage = 10f;
        [SerializeField] private float _attackInterval = 1f;
        [Tooltip("내 중심에서 상대 콜라이더 가장자리까지. 몸 반폭이 0.5라 0.5 이하면 맞닿아도 못 때린다.")]
        [SerializeField] private float _attackRange = 0.8f;
        [Tooltip("맞았을 때 뒤로 밀려나는 속도")]
        [SerializeField] private float _hitKnockback = 2f;
        [SerializeField] private float _cost = 50f;
        [SerializeField] private float _summonCooldown = 1.5f;

        public float MoveSpeed => _moveSpeed;
        public float JumpHeight => _jumpHeight;
        public float MaxHp => _maxHp;
        public float AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float AttackRange => _attackRange;
        public float HitKnockback => _hitKnockback;
        public float Cost => _cost;
        public float SummonCooldown => _summonCooldown;
    }
}

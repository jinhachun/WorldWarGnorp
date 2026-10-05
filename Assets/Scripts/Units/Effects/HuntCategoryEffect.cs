using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛은 사거리 안의 이 병종 적을 먼저 노리고, 그 병종을 공격할 때마다 전투 동안 그 유닛의 공격력에 곱한다 (꼭두각시 왕: 경기병 → 적 지원병 ×1.2)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Hunt Category")]
    public class HuntCategoryEffect : UnitEffect
    {
        [SerializeField] private UnitCategory _prey;
        [SerializeField] private float _factor = 1.2f;

        public override UnitCategory PreferredTargetCategory => _prey;

        public override void OnAttack(Unit attacker, IDamageable target, int stacks)
        {
            if (target is Unit enemy && enemy.Definition.Category == _prey)
                attacker.AddModifier(new StatModifier(UnitStat.Attack, StatOp.Multiply, _factor));
        }
    }
}

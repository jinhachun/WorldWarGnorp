using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛은 사거리 안의 이 병종 적을 먼저 노리고, 그 병종을 공격할 때마다 전투 동안 그 유닛의 공격력에 곱한다 (꼭두각시 왕: 경기병 → 적 지원병 ×1.2)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Hunt Category")]
    public class HuntCategoryEffect : UnitEffect
    {
        [SerializeField] private UnitCategory _prey;
        [SerializeField] private float _factor = 1.2f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _factorPerTier = 0.2f;

        private float Factor(int level) => OwnedBuilding.Scaled(_factor, _factorPerTier, level);

        public override UnitCategory PreferredTargetCategory => _prey;

        public override void OnAttack(Unit attacker, IDamageable target, int stacks)
        {
            if (target is Unit enemy && enemy.Definition.Category == _prey)
                attacker.AddModifier(new StatModifier(UnitStat.Attack, StatOp.Multiply, Factor(Level(attacker.Team))));
        }

        public override string Describe(int level) => string.Format(Description, $"{Factor(level):0.##}");
        public override string Headline(int level) => $"×{Factor(level):0.##}";
    }
}

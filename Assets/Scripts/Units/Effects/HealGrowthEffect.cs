using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 회복받을 때마다 크기·체력·공격력이 커진다 (순례지: 중보병 각 +5%)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Heal Growth")]
    public class HealGrowthEffect : UnitEffect
    {
        [Tooltip("기준 크기 대비 — 0.05 = +5%")]
        [SerializeField] private float _size = 0.05f;
        [Tooltip("합연산 — 0.05 = +5%")]
        [SerializeField] private float _stats = 0.05f;
        [Tooltip("등급이 오를 때마다 크기·체력·공격력에 각각 더한다")]
        [SerializeField] private float _perTier = 0.05f;

        public override void OnHealReceived(Unit unit, float amount, int stacks)
        {
            int level = Level(unit.Team);
            float stats = OwnedBuilding.Scaled(_stats, _perTier, level);
            unit.Grow(OwnedBuilding.Scaled(_size, _perTier, level));
            unit.AddModifier(new StatModifier(UnitStat.MaxHp, StatOp.Percent, stats));
            unit.AddModifier(new StatModifier(UnitStat.Attack, StatOp.Percent, stats));
            BuffLinks.EffectToPoint(unit.Team, this, unit.Position, BuffKind.Stat, BuffLabel.Of(new StatModifier(UnitStat.Attack, StatOp.Percent, stats)));
        }

        // 설명은 크기·스탯을 한 숫자로 쓴다(기획서 "크기, 체력, 공격력 +5%") — 셋이 같은 값일 때만 맞다
        public override string Describe(int level) => string.Format(Description, $"{OwnedBuilding.Scaled(_stats, _perTier, level) * 100f:0.#}");
    }
}

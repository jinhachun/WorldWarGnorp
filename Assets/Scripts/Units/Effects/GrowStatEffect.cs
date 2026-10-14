using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 커질 때마다 그 유닛에게 스탯을 건다 (순례지 신화: 중보병 이동속도 +5%, 공격속도 +5% — 기획서 §7-1).
    // 유닛 하나에 걸리므로 그 유닛이 사는 동안 남는다. 커진 이유는 가리지 않는다(발동 · 회복 · 소환 때 커짐 모두 Unit.Grow)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Grow Stat")]
    public class GrowStatEffect : UnitEffect
    {
        [SerializeField] private StatModifier[] _stats = new StatModifier[0];

        public override void OnGrew(Unit unit, int stacks)
        {
            foreach (StatModifier stat in _stats)
                unit.AddModifier(stat);
            BuffLinks.EffectToPoint(unit.Team, this, unit.Position, BuffKind.Stat);
        }

        // 설명의 {0}에 스탯들을 쉼표로 이어 넣는다(「이동속도 +5%, 공격속도 +5%」)
        public override string Describe(int level)
            => string.Format(Description, string.Join(", ", System.Array.ConvertAll(_stats, stat => stat.Describe())));
    }
}

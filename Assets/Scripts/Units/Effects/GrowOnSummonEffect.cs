using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 소환될 때 크기가 N배가 된다 (방패 공방 신화: 중보병 2배).
    // 지금 크기에 곱한다 — 다른 효과로 이미 커진 유닛도 그 크기의 N배가 된다(상한은 Unit.Grow)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Grow On Summon")]
    public class GrowOnSummonEffect : UnitEffect
    {
        [Tooltip("소환될 때 크기에 곱한다 — 2 = 2배")]
        [SerializeField] private float _factor = 2f;

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            if (_factor > 1f)
            {
                unit.Grow(unit.Size * (_factor - 1f));
                BuffLinks.EffectToPoint(unit.Team, this, unit.Position, BuffKind.Vitality);
            }
        }

        public override string Describe(int level) => string.Format(Description, $"{_factor:0.##}");
    }
}

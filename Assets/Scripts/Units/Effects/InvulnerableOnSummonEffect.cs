using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 소환된 뒤 일정 시간 무적 (왕족친위대 신화: 친위기사 10초).
    // 무적은 Unit.GrantInvulnerable 한 곳만 지난다 — 무적 지속시간을 보정하는 효과가 이 무적에도 걸리게
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable On Summon")]
    public class InvulnerableOnSummonEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 10f;

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            unit.GrantInvulnerable(_seconds);
            BuffLinks.EffectToPoint(unit.Team, this, unit.Position, BuffKind.Invulnerable);
        }

        public override string Describe(int level) => string.Format(Description, $"{_seconds:0.##}");
    }
}

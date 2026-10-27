using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 경보병)이 승급하면 승급한 유닛이 N초간 무적 (기사 서임식 신화 — 2초, 사용자 10/28). 승급은 Promotion.Promote가 알린다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Promotion Invulnerable")]
    public class PromotionInvulnerableEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 2f;

        public override void OnPromoted(Unit promoted, int stacks)
        {
            promoted.GrantInvulnerable(_seconds);
            BuffLinks.EffectToPoint(promoted.Team, this, promoted.Position, BuffKind.Invulnerable);
        }

        public override string Describe(int level) => string.Format(Description, _seconds);
    }
}

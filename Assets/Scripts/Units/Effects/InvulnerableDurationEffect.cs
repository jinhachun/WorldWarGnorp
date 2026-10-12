using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 받는 모든 무적의 지속시간에 N초를 더한다 (충정의 맹세 신화 — 기획서 §7-1).
    // 무적을 주는 길이 Unit.GrantInvulnerable 하나라 그 자리에서 한 번만 더해진다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable Duration")]
    public class InvulnerableDurationEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 2f;

        public override float InvulnerableBonusSeconds => _seconds;

        public override string Describe(int level) => string.Format(Description, $"{_seconds:0.#}");
    }
}

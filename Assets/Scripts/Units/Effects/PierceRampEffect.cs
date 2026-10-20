using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 관통한 화살이 적을 맞힐 때마다 그 화살의 피해 +N%(합연산 — 관통탄 특허국 신화 +100%, 사용자 10/20). 첫 적은 그대로, 그다음 적부터 2배 · 3배 · 4배 …
    // 적용 대상 유닛(궁병)의 화살과 이 진영 타워의 화살 모두 — 관통 자체는 §7 효과(PierceEffect)가 켠다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Pierce Ramp")]
    public class PierceRampEffect : UnitEffect
    {
        [SuffixLabel("%", true)]
        [SerializeField] private float _bonusPercent = 100f;

        public override float PierceHitDamageBonus => _bonusPercent / 100f;

        public override string Describe(int level) => string.Format(Description, $"{_bonusPercent:0.##}");
    }
}

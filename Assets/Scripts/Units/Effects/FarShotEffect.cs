using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 타워들과 적용 대상 유닛이 사거리 끝 절반에 있는 적에게 주는 피해 +N% (감시 망원경 신화 — 내 궁병 +50%, 사용자 10/28 [제안]).
    // 쏘는 순간의 거리로 가린다(Base.Shoot · Unit.Shoot) — 쏘는 자리에서 사거리의 절반보다 멀면
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Far Shot")]
    public class FarShotEffect : UnitEffect
    {
        [SuffixLabel("%", true)]
        [SerializeField] private float _percent = 50f;

        public override float FarShotDamageBonus => _percent / 100f;

        public override string Describe(int level) => string.Format(Description, _percent);
    }
}

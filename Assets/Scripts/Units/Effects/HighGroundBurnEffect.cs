using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 높은 고도에서 피해를 받은 적이 N초간 불타 매초 그 피해의 일정 비율만큼 피해를 받는다 (기름투하대 신화 — 기획서 §7-1). 불은 Unit.Ignite —
    // 다시 맞으면 시간이 새로 시작되고 매초 피해는 큰 쪽이 남는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/High Ground Burn")]
    public class HighGroundBurnEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 3f;
        [Tooltip("매초 피해 = 불을 붙인 피해의 이 비율 — 0.2 = 20%")]
        [SerializeField] private float _ratio = 0.2f;

        public override void OnHighGroundHit(Unit victim, float damage, IDamageable attacker) => victim.Ignite(_seconds, damage * _ratio, attacker);

        // 설명의 {0} = 초 · {1} = 매초 피해 비율(%)
        public override string Describe(int level) => string.Format(Description, $"{_seconds:0.#}", $"{_ratio * 100f:0.#}");
    }
}

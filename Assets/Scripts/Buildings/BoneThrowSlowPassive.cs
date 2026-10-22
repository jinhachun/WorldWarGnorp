using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 뼈 투척 신화 줄 — 효과는 BoneThrowAction이 이 표시를 보고 낸다(뼈에 맞은 적의 이동속도 ×값, 몇 초)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Bone Throw Slow")]
    public class BoneThrowSlowPassive : BuildingPassive
    {
        [Tooltip("이동속도에 곱한다")]
        [SerializeField] private float _factor = 0.5f;
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 2f;

        public float Factor => _factor;
        public float Seconds => _seconds;

        public override string Describe(int level) => $"뼈에 맞은 적은 {_seconds:0.##}초 동안 이동속도 ×{_factor:0.##}";
    }
}

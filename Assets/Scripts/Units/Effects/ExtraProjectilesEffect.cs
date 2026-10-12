using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 공격 한 번에 화살을 여러 발 잇달아 쏜다 (궁술수련관 신화: 궁수 3발 — 기획서 §7-1, 사용자 10/13).
    // 발마다 다른 적을 노리고, 노릴 적이 모자라면 같은 적에게 쏜다. 연발은 Unit.Burst. 신화는 등급이 하나뿐이라 수를 등급으로 올리지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Extra Projectiles")]
    public class ExtraProjectilesEffect : UnitEffect
    {
        [Tooltip("첫 발 말고 더 쏘는 수 — 2면 모두 3발")]
        [SuffixLabel("발", true)]
        [SerializeField] private int _extra = 2;
        [Tooltip("발 사이 간격 — 다음 공격 전에 다 나가도록 공격 간격을 발 수로 나눈 값보다 길면 그 값으로 줄어든다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _interval = 0.15f;

        public override int ExtraProjectiles => _extra;
        public override float ExtraProjectileInterval => _interval;

        // 글의 숫자는 모두 쏘는 수(첫 발 포함)
        public override string Describe(int level) => string.Format(Description, _extra + 1);
    }
}

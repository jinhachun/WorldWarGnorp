using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛의 화살에 맞은 적은 잠시 이동속도 ×N (합치기 보상 업그레이드 「사냥꾼의 활」) — N은 업그레이드의 값(여럿이면 가장 센 것 하나)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Arrow Slow")]
    public class UpgradeArrowSlowEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 2f;

        public override void OnArrowHit(Team team, Unit victim) => victim.Slow(Value(team), _seconds);
    }
}

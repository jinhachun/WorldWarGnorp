using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 중보병)이 적과 맞붙어 있는 동안, 바로 뒤 아군이 받는 피해 ×N (국가 전용 업그레이드 「방패 대열」 — 킹덤) — N은 업그레이드의 값(여럿이면 가장 센 것 하나).
    // 「바로 뒤」 = 그 아군의 앞에 맞닿은 아군(Unit.AllyInFront)이 이 효과의 대상이고 맞붙어 있다(Unit.Engaged). 한 아군은 한 번만 줄어든다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Shield Wall")]
    public class UpgradeShieldWallEffect : UnitEffect
    {
        public override float DamageTakenFactor(Unit victim)
        {
            Unit shield = victim.AllyInFront;
            return shield != null && shield.IsAlive && shield.Engaged && this.AppliesFor(victim.Team, shield.Definition) ? Value(victim.Team) : 1f;
        }
    }
}

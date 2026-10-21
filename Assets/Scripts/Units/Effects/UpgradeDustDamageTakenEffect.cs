using UnityEngine;

namespace GnorpWar
{
    // 뼛가루 언덕 위에 있는 적용 대상 유닛이 받는 피해 ×N (국가 전용 업그레이드 「뼈 갑옷」 — 트라이브) — N은 업그레이드의 값(여럿이면 가장 센 것 하나).
    // 「언덕 위」 판정은 전쟁 북소리와 같다(Unit.OnBoneDust)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Dust Damage Taken")]
    public class UpgradeDustDamageTakenEffect : UnitEffect
    {
        public override float DamageTakenFactor(Unit victim)
            => victim.OnBoneDust && this.AppliesFor(victim.Team, victim.Definition) ? Value(victim.Team) : 1f;
    }
}

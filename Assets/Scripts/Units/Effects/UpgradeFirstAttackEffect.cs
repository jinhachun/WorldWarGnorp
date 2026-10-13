using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛의 첫 공격은 피해 +N% (합치기 보상 업그레이드 「돌격 나팔」) — N은 업그레이드의 합쳐진 값
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade First Attack")]
    public class UpgradeFirstAttackEffect : UnitEffect
    {
        public override float FirstAttackDamageBonus(Team team) => Value(team) / 100f;
    }
}

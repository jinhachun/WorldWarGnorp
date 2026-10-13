using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 지금 공격한 적보다 높은 고도에 있으면 공격속도 +N% (합치기 보상 업그레이드 「고지 점령」) — N은 업그레이드의 합쳐진 값
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade High Ground Haste")]
    public class UpgradeHighGroundHasteEffect : UnitEffect
    {
        public override float HighGroundAttackSpeedBonus(Team team) => Value(team) / 100f;
    }
}

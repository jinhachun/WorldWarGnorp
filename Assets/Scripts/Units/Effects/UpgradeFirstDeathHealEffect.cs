using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 처음 죽을 때, 죽는 대신 최대 체력의 N%를 회복한다 (지역 특수 업그레이드 「신정」) — N은 업그레이드의 값
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade First Death Heal")]
    public class UpgradeFirstDeathHealEffect : UnitEffect
    {
        public override float FirstDeathHealRatio(Team team) => Value(team) / 100f;
    }
}

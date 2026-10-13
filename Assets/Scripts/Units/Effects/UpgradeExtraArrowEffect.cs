using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다 N% 확률로 화살 1발을 더 쏜다 (합치기 보상 업그레이드 「연사」) — N은 업그레이드의 합쳐진 값(100까지)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Extra Arrow")]
    public class UpgradeExtraArrowEffect : UnitEffect
    {
        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            if (Random.value * 100f < Value(killer.Team))
                killer.ShootExtra();
        }
    }
}

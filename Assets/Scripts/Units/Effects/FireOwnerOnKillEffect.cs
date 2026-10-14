using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다 이 효과를 켠 기물을 즉시 1회 발동시킨다 (마구간 신화 — 기획서 §7-1).
    // 발동은 쿨다운 발동과 같은 길이다(게이지는 그대로) — 그래서 소환된 기사가 또 처치하면 다시 돈다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Fire Owner On Kill")]
    public class FireOwnerOnKillEffect : UnitEffect
    {
        public override void OnKill(Unit killer, Unit victim, int stacks) => BoardRunner.For(killer.Team).FireOwners(this, killer.Position);
    }
}

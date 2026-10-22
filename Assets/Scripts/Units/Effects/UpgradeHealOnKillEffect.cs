using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다 최대 체력의 N%를 회복한다 (합치기 보상 업그레이드 「약탈」) — N은 업그레이드의 합쳐진 값
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Heal On Kill")]
    public class UpgradeHealOnKillEffect : UnitEffect
    {
        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            killer.Heal(killer.MaxHp * Value(killer.Team) / 100f);
            BuffLinks.EffectHeal(killer.Team, this, null, killer);   // 켠 기물이 없다 — 본진에서 회복 오브
        }
    }
}

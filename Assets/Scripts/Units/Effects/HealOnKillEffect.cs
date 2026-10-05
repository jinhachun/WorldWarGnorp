using UnityEngine;

namespace GnorpWar
{
    // 적을 처치한 유닛이 최대 체력의 일정 비율을 회복한다 (까마귀의사)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Heal On Kill")]
    public class HealOnKillEffect : UnitEffect
    {
        [Tooltip("최대 체력 대비 — 0.3 = 30%")]
        [SerializeField] private float _ratio = 0.3f;

        public override void OnKill(Unit killer, Unit victim, int stacks) => killer.Heal(killer.MaxHp * _ratio);
    }
}

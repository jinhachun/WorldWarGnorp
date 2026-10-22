using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 아군을 처치할 때마다, 자신의 최대 체력의 N%를 회복한다 (부족의 최종병기 신화 — 툰드라 맘모스)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Heal On Ally Kill")]
    public class HealOnAllyKillEffect : UnitEffect
    {
        [Tooltip("최대 체력 대비 — 0.1 = 10%")]
        [SerializeField] private float _ratio = 0.1f;

        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            if (victim.Team != killer.Team)
                return;
            killer.Heal(killer.MaxHp * _ratio);
            BuffLinks.EffectHeal(killer.Team, this, null, killer);
        }

        public override string Describe(int level) => string.Format(Description, $"{_ratio * 100f:0.#}");
    }
}

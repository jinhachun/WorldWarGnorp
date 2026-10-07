using UnityEngine;

namespace GnorpWar
{
    // 적을 처치한 유닛이 최대 체력의 일정 비율을 회복한다 (까마귀의사)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Heal On Kill")]
    public class HealOnKillEffect : UnitEffect
    {
        [Tooltip("최대 체력 대비 — 0.3 = 30%")]
        [SerializeField] private float _ratio = 0.3f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _ratioPerTier = 0.15f;

        private float Ratio(int level) => OwnedBuilding.Scaled(_ratio, _ratioPerTier, level);

        public override void OnKill(Unit killer, Unit victim, int stacks) => killer.Heal(killer.MaxHp * Ratio(Level(killer.Team)));

        public override string Describe(int level) => string.Format(Description, $"{Ratio(level) * 100f:0.#}");
        public override string Headline(int level) => $"{Ratio(level) * 100f:0.#}%";
    }
}

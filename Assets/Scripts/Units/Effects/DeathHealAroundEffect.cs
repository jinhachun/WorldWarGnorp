using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽으면 죽은 자리 주변의 아군이 각자 최대 체력의 비율만큼 회복한다 (성당기사단 신화: 방패수 → 주변 30%)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Heal Around")]
    public class DeathHealAroundEffect : UnitEffect
    {
        [SerializeField] private float _radius = 3f;
        [Tooltip("회복받는 아군의 최대 체력 대비 — 0.3 = 30%")]
        [SerializeField] private float _ratio = 0.3f;

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            foreach (Unit ally in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (ally.IsAlive && ally.Team == unit.Team && (ally.Position - at).sqrMagnitude <= _radius * _radius)
                    ally.Heal(ally.MaxHp * _ratio);
            BuffLinks.EffectFromUnitToUnits(unit.Team, this, at, unit.Team, u => (u.Position - at).sqrMagnitude <= _radius * _radius, BuffKind.Stat);
        }

        public override string Describe(int level) => string.Format(Description, $"{_ratio * 100f:0.#}");
    }
}

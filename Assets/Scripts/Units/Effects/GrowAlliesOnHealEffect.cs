using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 회복받을 때, 그 유닛의 크기가 기준 이상이면 주변 아군(자기 포함)의 크기도 커진다 (크루세이더 길드 신화)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Grow Allies On Heal")]
    public class GrowAlliesOnHealEffect : UnitEffect
    {
        [Tooltip("이 크기 이상인 유닛이 회복받을 때만 — 2 = 2배")]
        [SerializeField] private float _minSize = 2f;
        [SerializeField] private float _radius = 3f;
        [Tooltip("기준 크기 대비 — 0.05 = +5%")]
        [SerializeField] private float _grow = 0.05f;

        public override void OnHealReceived(Unit unit, float amount, int stacks)
        {
            if (unit.Size < _minSize)
                return;
            foreach (Unit ally in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (ally.IsAlive && ally.Team == unit.Team && (ally.Position - unit.Position).sqrMagnitude <= _radius * _radius)
                    ally.Grow(_grow);
            BuffLinks.EffectToPoint(unit.Team, this, unit.Position, BuffKind.Vitality);
        }

        public override string Describe(int level) => string.Format(Description, $"{_minSize:0.##}");
    }
}

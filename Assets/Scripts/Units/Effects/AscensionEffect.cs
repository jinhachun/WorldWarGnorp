using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(사제)이 N기 죽을 때마다, 그 순간 전장의 모든 아군이 지금 체력의 일정 비율을 잃고 공격력 +%(합연산) (승천의 전당).
    // 죽은 수는 이 효과의 스택으로 센다(기물이 쿨다운이 없어 스택이 따로 오르지 않는다 — 전투마다 0부터)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Ascension")]
    public class AscensionEffect : UnitEffect
    {
        [SerializeField] private int _every = 3;
        [Tooltip("지금 체력 대비 — 0.5 = 50%")]
        [SerializeField] private float _hpLoss = 0.5f;
        [Tooltip("합연산 — 1 = +100%")]
        [SerializeField] private float _attack = 1f;

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            TeamEffects.AddStack(unit.Team, this);
            if ((stacks + 1) % _every != 0)
                return;
            foreach (Unit ally in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (ally.IsAlive && ally.Team == unit.Team)
                {
                    ally.LoseHpRatio(_hpLoss);
                    ally.AddModifier(new StatModifier(UnitStat.Attack, StatOp.Percent, _attack));
                }
        }
    }
}

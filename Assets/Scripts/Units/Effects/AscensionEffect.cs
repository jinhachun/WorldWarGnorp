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

        // 다음 발동까지 남은 사제 수(진영별, 툴팁 표시용) — 전투 밖에서는 처음 값. 기물에서 찾을 땐 그 기물의 Effects에서 이 타입을 고른다
        public int RemainingOf(Team team)
        {
            if (!BattleManager.Fighting)
                return _every;
            foreach (TeamEffects.Entry e in TeamEffects.For(team))
                if (e.Effect == this)
                    return _every - e.Stacks % _every;
            return _every;
        }

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            TeamEffects.AddStack(unit.Team, this);
            if ((stacks + 1) % _every != 0)
                return;
            // 신화 — 이 진영에 AscensionKeepHpEffect가 켜져 있으면 체력만 잃지 않는다(공격력은 그대로 오른다).
            // 전설까지는 그 효과가 켜질 길이 없어 아래가 그대로 돈다
            bool keepHp = false;
            foreach (TeamEffects.Entry e in TeamEffects.For(unit.Team))
                keepHp = keepHp || e.Effect is AscensionKeepHpEffect;
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit ally in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (ally.IsAlive && ally.Team == unit.Team)
                {
                    if (!keepHp)
                        ally.LoseHpRatio(_hpLoss);
                    ally.AddModifier(new StatModifier(UnitStat.Attack, StatOp.Percent, _attack));
                    sum += ally.Position;
                    count++;
                }
            if (count > 0)
                BuffLinks.EffectFromUnit(unit.Team, this, at, sum / count, BuffKind.Stat, BuffLabel.Of(new StatModifier(UnitStat.Attack, StatOp.Percent, _attack)));
        }
    }
}

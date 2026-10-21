using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 유닛)이 N기 죽을 때마다, 전투 동안 내 유닛 전부(나중에 나오는 유닛도)의 스탯을 올린다 (국가 전용 업그레이드 「조상의 분노」 — 트라이브: 공격력 +1%).
    // N은 얻은 것마다 따로(UnitEffect.Values). 죽은 수는 이 효과의 스택(전투마다 0부터). 다시 일어난 유닛이 또 죽으면 또 센다. 진영 스탯(StatBook)에 건다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Death Count Stat")]
    public class UpgradeDeathCountStatEffect : UnitEffect
    {
        [SerializeField] private StatModifier _stat = new StatModifier(UnitStat.Attack, StatOp.Percent, 0.01f);

        public override object[] UpgradeNumbers() => new object[] { _stat.ValueText };

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            TeamEffects.AddStack(unit.Team, this);
            int deaths = stacks + 1;
            foreach (float every in Values(unit.Team))
                if (every >= 1f && deaths % Mathf.RoundToInt(every) == 0)
                {
                    StatBook.AddForBattle(unit.Team, this, _stat);
                    // 켠 기물이 없는 업그레이드가 터진 순간 — 본진에서 내 무리 가운데로 스탯 선 + 글자(ARCHITECTURE 「버프 선」)
                    if (Centroid(unit.Team, out Vector2 center))
                        BuffLinks.FromBase(unit.Team, center, BuffKind.Stat, BuffLabel.Of(_stat));
                }
        }

        private static bool Centroid(Team team, out Vector2 center)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit ally in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (ally.IsAlive && ally.Team == team)
                {
                    sum += ally.Position;
                    count++;
                }
            center = count > 0 ? sum / count : Vector2.zero;
            return count > 0;
        }
    }
}

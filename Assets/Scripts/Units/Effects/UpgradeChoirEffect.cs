using UnityEngine;

namespace GnorpWar
{
    // 전장에 있는 「세는 병종」 유닛(내 지원병) 1기마다, 내 유닛 전부의 공격속도 +N%(합연산) (국가 전용 업그레이드 「성가대」 — 킹덤) — N은 업그레이드의 합쳐진 값.
    // 지원병 수는 지금 전장의 수다 — 지원병이 나오고 죽을 때, 그리고 진영마다 RecountSeconds마다 다시 센다.
    // 「병종」 칸은 비워 내 유닛 누구의 스텝에서든 다시 센다 — 마지막 건축가가 성으로 조용히 사라져도(죽음 아님) 늦지 않게.
    // 진영 스탯(StatBook)에 「지금 수 − 지난번 수」만큼 더한다. 지난번 수는 이 효과의 스택(전투마다 0부터)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Choir")]
    public class UpgradeChoirEffect : UnitEffect
    {
        private const float RecountSeconds = 1f;

        [Tooltip("세는 병종 — 전장에 있는 이 병종의 내 유닛 수(지원병)")]
        [SerializeField] private UnitCategory[] _counted = new UnitCategory[0];

        // 받는 쪽 — 내 유닛 전부
        private class Everyone : IUnitFilter
        {
            public bool AppliesTo(UnitDefinition unit) => true;
        }

        // 세는 쪽 — 「세는 병종」(병종 취급 — 군제 개혁도 읽게 AppliesFor로 거른다)
        private class Counted : IUnitFilter, ICategoryFilter
        {
            public UnitCategory[] Categories;
            public bool AppliesTo(UnitDefinition unit) => System.Array.IndexOf(Categories, unit.Category) >= 0;
            public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(Categories, category) >= 0;
        }

        private static readonly Everyone All = new Everyone();
        private static readonly float[] NextRecount = new float[2];
        private Counted _filter;

        private Counted Filter => _filter != null && _filter.Categories == _counted ? _filter : _filter = new Counted { Categories = _counted };

        public override void OnEnabled(Team team) => NextRecount[(int)team] = 0f;

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            if (Filter.AppliesFor(unit.Team, unit.Definition))
                Recount(unit.Team);
        }

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            if (Filter.AppliesFor(unit.Team, unit.Definition))
                Recount(unit.Team);
        }

        public override void Tick(Unit unit, int stacks)
        {
            if (Time.time >= NextRecount[(int)unit.Team])
                Recount(unit.Team);
        }

        private void Recount(Team team)
        {
            NextRecount[(int)team] = Time.time + RecountSeconds;
            int count = 0;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == team && Filter.AppliesFor(team, unit.Definition))
                    count++;
            foreach (TeamEffects.Entry e in TeamEffects.For(team))
                if (e.Effect == this)
                {
                    if (count == e.Stacks)
                        return;
                    StatBook.AddForBattle(team, All, new StatModifier(UnitStat.AttackSpeed, StatOp.Percent, (count - e.Stacks) * e.Value / 100f));
                    e.Stacks = count;
                    return;
                }
        }
    }
}

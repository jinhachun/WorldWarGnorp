using System.Collections.Generic;

namespace GnorpWar
{
    // 이번 전투에 진영별로 켜진 유닛 효과와 그 스택. 전투 시작 때 비운다(BoardRunner)
    public static class TeamEffects
    {
        public class Entry
        {
            public UnitEffect Effect;
            public int Stacks;
            // 켜 준 기물의 등급(OwnedBuilding.Level) — 여럿이면 가장 높은 것
            public int Level = 1;
            // 켜 준 기물 — 이 효과가 소환한 유닛의 출처(Unit.SummonedBy)가 된다. 같은 기물 여럿이 켜도 종류는 하나다
            public BuildingDefinition Source;
        }

        private static readonly List<Entry>[] ByTeam = { new List<Entry>(), new List<Entry>() };

        public static List<Entry> For(Team team) => ByTeam[(int)team];

        public static void Clear()
        {
            foreach (List<Entry> entries in ByTeam)
                entries.Clear();
        }

        // 켠다 — 이미 켜져 있으면 등급만 높은 쪽으로
        public static Entry Enable(Team team, UnitEffect effect, int level, BuildingDefinition source = null)
        {
            foreach (Entry entry in For(team))
                if (entry.Effect == effect)
                {
                    entry.Level = System.Math.Max(entry.Level, level);
                    return entry;
                }
            var added = new Entry { Effect = effect, Level = level, Source = source };
            For(team).Add(added);
            return added;
        }

        public static void AddStack(Team team, UnitEffect effect) => Enable(team, effect, 1).Stacks++;

        public static BuildingDefinition SourceOf(Team team, UnitEffect effect)
        {
            foreach (Entry entry in For(team))
                if (entry.Effect == effect)
                    return entry.Source;
            return null;
        }

        public static int LevelOf(Team team, UnitEffect effect)
        {
            foreach (Entry entry in For(team))
                if (entry.Effect == effect)
                    return entry.Level;
            return 1;
        }
    }
}

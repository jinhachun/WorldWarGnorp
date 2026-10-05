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
        }

        private static readonly List<Entry>[] ByTeam = { new List<Entry>(), new List<Entry>() };

        public static List<Entry> For(Team team) => ByTeam[(int)team];

        public static void Clear()
        {
            foreach (List<Entry> entries in ByTeam)
                entries.Clear();
        }

        // 켠다 — 이미 켜져 있으면 그대로
        public static Entry Enable(Team team, UnitEffect effect)
        {
            foreach (Entry entry in For(team))
                if (entry.Effect == effect)
                    return entry;
            var added = new Entry { Effect = effect };
            For(team).Add(added);
            return added;
        }

        public static void AddStack(Team team, UnitEffect effect) => Enable(team, effect).Stacks++;
    }
}

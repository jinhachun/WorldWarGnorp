using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 「공격력이 가장 높은 내 유닛」(트라이브 명세 §2-1) — 전장에 살아 있는 그 진영 유닛을 지금 공격력(강화 포함)이 높은 순으로. 같으면 무작위.
    // 타격마다 묻는 효과(원시 의식 제단)가 있어 진영마다 한 프레임에 한 번만 센다 — 그 프레임 안에서 죽은 유닛은 읽을 때 거른다
    public static class Strongest
    {
        private static readonly List<Unit>[] Ranked = { new List<Unit>(), new List<Unit>() };
        private static readonly int[] Frame = { -1, -1 };
        private static readonly Dictionary<Unit, float> Tie = new Dictionary<Unit, float>();

        // rank번째(0 = 가장 높은)로 공격력이 높은 살아 있는 유닛 — exclude(죽는 중인 자기 자신)는 뺀다. 없으면 null
        public static Unit Of(Team team, int rank = 0, Unit exclude = null)
        {
            List<Unit> ranked = Ranked[(int)team];
            if (Frame[(int)team] != Time.frameCount)
            {
                Frame[(int)team] = Time.frameCount;
                ranked.Clear();
                Tie.Clear();
                foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                    if (unit.IsAlive && unit.Team == team)
                    {
                        ranked.Add(unit);
                        Tie[unit] = Random.value;
                    }
                ranked.Sort((a, b) => a.AttackDamage != b.AttackDamage ? b.AttackDamage.CompareTo(a.AttackDamage) : Tie[a].CompareTo(Tie[b]));
            }
            foreach (Unit unit in ranked)
                if (unit.IsAlive && unit != exclude && rank-- == 0)
                    return unit;
            return null;
        }
    }
}

using System;
using System.Collections.Generic;

namespace GnorpWar
{
    // 판도 — 지역마다 주인, 영주 목록, 턴. 규칙은 WorldTurn이 이 값을 바꾼다. UnityEngine을 쓰지 않는다
    public class WorldState
    {
        public readonly WorldGraph Graph;
        public readonly WorldRules Rules;
        // [지역] = 주인 영주의 번호
        public readonly int[] Owner;
        // [번호] = 영주 — 죽은 영주도 남는다(Alive = false)
        public readonly List<Lord> Lords = new List<Lord>();
        public readonly Lord Player;
        // 최후의 연합이 뭉쳤으면 그 영주(바탕이 된 영주 — 남은 AI 영주는 이 하나뿐이다), 아니면 null
        public Lord Coalition;
        // 연합이 뭉칠 때의 땅 수 · 내가 연합을 이긴 횟수
        public int CoalitionStartLands;
        public int CoalitionWins;
        // 1부터
        public int Turn = 1;

        // 영주가 지역마다 하나씩 — playerRegion의 영주 자리에 플레이어가 들어간다
        public WorldState(WorldGraph graph, WorldRules rules, int playerRegion, Random random)
        {
            Graph = graph;
            Rules = rules;
            Owner = new int[graph.RegionCount];
            for (int region = 0; region < graph.RegionCount; region++)
            {
                Lord lord = AddLord(region, random);
                if (region == playerRegion)
                {
                    lord.IsPlayer = true;
                    Player = lord;
                }
            }
        }

        // region에 새 영주를 세운다(런 시작 · 반란 뒤) — 빌드 방향은 그 땅의 것, 성향은 새로 뽑는다
        public Lord AddLord(int region, Random random)
        {
            var lord = new Lord
            {
                Id = Lords.Count,
                Build = Graph.Build[region],
                Temper = Rules.RollTemper(random),
                Happiness = Rules.HappinessStart,
            };
            Lords.Add(lord);
            Owner[region] = lord.Id;
            return lord;
        }

        public Lord OwnerOf(int region) => Lords[Owner[region]];

        public int LandsOf(Lord lord)
        {
            int count = 0;
            foreach (int owner in Owner)
                if (owner == lord.Id)
                    count++;
            return count;
        }

        // lord의 땅과 길로 이어진 다른 영주들(겹치지 않게)
        public List<Lord> Neighbors(Lord lord)
        {
            var neighbors = new List<Lord>();
            for (int region = 0; region < Owner.Length; region++)
            {
                if (Owner[region] != lord.Id)
                    continue;
                foreach (int next in Graph.Links[region])
                {
                    Lord other = OwnerOf(next);
                    if (other != lord && !neighbors.Contains(other))
                        neighbors.Add(other);
                }
            }
            return neighbors;
        }

        // lord가 가진 땅 중 그 업그레이드(WorldGraph.Upgrade의 번호)가 붙은 땅의 수
        public int UpgradeCount(Lord lord, int upgrade)
        {
            int count = 0;
            for (int region = 0; region < Owner.Length; region++)
                if (Owner[region] == lord.Id && Graph.Upgrade[region] == upgrade)
                    count++;
            return count;
        }

        // 살아 있는 AI 영주 수
        public int AiLords
        {
            get
            {
                int count = 0;
                foreach (Lord lord in Lords)
                    if (lord.Alive && !lord.IsPlayer)
                        count++;
                return count;
            }
        }

        // 연합전을 한 번 이길 때 넘어오는 몫 = 뭉칠 때 땅 수 ÷ 필요한 승리 수(올림) — 땅 14개 · 세 번이면 5 · 5 · 나머지 4
        private int CoalitionShare => Math.Max(1, (CoalitionStartLands + Rules.CoalitionWins - 1) / Math.Max(1, Rules.CoalitionWins));

        // 지금 연합을 이기면 넘어오는 땅 수 — 마지막 승리면 남은 땅 전부, 남은 땅이 몫보다 적어도 전부
        public int CoalitionTake
            => CoalitionWins + 1 >= Rules.CoalitionWins ? LandsOf(Coalition) : Math.Min(LandsOf(Coalition), CoalitionShare);

        // 연합을 다 먹기까지 남은 승리 수
        public int CoalitionWinsLeft
            => Math.Max(1, Math.Min(Rules.CoalitionWins - CoalitionWins, (LandsOf(Coalition) + CoalitionShare - 1) / CoalitionShare));

        public bool Unified => LandsOf(Player) == Owner.Length;
        public bool GameOver => !Player.Alive;
    }
}

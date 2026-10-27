using System;
using System.Collections.Generic;

namespace GnorpWar
{
    // 판도 — 지역마다 주인, 영주 목록, 턴. 규칙은 WorldTurn이 이 값을 바꾼다. UnityEngine을 쓰지 않는다.
    // 이벤트 땅(Graph.IsEvent — 아웃게임기획 §3-1)은 판도 밖이다: 주인이 없고(NoOwner) 통일 · 연합 · 수입에 세지 않는다. 그 땅의 종류 · 닫힘도 여기 든다
    public class WorldState
    {
        public const int NoOwner = -1;

        public readonly WorldGraph Graph;
        public readonly WorldRules Rules;
        // [지역] = 주인 영주의 번호(이벤트 땅은 NoOwner)
        public readonly int[] Owner;
        // [이벤트 땅] = 종류 · 한 번 방문해 닫혔나(영주 땅의 칸은 쓰지 않는다)
        public readonly EventLandKind[] EventKind;
        public readonly bool[] EventClosed;
        // [번호] = 영주 — 죽은 영주도 남는다(Alive = false)
        public readonly List<Lord> Lords = new List<Lord>();
        public readonly Lord Player;
        // 연합 하나(기획 §6-1 · 사용자 10/27) — 바탕 영주(유령이면 그 유령) 아래 남은 AI 영주들이 뭉친 것
        public class Coalition
        {
            public Lord Lord;
            // 바탕이 유령인가(유령 연합 — 탑은 그 유령의 스냅샷 그대로)
            public bool Ghost;
            // 뭉칠 때의 땅 수 · 다 먹으려면 이겨야 하는 횟수 · 내가 이긴 횟수
            public int StartLands;
            public int WinsNeeded;
            public int Wins;
            // 흡수된 영주들의 번호(바탕 제외) — 지도의 그림(UI/WorldLords) · 연합 결성 팝업이 쓴다
            public int[] Members;
        }

        // 뭉친 연합들(런에 한 번 뭉친다 — 멸망한 연합도 남는다: Lord.Alive = false). 뭉친 뒤 남은 AI 영주는 이 바탕 영주들뿐이다
        public readonly List<Coalition> Coalitions = new List<Coalition>();

        // lord가 바탕인 연합(아니면 null)
        public Coalition CoalitionOf(Lord lord) => lord == null ? null : Coalitions.Find(c => c.Lord == lord);
        // 1부터
        public int Turn = 1;
        // 플레이어의 목숨(AI 영주는 Lord.Happiness) — 0이면 국가 위기: 그 뒤 한 번이라도 지면 게임 오버
        public int Lives;
        public bool InCrisis => Lives <= 0 && Player.Alive;

        // 영주가 영주 땅마다 하나씩 — playerRegion(영주 땅)의 영주 자리에 플레이어가 들어간다. 이벤트 땅에는 종류를 섞어 놓는다(EventLands.Deal)
        public WorldState(WorldGraph graph, WorldRules rules, int playerRegion, Random random)
        {
            Graph = graph;
            Rules = rules;
            Lives = rules.LivesStart;
            Owner = new int[graph.RegionCount];
            EventKind = new EventLandKind[graph.RegionCount];
            EventClosed = new bool[graph.RegionCount];
            var events = new List<int>();
            for (int region = 0; region < graph.RegionCount; region++)
            {
                if (graph.IsEvent[region])
                {
                    Owner[region] = NoOwner;
                    events.Add(region);
                    continue;
                }
                Lord lord = AddLord(region, random);
                if (region == playerRegion)
                {
                    lord.IsPlayer = true;
                    Player = lord;
                }
            }
            EventLandKind[] kinds = EventLands.Deal(events.Count, random);
            for (int i = 0; i < events.Count; i++)
                EventKind[events[i]] = kinds[i];
        }

        // region에 새 영주를 세운다(런 시작 · 반란 뒤) — 빌드 방향은 그 땅의 방향들 중 하나(둘 이상이면 고르게 뽑는다), 성향은 새로 뽑는다
        public Lord AddLord(int region, Random random)
        {
            int[] builds = Graph.Builds[region];
            var lord = new Lord
            {
                Id = Lords.Count,
                Build = builds.Length == 1 ? builds[0] : builds[random.Next(builds.Length)],
                Temper = Rules.RollTemper(random),
                Happiness = Rules.HappinessStart,
            };
            Lords.Add(lord);
            Owner[region] = lord.Id;
            return lord;
        }

        // 이벤트 땅이면 null
        public Lord OwnerOf(int region) => Owner[region] == NoOwner ? null : Lords[Owner[region]];

        // lord가 방문할 수 있는 이벤트 땅 = lord의 땅과 국경을 맞댄, 아직 안 닫힌 이벤트 땅(지도 번호 순). 방문은 플레이어만 한다(AI는 이벤트 땅을 무시한다)
        public List<int> VisitableEvents(Lord lord)
        {
            var events = new List<int>();
            for (int region = 0; region < Owner.Length; region++)
                if (Graph.IsEvent[region] && !EventClosed[region] && Array.Exists(Graph.EventBorders[region], land => Owner[land] == lord.Id))
                    events.Add(region);
            return events;
        }

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

        // 연합전을 한 번 이길 때 넘어오는 몫 = 뭉칠 때 땅 수 ÷ 그 연합의 이기는 횟수(올림) — 땅 14개 · 세 번이면 5 · 5 · 나머지 4
        private static int CoalitionShare(Coalition c) => Math.Max(1, (c.StartLands + c.WinsNeeded - 1) / Math.Max(1, c.WinsNeeded));

        // 지금 그 연합을 이기면 넘어오는 땅 수 — 마지막 승리면 남은 땅 전부, 남은 땅이 몫보다 적어도 전부
        public int CoalitionTake(Coalition c)
            => c.Wins + 1 >= c.WinsNeeded ? LandsOf(c.Lord) : Math.Min(LandsOf(c.Lord), CoalitionShare(c));

        // 그 연합을 다 먹기까지 남은 승리 수
        public int CoalitionWinsLeft(Coalition c)
            => Math.Max(1, Math.Min(c.WinsNeeded - c.Wins, (LandsOf(c.Lord) + CoalitionShare(c) - 1) / CoalitionShare(c)));

        public bool Unified => LandsOf(Player) == Graph.LordRegionCount;
        public bool GameOver => !Player.Alive;
    }
}

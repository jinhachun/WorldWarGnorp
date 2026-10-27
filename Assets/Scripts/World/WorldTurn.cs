using System;
using System.Collections.Generic;

namespace GnorpWar
{
    // 월드맵 한 턴의 규칙(기획 §2 · §5 · §6) — 판도(WorldState)만 바꾼다. 탑·상점·실제 전투는 모른다:
    // 영주의 힘은 power로 바깥에서 받고(탑 전투력), 플레이어의 승패는 부르는 쪽이 정해서 Invade에 넘긴다.
    // 한 턴 = (상점 — 바깥) → 플레이어의 Invade 또는 Visit(이벤트 땅 · 쉬면 둘 다 안 부른다) → AiPhase → Settle → Turn + 1.
    // UnityEngine을 쓰지 않고 난수도 받아 쓴다 — Unity 밖에서 같은 씨앗으로 수백 판 돌려 볼 수 있게
    public static class WorldTurn
    {
        // 승률 = 내 힘² ÷ (내 힘² + 상대 힘²)
        public static double WinChance(float mine, float theirs)
        {
            double a = (double)mine * mine, b = (double)theirs * theirs;
            return a + b > 0.0 ? a / (a + b) : 0.5;
        }

        // 성향대로 고른 침략 대상 — 후보는 이어진 다른 AI 영주뿐(플레이어의 땅은 공격당하지 않는다). 쉬면 null. 동점은 무작위.
        // 연합은 침략하지 않는다 — 연합이 여럿이어도 서로 싸우지 않고 나만 기다린다(그래서 연합은 행복도가 깎여 쪼개질 일도 없다)
        public static Lord PickTarget(WorldState state, Lord lord, Func<Lord, float> power, Random random)
        {
            if (lord.Temper == LordTemper.Cautious || state.CoalitionOf(lord) != null)
                return null;
            List<Lord> candidates = state.Neighbors(lord);
            candidates.RemoveAll(other => other.IsPlayer);
            if (candidates.Count == 0)
                return null;
            switch (lord.Temper)
            {
                case LordTemper.Raider:
                    return Best(candidates, other => -power(other), random);
                case LordTemper.Ambitious:
                    return Best(candidates, other => state.LandsOf(other), random);
                default:
                    return candidates[random.Next(candidates.Count)];
            }
        }

        private static Lord Best(List<Lord> candidates, Func<Lord, float> score, Random random)
        {
            Lord best = null;
            float bestScore = 0f;
            int ties = 0;
            foreach (Lord candidate in candidates)
            {
                float value = score(candidate);
                if (best == null || value > bestScore)
                {
                    best = candidate;
                    bestScore = value;
                    ties = 1;
                }
                else if (value == bestScore && random.Next(++ties) == 0)
                    best = candidate;
            }
            return best;
        }

        // region 땅을 침략한 결과(기획 §7-2) — 상대는 그 땅의 영주다. 이기면 땅이 넘어오고(넘어온 땅들을 돌려준다) AI 영주면 행복도가 오른다:
        //   AI 영주가 이기면 그 땅 하나 · 내가 이기면 그 영주의 땅 전부 · 내가 연합을 이기면 몫만큼(WorldState.CoalitionTake — 침략한 땅부터 가까운 순).
        // 땅이 0개가 된 영주는 사라진다. 지면 AI 영주는 상대의 땅 수만큼 행복도가, 플레이어는 목숨 1이 떨어진다(막아 낸 쪽은 그대로) — 빈 목록.
        // 행복도가 바닥난 AI 영주의 멸망은 여기가 아니라 Settle에서 · 플레이어의 목숨은 이겨도 그대로다
        public static List<int> Invade(WorldState state, Lord attacker, int region, bool won)
        {
            WorldRules rules = state.Rules;
            Lord target = state.OwnerOf(region);
            var taken = new List<int>();
            attacker.WonLastInvasion = won;
            if (!won)
            {
                // 플레이어는 목숨 −1(상대의 땅 수와 상관없다). 이미 0(국가 위기)이면 게임 오버 — 사용자 10/23
                if (attacker.IsPlayer)
                {
                    if (state.Lives <= 0)
                        attacker.Alive = false;
                    state.Lives = Math.Max(0, state.Lives - 1);
                }
                else
                    attacker.Happiness = Math.Max(0, attacker.Happiness - rules.LossDrop(state.LandsOf(target)));
                return taken;
            }
            if (!attacker.IsPlayer)
                taken.Add(region);
            else if (state.CoalitionOf(target) is WorldState.Coalition coalition)
            {
                taken = Nearest(state, region, state.CoalitionTake(coalition));
                coalition.Wins++;
            }
            else
                taken = Nearest(state, region, state.LandsOf(target));
            foreach (int land in taken)
                state.Owner[land] = attacker.Id;
            if (state.LandsOf(target) == 0)
                target.Alive = false;
            // 플레이어의 목숨은 이겨도 늘지 않는다
            if (!attacker.IsPlayer)
                attacker.Happiness = Math.Min(rules.HappinessMax, attacker.Happiness + rules.WinHappiness);
            return taken;
        }

        // 플레이어가 이벤트 땅 region을 방문한다(아웃게임기획 §3-1) — 그 턴의 침략 대신. 전투가 아니라 목숨 · 판도 · 승리 보너스는 그대로이고, 그 땅은 닫힌다(런에 다시 못 씀 · 방문 상한은 없다).
        // 방문할 수 없는 땅(국경을 안 맞댐 · 닫힘 · 이벤트 땅 아님)이면 예외. 그 땅의 종류를 돌려준다
        public static EventLandKind Visit(WorldState state, int region)
        {
            if (!state.VisitableEvents(state.Player).Contains(region))
                throw new ArgumentException($"방문할 수 없는 땅 {region}");
            state.EventClosed[region] = true;
            state.Player.WonLastInvasion = false;
            return state.EventKind[region];
        }

        // region의 영주가 가진 땅 중 region에서 가까운 순으로 count개 — region이 맨 앞이고, 그 영주의 땅을 따라 국경을 맞댄 땅부터 번져 간다(거리가 같으면 지도의 이웃 순서).
        // 이어진 덩어리가 모자라면 떨어져 있는 땅을 지도에서 가까운 순으로 채운다
        private static List<int> Nearest(WorldState state, int region, int count)
        {
            int owner = state.Owner[region];
            var lands = new List<int> { region };
            for (int i = 0; i < lands.Count; i++)
                foreach (int next in state.Graph.Links[lands[i]])
                    if (state.Owner[next] == owner && !lands.Contains(next))
                        lands.Add(next);
            var reached = new List<int> { region };
            for (int i = 0; i < reached.Count && lands.Count < count; i++)
                foreach (int next in state.Graph.Links[reached[i]])
                    if (!reached.Contains(next))
                    {
                        reached.Add(next);
                        if (state.Owner[next] == owner && !lands.Contains(next))
                            lands.Add(next);
                    }
            if (lands.Count > count)
                lands.RemoveRange(count, lands.Count - count);
            return lands;
        }

        // ③ AI 침략 — 영주를 무작위 차례로 한 명씩. 차례가 온 영주가 그 순간의 판도에서 대상을 고르고, 주사위 한 번으로 승패.
        // 차례가 오기 전에 땅을 다 잃은 영주는 건너뛴다. 한 영주가 한 턴에 여러 영주에게 땅을 하나씩 잃을 수 있다
        public static void AiPhase(WorldState state, Func<Lord, float> power, Random random)
        {
            foreach (Lord lord in AiOrder(state, random))
                AiInvade(state, lord, power, random, out _, out _);
        }

        // 이번 턴 AI 영주들의 차례(무작위)
        public static List<Lord> AiOrder(WorldState state, Random random)
        {
            var order = new List<Lord>();
            foreach (Lord lord in state.Lords)
                if (lord.Alive && !lord.IsPlayer)
                    order.Add(lord);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        // AI 영주 한 명의 차례 — 침략했으면 그 대상(쉬었으면 null), region = 침략한 땅(대상의 땅 중 내 땅과 국경을 맞댄 것 하나를 무작위로), won = 이겼나
        public static Lord AiInvade(WorldState state, Lord lord, Func<Lord, float> power, Random random, out int region, out bool won)
        {
            region = -1;
            won = false;
            if (!lord.Alive)
                return null;
            Lord target = PickTarget(state, lord, power, random);
            if (target == null)
                return null;
            var border = new List<int>();
            for (int land = 0; land < state.Owner.Length; land++)
                if (state.Owner[land] == target.Id && Array.Exists(state.Graph.Links[land], next => state.Owner[next] == lord.Id))
                    border.Add(land);
            region = border[random.Next(border.Count)];
            won = random.NextDouble() < WinChance(power(lord), power(target));
            Invade(state, lord, region, won);
            return target;
        }

        // ④ 정산 — 행복도가 0인 AI 영주는 반란으로 멸망하고, 그 영주의 땅마다 새 영주가 들어선다. 플레이어는 행복도가 없다(목숨 — 게임 오버는 Invade에서).
        // 새로 들어선 영주들을 돌려준다(부르는 쪽이 탑을 세운다) — 다음 턴부터 움직인다
        public static List<Lord> Settle(WorldState state, Random random)
        {
            var created = new List<Lord>();
            int count = state.Lords.Count;
            for (int i = 0; i < count; i++)
            {
                Lord lord = state.Lords[i];
                if (!lord.Alive || lord.IsPlayer || lord.Happiness > 0)
                    continue;
                lord.Alive = false;
                for (int region = 0; region < state.Owner.Length; region++)
                    if (state.Owner[region] == lord.Id)
                        created.Add(state.AddLord(region, random));
            }
            return created;
        }

        // 연합(기획 §6-1 · 사용자 10/27) — ④ 정산 끝에 부른다. 내 땅이 CoalitionLands 이상이고 AI 영주가 둘 이상 남았으면 뭉친다(런에 한 번):
        //   유령 영주가 있으면 — 살아 있는 유령마다 연합 하나. 남은 AI 영주는 전부 지도 거리(영주 땅 그래프 최단 거리)가 가장 가까운 유령에게 흡수된다
        //     (같으면 땅이 많은 유령, 그것도 같으면 번호가 앞선 유령). 이기는 횟수는 뭉칠 때 땅 수로(WorldRules.GhostCoalitionWins).
        //   유령이 없으면 — 힘이 가장 센 영주(동점은 번호가 앞선 쪽) 아래 하나로, 이기는 횟수 CoalitionWins.
        // 흡수된 영주는 사라지고 땅이 바탕 영주의 것이 된다. 뭉친 연합들을 돌려준다(안 뭉쳤으면 빈 목록 — state.Coalitions에도 든다)
        public static List<WorldState.Coalition> FormCoalitions(WorldState state, Func<Lord, float> power, Func<Lord, bool> isGhost)
        {
            WorldRules rules = state.Rules;
            var formed = new List<WorldState.Coalition>();
            if (!rules.Coalition || state.Coalitions.Count > 0 || !state.Player.Alive || state.LandsOf(state.Player) < rules.CoalitionLands || state.AiLords < 2)
                return formed;
            var leaders = new List<Lord>();
            foreach (Lord lord in state.Lords)
                if (lord.Alive && !lord.IsPlayer && isGhost(lord))
                    leaders.Add(lord);
            bool ghost = leaders.Count > 0;
            if (!ghost)
            {
                Lord strongest = null;
                float best = 0f;
                foreach (Lord lord in state.Lords)
                {
                    if (!lord.Alive || lord.IsPlayer)
                        continue;
                    float value = power(lord);
                    if (strongest == null || value > best)
                    {
                        strongest = lord;
                        best = value;
                    }
                }
                leaders.Add(strongest);
            }
            // [바탕 영주 차례][지역] = 그 바탕 영주의 땅에서 건너는 수
            var distance = new int[leaders.Count][];
            for (int i = 0; i < leaders.Count; i++)
                distance[i] = DistancesFrom(state, leaders[i]);
            var members = new List<int>[leaders.Count];
            for (int i = 0; i < leaders.Count; i++)
                members[i] = new List<int>();
            // 판도를 바꾸기 전에 다 정한다(흡수가 다른 영주의 거리 · 땅 수를 바꾸지 않게)
            var joins = new List<(Lord Lord, int Leader)>();
            foreach (Lord lord in state.Lords)
            {
                if (!lord.Alive || lord.IsPlayer || leaders.Contains(lord))
                    continue;
                int pick = 0, pickDistance = int.MaxValue, pickLands = 0;
                for (int i = 0; i < leaders.Count; i++)
                {
                    int d = int.MaxValue;
                    for (int region = 0; region < state.Owner.Length; region++)
                        if (state.Owner[region] == lord.Id && distance[i][region] >= 0)
                            d = Math.Min(d, distance[i][region]);
                    int lands = state.LandsOf(leaders[i]);
                    // 차례가 번호 순이라 같으면 앞선 번호가 남는다
                    if (d < pickDistance || (d == pickDistance && lands > pickLands))
                    {
                        pick = i;
                        pickDistance = d;
                        pickLands = lands;
                    }
                }
                joins.Add((lord, pick));
            }
            foreach ((Lord lord, int leader) in joins)
            {
                for (int region = 0; region < state.Owner.Length; region++)
                    if (state.Owner[region] == lord.Id)
                        state.Owner[region] = leaders[leader].Id;
                lord.Alive = false;
                members[leader].Add(lord.Id);
            }
            for (int i = 0; i < leaders.Count; i++)
            {
                int lands = state.LandsOf(leaders[i]);
                var coalition = new WorldState.Coalition
                {
                    Lord = leaders[i],
                    Ghost = ghost,
                    StartLands = lands,
                    WinsNeeded = ghost ? rules.GhostCoalitionWins(lands) : rules.CoalitionWins,
                    Members = members[i].ToArray(),
                };
                state.Coalitions.Add(coalition);
                formed.Add(coalition);
            }
            return formed;
        }

        // lord의 땅들에서 영주 땅끼리의 길로 건너는 수(닿지 않는 땅은 −1)
        private static int[] DistancesFrom(WorldState state, Lord lord)
        {
            var distance = new int[state.Owner.Length];
            var queue = new Queue<int>();
            for (int region = 0; region < distance.Length; region++)
            {
                distance[region] = state.Owner[region] == lord.Id ? 0 : -1;
                if (distance[region] == 0)
                    queue.Enqueue(region);
            }
            while (queue.Count > 0)
            {
                int region = queue.Dequeue();
                foreach (int next in state.Graph.Links[region])
                    if (distance[next] < 0)
                    {
                        distance[next] = distance[region] + 1;
                        queue.Enqueue(next);
                    }
            }
            return distance;
        }

        // 약자 보정(기획 §6-1) — AI 영주가 이번 턴 수입에 더 받는 골드: 가장 큰 영주(플레이어 포함)보다 땅이 적은 만큼 × PerLand, Max까지. 플레이어는 받지 않는다
        public static int UnderdogGold(WorldState state, Lord lord)
        {
            WorldRules rules = state.Rules;
            if (!rules.Underdog || lord.IsPlayer)
                return 0;
            int largest = 0;
            foreach (Lord other in state.Lords)
                if (other.Alive)
                    largest = Math.Max(largest, state.LandsOf(other));
            return Math.Min(rules.UnderdogGoldMax, (largest - state.LandsOf(lord)) * rules.UnderdogGoldPerLand);
        }
    }
}

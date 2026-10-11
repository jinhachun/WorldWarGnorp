using System;
using System.Collections.Generic;

namespace GnorpWar
{
    // 월드맵 한 턴의 규칙(기획 §2 · §5 · §6) — 판도(WorldState)만 바꾼다. 탑·상점·실제 전투는 모른다:
    // 영주의 힘은 power로 바깥에서 받고(탑 전투력), 플레이어의 승패는 부르는 쪽이 정해서 Invade에 넘긴다.
    // 한 턴 = (상점 — 바깥) → 플레이어의 Invade(쉬면 안 부른다) → AiPhase → Settle → Turn + 1.
    // UnityEngine을 쓰지 않고 난수도 받아 쓴다 — Unity 밖에서 같은 씨앗으로 수백 판 돌려 볼 수 있게
    public static class WorldTurn
    {
        // 승률 = 내 힘² ÷ (내 힘² + 상대 힘²)
        public static double WinChance(float mine, float theirs)
        {
            double a = (double)mine * mine, b = (double)theirs * theirs;
            return a + b > 0.0 ? a / (a + b) : 0.5;
        }

        // 성향대로 고른 침략 대상 — 후보는 이어진 다른 AI 영주뿐(플레이어의 땅은 공격당하지 않는다). 쉬면 null. 동점은 무작위
        public static Lord PickTarget(WorldState state, Lord lord, Func<Lord, float> power, Random random)
        {
            if (lord.Temper == LordTemper.Cautious)
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

        // 침략 한 번의 결과 — 이기면 상대의 땅을 전부 가져가고 행복도가 오른다(상대는 사라진다). 지면 상대의 땅 수만큼 행복도가 떨어진다(막아 낸 쪽은 그대로).
        // 행복도가 바닥난 영주의 멸망은 여기가 아니라 Settle에서
        public static void Invade(WorldState state, Lord attacker, Lord target, bool won)
        {
            WorldRules rules = state.Rules;
            attacker.WonLastInvasion = won;
            if (!won)
            {
                attacker.Happiness = Math.Max(0, attacker.Happiness - rules.LossDrop(state.LandsOf(target)));
                return;
            }
            for (int region = 0; region < state.Owner.Length; region++)
                if (state.Owner[region] == target.Id)
                    state.Owner[region] = attacker.Id;
            target.Alive = false;
            attacker.Happiness = Math.Min(rules.HappinessMax, attacker.Happiness + rules.WinHappiness);
        }

        // ③ AI 침략 — 영주를 무작위 차례로 한 명씩. 차례가 온 영주가 그 순간의 판도에서 대상을 고르고, 주사위 한 번으로 승패.
        // 차례가 오기 전에 먹힌 영주는 건너뛴다. 방금 땅을 먹은 영주가 같은 턴에 통째로 먹힐 수 있다
        public static void AiPhase(WorldState state, Func<Lord, float> power, Random random)
        {
            foreach (Lord lord in AiOrder(state, random))
                AiInvade(state, lord, power, random, out _);
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

        // AI 영주 한 명의 차례 — 침략했으면 그 대상(쉬었으면 null), won = 이겼나
        public static Lord AiInvade(WorldState state, Lord lord, Func<Lord, float> power, Random random, out bool won)
        {
            won = false;
            if (!lord.Alive)
                return null;
            Lord target = PickTarget(state, lord, power, random);
            if (target == null)
                return null;
            won = random.NextDouble() < WinChance(power(lord), power(target));
            Invade(state, lord, target, won);
            return target;
        }

        // ④ 정산 — 행복도가 0인 영주는 반란으로 멸망한다. 플레이어면 게임 오버(WorldState.GameOver), AI면 그 영주의 땅마다 새 영주가 들어선다.
        // 새로 들어선 영주들을 돌려준다(부르는 쪽이 탑을 세운다) — 다음 턴부터 움직인다
        public static List<Lord> Settle(WorldState state, Random random)
        {
            var created = new List<Lord>();
            int count = state.Lords.Count;
            for (int i = 0; i < count; i++)
            {
                Lord lord = state.Lords[i];
                if (!lord.Alive || lord.Happiness > 0)
                    continue;
                lord.Alive = false;
                if (lord.IsPlayer)
                    continue;
                for (int region = 0; region < state.Owner.Length; region++)
                    if (state.Owner[region] == lord.Id)
                        created.Add(state.AddLord(region, random));
            }
            return created;
        }

        // 최후의 연합(기획 §6-1) — ④ 정산 끝에 부른다. 내 땅이 CoalitionLands 이상이고 AI 영주가 둘 이상 남았으면, 힘이 가장 센 영주(동점은 번호가 앞선 쪽)를
        // 바탕으로 남은 AI 영주가 전부 하나로 뭉친다: 나머지의 땅이 바탕 영주의 것이 되고 나머지는 사라진다. 바탕 영주를 돌려주고 merged에 합쳐진 영주들을 담는다.
        // 뭉치지 않았으면 null — 런에 한 번뿐이다. 연합은 남은 AI가 하나라 침략할 대상이 없고, 그래서 행복도가 깎여 쪼개질 일도 없다
        public static Lord FormCoalition(WorldState state, Func<Lord, float> power, List<Lord> merged)
        {
            WorldRules rules = state.Rules;
            if (!rules.Coalition || state.Coalition != null || !state.Player.Alive || state.LandsOf(state.Player) < rules.CoalitionLands || state.AiLords < 2)
                return null;
            Lord leader = null;
            float best = 0f;
            foreach (Lord lord in state.Lords)
            {
                if (!lord.Alive || lord.IsPlayer)
                    continue;
                float value = power(lord);
                if (leader == null || value > best)
                {
                    leader = lord;
                    best = value;
                }
            }
            foreach (Lord lord in state.Lords)
            {
                if (!lord.Alive || lord.IsPlayer || lord == leader)
                    continue;
                for (int region = 0; region < state.Owner.Length; region++)
                    if (state.Owner[region] == lord.Id)
                        state.Owner[region] = leader.Id;
                lord.Alive = false;
                merged.Add(lord);
            }
            state.Coalition = leader;
            return leader;
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

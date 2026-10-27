using System;

namespace GnorpWar
{
    // 월드맵 규칙의 수치 — 값의 원본은 WorldConfig 에셋이고(ToRules), 여기는 Unity 밖에서도 돌도록 숫자만 든 사본이다
    public class WorldRules
    {
        // 플레이어의 목숨 — 지면 −1, 늘지 않는다. 0이 되면 국가 위기, 위기에서 지면 게임 오버(WorldTurn.Invade · 사용자 10/23). AI 영주는 아래 행복도
        public int LivesStart = 6;
        public int HappinessStart = 3;
        public int HappinessMax = 6;
        public int WinHappiness = 1;
        // 패배 시 행복도 −1에서 시작해, 상대의 땅 수가 이 값들 이상일 때마다 1씩 더 떨어진다({4, 10} = 1~3개 −1 · 4~9개 −2 · 10개 이상 −3)
        public int[] LossLandSteps = { 4, 10 };
        // 칸 순서 = LordTemper
        public float[] TemperWeights = { 0.40f, 0.27f, 0.23f, 0.10f };
        // 연합 — 내 땅이 이 수 이상이면 남은 AI 영주들이 유령 영주마다 하나씩(유령이 없으면 하나로) 뭉친다(WorldTurn.FormCoalitions)
        public bool Coalition = true;
        public int CoalitionLands = 15;
        // 유령이 없을 때의 연합을 다 먹으려면 이겨야 하는 횟수 — 한 번 이길 때마다 뭉칠 때 땅 수 ÷ 이기는 횟수(올림)만큼 넘어오고, 마지막 승리에 남은 땅 전부
        public int CoalitionWins = 3;
        // 유령 연합의 이기는 횟수 = 1 + (뭉칠 때 땅 수가 이 값들 이상인 개수) — {4, 8} = 1~3개 1번 · 4~7개 2번 · 8개 이상 3번(사용자 10/27)
        public int[] GhostCoalitionWinSteps = { 4, 8 };
        // 연합 수의 상한 — 유령이 더 많으면 땅이 많은 유령부터 바탕, 나머지 유령은 흡수된다(사용자 10/28)
        public int MaxCoalitions = 3;

        public int GhostCoalitionWins(int lands)
        {
            int wins = 1;
            foreach (int step in GhostCoalitionWinSteps)
                if (lands >= step)
                    wins++;
            return wins;
        }
        // 약자 보정 — AI 영주가 턴마다 더 받는 골드 = (가장 큰 영주의 땅 수 − 자기 땅 수) × PerLand, Max까지(WorldTurn.UnderdogGold)
        public bool Underdog = true;
        public int UnderdogGoldPerLand = 1;
        public int UnderdogGoldMax = 10;

        public int LossDrop(int targetLands)
        {
            int drop = 1;
            foreach (int step in LossLandSteps)
                if (targetLands >= step)
                    drop++;
            return drop;
        }

        public LordTemper RollTemper(Random random)
        {
            float total = 0f;
            foreach (float weight in TemperWeights)
                total += weight;
            double pick = random.NextDouble() * total;
            for (int i = 0; i < TemperWeights.Length; i++)
            {
                pick -= TemperWeights[i];
                if (pick < 0.0)
                    return (LordTemper)i;
            }
            return (LordTemper)(TemperWeights.Length - 1);
        }
    }
}

using System;

namespace GnorpWar
{
    // 월드맵 규칙의 수치 — 값의 원본은 WorldConfig 에셋이고(ToRules), 여기는 Unity 밖에서도 돌도록 숫자만 든 사본이다
    public class WorldRules
    {
        public int HappinessStart = 3;
        public int HappinessMax = 6;
        public int WinHappiness = 1;
        // 패배 시 행복도 −1에서 시작해, 상대의 땅 수가 이 값들 이상일 때마다 1씩 더 떨어진다({4, 10} = 1~3개 −1 · 4~9개 −2 · 10개 이상 −3)
        public int[] LossLandSteps = { 4, 10 };
        // 칸 순서 = LordTemper
        public float[] TemperWeights = { 0.40f, 0.27f, 0.23f, 0.10f };
        // 최후의 연합 — 내 땅이 이 수 이상이면 남은 AI 영주들이 하나로 뭉친다(WorldTurn.FormCoalition)
        public bool Coalition = true;
        public int CoalitionLands = 15;
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

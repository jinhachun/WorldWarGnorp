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

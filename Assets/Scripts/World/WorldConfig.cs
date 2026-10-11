using UnityEngine;

namespace GnorpWar
{
    // 월드맵 런의 수치(기획 §5 · §6) — 규칙(WorldTurn)은 ToRules가 만든 숫자 사본을 읽는다
    [CreateAssetMenu(menuName = "GnorpWar/World Config")]
    public class WorldConfig : ScriptableObject
    {
        [Tooltip("이번 런의 월드맵 한 장")]
        [SerializeField] private WorldMapDefinition _map;

        [Header("행복도 — 0이 되면 반란으로 멸망한다(플레이어는 게임 오버)")]
        [SerializeField] private int _happinessStart = 3;
        [SerializeField] private int _happinessMax = 6;
        [Tooltip("침략에 이기면 오르는 값 — 상대의 땅 크기와 상관없다")]
        [SerializeField] private int _winHappiness = 1;
        [Tooltip("침략에 지면 −1에서 시작해, 상대의 땅 수가 이 값들 이상일 때마다 1씩 더 떨어진다 — 4 · 10이면 땅 1~3개 −1 · 4~9개 −2 · 10개 이상 −3")]
        [SerializeField] private int[] _lossLandSteps = { 4, 10 };

        [Header("영주 성향 — 영주가 생길 때(런 시작 · 반란 뒤) 이 비율로 뽑는다")]
        [Tooltip("칸 순서: 약탈자(가장 약한 이웃을 침) · 야심가(땅이 가장 많은 이웃) · 무모한 자(무작위 이웃) · 안전주의자(늘 쉰다). 합이 1이 아니어도 된다(비율로 읽는다)")]
        [SerializeField] private float[] _temperWeights = { 0.40f, 0.27f, 0.23f, 0.10f };

        public WorldMapDefinition Map => _map;

        public WorldRules ToRules() => new WorldRules
        {
            HappinessStart = _happinessStart,
            HappinessMax = _happinessMax,
            WinHappiness = _winHappiness,
            LossLandSteps = (int[])_lossLandSteps.Clone(),
            TemperWeights = (float[])_temperWeights.Clone(),
        };
    }
}

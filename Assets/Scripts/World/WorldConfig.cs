using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 월드맵 런의 수치(기획 §5 · §6 · §6-1) — 규칙(WorldTurn)은 ToRules가 만든 숫자 사본을 읽는다
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

        [Header("수입 · 상점 — 영주마다 턴에 한 번")]
        [Tooltip("턴 수입 = 1라운드 상점 골드(BattleConfig) + 땅 수 × 이 값. 침략에 이긴 다음 턴에는 승리 보너스(BattleConfig)도 받는다")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _goldPerLand = 2;
        [Tooltip("상점 등급 단계 = 턴 − 1, 이 값까지(단계 0 = 지금의 1라운드 진열 확률 · 9 = 10라운드)")]
        [SerializeField] private int _shopTierMax = 9;
        [Tooltip("반란 뒤 들어선 새 영주가 생기자마자 보는 상점 횟수(그 턴의 수입으로) — 탑은 시작 층수(BattleConfig)")]
        [SerializeField] private int _newLordShops = 1;

        [Header("탑 전투력 — AI끼리의 승패 주사위가 읽는다(World/TowerPower)")]
        [Tooltip("유닛을 쿨다운마다 내지 않는 기물(강화 · 타워 · 패시브)은 쓴 골드(구매가 × 합쳐진 수) × 이 값을 전투력에 얹는다. 0 = 생산 기물만 센다")]
        [SerializeField] private float _powerPerGold = 1f;

        [Header("최후의 연합 — 내 땅이 이 수 이상이 되면 남은 AI 영주들이 가장 센 탑의 영주 아래 하나로 뭉친다(턴의 정산 끝에)")]
        [SerializeField] private bool _coalition = true;
        [SuffixLabel("개", true)]
        [SerializeField] private int _coalitionLands = 15;

        [Header("약자 보정 — AI 영주가 턴 수입에 더 받는 골드 = (가장 큰 영주의 땅 수 − 자기 땅 수) × 땅당 골드, 상한까지. 플레이어는 받지 않는다")]
        [SerializeField] private bool _underdog = true;
        [Tooltip("땅 1개의 수입(땅당 골드)보다 작아야 땅이 많은 영주가 언제나 더 번다")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _underdogGoldPerLand = 1;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _underdogGoldMax = 10;

        [Header("영주 색 — 유닛 그림의 파란 자리 · 탑 지붕 · 깃발(Core/LordPaint). 내 색(파랑)은 BattleConfig의 열쇠 색")]
        [Tooltip("AI 영주의 색 — 영주 번호마다 11칸씩 건너뛰며 돌려 쓴다(반란으로 생긴 영주는 번호가 이어지므로 같은 색이 다시 나올 수 있다)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Color[] _lordColors = DefaultLordColors();

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        // 첫 값(임시) — 색상환에서 파랑 둘레 90도(내 색)를 비우고 고르게 나눈 28색. 이웃한 색이 헷갈리지 않게 한 칸 건너 옅게
        private static Color[] DefaultLordColors()
        {
            var colors = new Color[28];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.HSVToRGB(Mathf.Repeat((265f + 270f * i / colors.Length) / 360f, 1f), i % 2 == 0 ? 0.7f : 0.4f, 1f);
            return colors;
        }

        public WorldMapDefinition Map => _map;
        // 번호가 이웃한 영주(시작 때 = 이웃한 땅)의 색이 색상환에서 멀어지게 건너뛰며 고른다 — 건너뛰는 폭은 색 수(28)와 서로소여야 전부 한 번씩 돈다
        private const int ColorStride = 11;
        public Color LordColor(int lord) => _lordColors[lord * ColorStride % _lordColors.Length];
        public int GoldPerLand => _goldPerLand;
        public int ShopTierMax => _shopTierMax;
        public int NewLordShops => _newLordShops;
        public float PowerPerGold => _powerPerGold;

        public WorldRules ToRules() => new WorldRules
        {
            HappinessStart = _happinessStart,
            HappinessMax = _happinessMax,
            WinHappiness = _winHappiness,
            LossLandSteps = (int[])_lossLandSteps.Clone(),
            TemperWeights = (float[])_temperWeights.Clone(),
            Coalition = _coalition,
            CoalitionLands = _coalitionLands,
            Underdog = _underdog,
            UnderdogGoldPerLand = _underdogGoldPerLand,
            UnderdogGoldMax = _underdogGoldMax,
        };
    }
}

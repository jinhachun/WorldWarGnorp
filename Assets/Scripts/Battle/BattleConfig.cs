using UnityEngine;

namespace GnorpWar
{
    // 적 보드 한 라운드 — 필드 칸마다 건물과 경험치 (빈 칸은 Definition 없음)
    [System.Serializable]
    public class EnemyRound
    {
        [SerializeField] private OwnedBuilding[] _field = new OwnedBuilding[0];

        public OwnedBuilding[] Field => _field;
    }

    // 런·상점·전투 전체에 걸친 수치
    [CreateAssetMenu(menuName = "GnorpWar/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        [Header("보드")]
        [SerializeField] private int _fieldSlots = 5;
        [SerializeField] private int _storageSlots = 5;
        [Tooltip("건물 쿨다운 최소값(초) — 쿨감이 쌓여도 이 아래로는 안 내려간다")]
        [SerializeField] private float _minCooldown = 0.1f;

        [Header("런")]
        [SerializeField] private int _lives = 3;
        [Tooltip("이만큼 이기면 런 클리어")]
        [SerializeField] private int _winsToClear = 10;

        [Header("상점")]
        [Tooltip("상점 단계마다 받는 골드 (남은 골드는 이월)")]
        [SerializeField] private int _roundGold = 10;
        [Tooltip("직전 전투에서 이겼으면 더 받는 골드")]
        [SerializeField] private int _winBonus = 3;
        [SerializeField] private int _rerollCost = 1;
        [Tooltip("판매 값 = 가격 × 산 개수(경험치 + 1) × 이 비율, 내림, 최소 1")]
        [SerializeField] private float _sellRatio = 0.5f;
        [SerializeField] private int _offerCount = 5;
        [Tooltip("상점에 나오는 건물 — 등급을 먼저 뽑고 그 등급 안에서는 같은 확률")]
        [SerializeField] private BuildingDefinition[] _shopPool = new BuildingDefinition[0];

        [Header("등급 — 칸 순서: 일반 · 고급 · 희귀 · 영웅 · 전설")]
        [SerializeField] private int[] _rarityPrices = { 3, 4, 5, 10, 12 };
        [Tooltip("1라운드 진열의 등급 가중치")]
        [SerializeField] private float[] _rarityWeightsFirst = { 60, 30, 10, 0, 0 };
        [Tooltip("런 클리어 라운드(이긴 수 = Wins To Clear) 진열의 등급 가중치 — 그 사이는 라운드에 따라 섞고, 넘으면 이 값")]
        [SerializeField] private float[] _rarityWeightsLast = { 15, 25, 30, 20, 10 };

        [Header("적")]
        [Tooltip("라운드별 적 보드 — 0번이 1라운드. 라운드가 목록보다 많으면 마지막 보드")]
        [SerializeField] private EnemyRound[] _enemyRounds = new EnemyRound[0];

        [Header("가속 — 전투가 끝나지 않는 것을 막는다 (공격력 · 생산 수)")]
        [Tooltip("이 시각(초)에 첫 증가")]
        [SerializeField] private float _rampStartSeconds = 60f;
        [SerializeField] private float _rampStepSeconds = 10f;
        [Tooltip("한 번에 원래 공격력의 몇 배를 더하나 (단리, 0.1 = +10%)")]
        [SerializeField] private float _rampPerStep = 0.1f;
        [Tooltip("한 번에 모든 건물의 한 번 생산 수에 더하는 마리 수 (양 진영)")]
        [SerializeField] private int _rampUnitsPerStep = 1;

        public int FieldSlots => _fieldSlots;
        public int StorageSlots => _storageSlots;
        public float MinCooldown => _minCooldown;
        public int Lives => _lives;
        public int WinsToClear => _winsToClear;
        public int RoundGold => _roundGold;
        public int WinBonus => _winBonus;
        public int RerollCost => _rerollCost;
        public float SellRatio => _sellRatio;
        public int OfferCount => _offerCount;
        public BuildingDefinition[] ShopPool => _shopPool;
        public int PriceOf(BuildingRarity rarity) => _rarityPrices[(int)rarity];

        // round는 0부터 — 0이면 First, WinsToClear - 1 이상이면 Last
        public float RarityWeight(BuildingRarity rarity, int round)
            => Mathf.Lerp(_rarityWeightsFirst[(int)rarity], _rarityWeightsLast[(int)rarity], round / (float)Mathf.Max(1, _winsToClear - 1));

        public EnemyRound EnemyRoundAt(int round) => _enemyRounds[Mathf.Clamp(round, 0, _enemyRounds.Length - 1)];

        // 전투 경과 시간까지 오른 가속 단계 수 — 시작 시각에 첫 증가, 이후 간격마다
        private int RampStepsAt(float elapsed)
            => elapsed < _rampStartSeconds ? 0 : Mathf.FloorToInt((elapsed - _rampStartSeconds) / _rampStepSeconds) + 1;

        public float DamageMultiplierAt(float elapsed) => 1f + RampStepsAt(elapsed) * _rampPerStep;
        public int ExtraUnitsAt(float elapsed) => RampStepsAt(elapsed) * _rampUnitsPerStep;
    }
}

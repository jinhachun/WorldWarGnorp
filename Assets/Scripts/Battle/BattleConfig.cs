using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 런·상점·전투 전체에 걸친 수치
    [CreateAssetMenu(menuName = "GnorpWar/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        [Header("보드 — 필드는 성 위로 쌓는 탑(칸 0 = 1층)")]
        [Tooltip("런을 시작할 때의 탑 층수")]
        [SerializeField] private int _startFloors = 3;
        [Tooltip("층 올리기로 닿을 수 있는 최대 층수")]
        [SerializeField] private int _maxFloors = 8;
        [Tooltip("층 올리기 비용 — 칸 순서: 첫 번째 올리기 · 두 번째 … (칸 수 = 최대 층수 − 시작 층수). 칸이 모자라면 마지막 값")]
        [SerializeField] private int[] _floorCosts = { 8, 14, 28, 44, 64 };
        [SerializeField] private int _storageSlots = 5;

        [Header("월드맵 — 런 한 판의 판도 · 영주 · 턴 규칙")]
        [SerializeField] private WorldConfig _world;

        [Header("런")]
        [Tooltip("턴이 하나 오를 때마다 양쪽 성 최대 체력에 더하는 값 — N턴 = 성 에셋 체력 + 이 값 × (N - 1). 기물의 성 체력 보너스(+%·×)는 이 합에 붙는다")]
        [SerializeField] private float _baseHpPerRound = 0f;

        [Header("상점")]
        [Tooltip("턴마다 받는 상점 골드의 바탕 — 수입 = 이 값 + 땅 수 × 땅당 골드(WorldConfig) (남은 골드는 이월)")]
        [UnityEngine.Serialization.FormerlySerializedAs("_roundGold")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _roundGoldStart = 10;
        [Tooltip("직전 전투에서 이겼으면 더 받는 골드")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _winBonus = 3;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _rerollCost = 1;
        [Tooltip("판매 값 = 가격 × 산 개수(경험치 + 1) × 이 비율, 내림, 최소 1")]
        [SerializeField] private float _sellRatio = 0.5f;
        [SerializeField] private int _offerCount = 5;
        [Tooltip("진열 한 칸이 「공용 + 그 탑의 국가」 기물일 확률(0.66 = 66%) — 나머지는 다른 국가들의 기물. 등급을 먼저 뽑고 그 등급 안에서 건다(그 등급에 한쪽 기물이 없으면 다른 쪽에서). 내 상점과 AI 영주의 상점(영주의 국가 = 빌드 방향의 국가)에 같이 걸린다")]
        [SerializeField] private float _homeNationShare = 0.66f;
        [Tooltip("상점에 나오는 건물 — 등급을 먼저 뽑고 그 등급 안에서는 같은 확률")]
        [SerializeField] private BuildingDefinition[] _shopPool = new BuildingDefinition[0];
        [Tooltip("합치기 보상 업그레이드 전부 — 기물을 합칠 때마다 이 중 「후보에 나옴」이 켜진 것에서 셋이 나온다")]
        [SerializeField] private UpgradeDefinition[] _upgrades = new UpgradeDefinition[0];
        [Tooltip("지역 특수 업그레이드 전부(신화 등급) — 런마다 이 중 「특수 지역 수」만큼을 지역에 하나씩 놓고(남은 것은 그 런에 나오지 않는다)")]
        [SerializeField] private UpgradeDefinition[] _specialUpgrades = new UpgradeDefinition[0];
        [Tooltip("지역 특수 업그레이드가 놓이는 지역 수")]
        [SerializeField] private int _specialRegions = 10;

        [Header("등급 — 칸 순서: 일반 · 고급 · 희귀 · 영웅 · 전설 (가격만 신화 칸이 하나 더)")]
        [SerializeField] private int[] _rarityPrices = { 3, 4, 5, 10, 12, 24 };
        // 가중치는 5칸 — 신화는 진열되지 않으므로 칸이 없다. RarityWeight가 (int)rarity로 읽으니
        // 신화 기물이 상점 풀(_shopPool)에 들어가면 그때 IndexOutOfRange가 난다(지금은 풀에 없어 RunState.HomeOffer가 건너뛴다)
        [Tooltip("상점 등급 단계마다 한 줄(0단계부터 — 단계는 턴 수로 오른다, WorldConfig의 등급 단계) — 그 단계 진열의 등급 가중치. 단계가 줄 수를 넘으면 마지막 줄")]
        [SerializeField] private RarityWeights[] _rarityWeightsByTier = { new RarityWeights() };

        // 한 단계의 진열 등급 가중치 — 칸 순서: 일반 · 고급 · 희귀 · 영웅 · 전설(합이 100일 필요는 없다 — 비율로 읽는다)
        [System.Serializable]
        public class RarityWeights
        {
            public float[] Weights = { 100, 0, 0, 0, 0 };
        }

        [Header("등급업 — 생산 기물은 등급이 올라도 한 번에 나오는 수가 그대로고, 쿨다운이 준다(사용자 10/10)")]
        [Tooltip("생산 기물(유닛을 내는 기물)의 등급이 하나 오를 때마다 쿨다운에 곱한다 — 0.8이면 8 → 6.4 → 5.12초")]
        [SerializeField] private float _rankCooldownFactor = 0.8f;

        [Header("맵 — 전장은 침략한 땅의 지형이다(WorldMapDefinition). 이 목록은 상점의 지형과 치트의 맵 고정이 읽는다")]
        [Tooltip("0번 = 상대를 고르기 전(상점)에 깔리는 지형(기본)")]
        [SerializeField] private MapDefinition[] _maps = new MapDefinition[0];

        [Header("영주 색 — 유닛 그림의 한 색 · 탑 지붕 · 깃발을 그 진영 영주의 색으로 칠한다(Core/LordPaint)")]
        [Tooltip("유닛 그림에서 영주 색으로 바뀌는 색 — 아군 그림(Sprites/Units/Ally)에 칠한 파랑과 같아야 한다. 내 색이기도 하다(나는 그린 그대로)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Color _lordKeyColor = new Color32(0x81, 0xAB, 0xFF, 0xFF);

        [Header("가속 — 전투가 끝나지 않는 것을 막는다 (공격력 · 생산 수)")]
        [Tooltip("이 시각(초)에 첫 증가")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _rampStartSeconds = 60f;
        [SuffixLabel("초", true)]
        [SerializeField] private float _rampStepSeconds = 10f;
        [Tooltip("한 번에 원래 공격력의 몇 배를 더하나 (단리, 0.1 = +10%)")]
        [SerializeField] private float _rampPerStep = 0.1f;
        [Tooltip("한 번에 모든 건물의 한 번 생산 수에 더하는 마리 수 (양 진영)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _rampUnitsPerStep = 1;

        [Header("데스매치 — 오래 끄는 전투를 남은 유닛으로 끝낸다")]
        [Tooltip("이 시각(초)에 양쪽 본진이 화살을 멈춘다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _deathmatchSeconds = 180f;
        [Tooltip("본진이 멈춘 뒤 기물이 이만큼(초) 더 발동하고 멈춘다(생산 포함)")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _boardGraceSeconds = 10f;
        [Tooltip("교착 — 기물이 멈춘 뒤 이만큼(초) 동안 아무도 피해를 받지 않고 죽지 않으면 아군 승리. 가장 느린 유닛이 가장 느린 맵에서 성에서 성까지 걷는 시간보다 길어야 한다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _stalemateSeconds = 240f;
        [Tooltip("대기 풀림 — 전투 중 이만큼(초) 동안 아무도 피해를 받지 않고 죽지 않으면, 피해가 다시 날 때까지 유닛이 멈춰 서는 규칙(계단 · 사거리 안에서 서기 · 받침 기다리기)을 풀고 맞붙을 때까지 전진한다. 0 = 끔")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _stallAdvanceSeconds = 10f;

        [Header("유닛")]
        [Tooltip("유닛 크기 상한 — 1 = 기본 크기, 3 = 기본의 3배(+200%). 넘치는 성장은 버린다")]
        [SerializeField] private float _maxUnitSize = 3f;
        [Tooltip("밟기 — 적 머리 위에 서 있는 동안 그 적에게 초당 내 공격력의 이 비율만큼 피해 (모든 유닛)")]
        [SerializeField] private float _stompDamagePerSecond = 0.5f;
        [Tooltip("모든 유닛이 앞 아군 위로 한 번 뛰면(행군·싸움) 이만큼(초) 쉬고 다시 뛸 수 있다 — 너무 자주 뛰어 산이 쌓이기 전에 넘어가지 않게. 0 = 쉬지 않음")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _climbRest = 0f;
        [Tooltip("앞 아군이 내 이동 속도의 이 비율보다 느리게 움직여야 그 위로 올라탄다 — 낮출수록 거의 멈춘 아군에만 오른다")]
        [SerializeField] private float _climbSlowerThan = 0.5f;
        [Tooltip("등반 점프 높이 = 내 키 + 이 칸 수. 2면 한 번에 두 층을 넘는다")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _climbJumpExtra = 2f;
        [Tooltip("뒤돌아 싸움 — 「뒤돌아 싸움」이 켜진 병종(UnitCategory)은 사거리에 칠 것이 없고 등 뒤 이 칸 안에 적 유닛이 있으면 돌아서서 그쪽으로 걸어가 싸운다(사용자 10/16 — 백도어 줄이기)")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _turnBackRange = 4f;
        [Tooltip("뒤돌아 싸움을 그만두는 거리 = 위 칸 + 이 여유 — 들어가는 거리와 나오는 거리를 달리해 경계에서 앞뒤로 떨지 않게(사용자 10/20)")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _turnBackExit = 1f;
        [Tooltip("대상 · 방향을 정하면 적어도 이만큼 유지한다 — 노리는 적(대상이 죽거나 사거리 밖이면 바로 풀린다) · 뒤돌기를 그만둔 뒤 다시 돌아서기까지(사용자 10/20 — 앞뒤로 떨지 않게). 공격 판정 속도와는 무관")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _targetHold = 0.5f;

        [Header("전투력 — 체력바 밑 표시. 유닛 전투력 = √(지금 체력 × 초당 피해) × 표시 배율, 진영 합계")]
        [Tooltip("범위 공격(화염·휩쓸기) 유닛의 초당 피해에 곱한다 — 한 번에 여럿을 친다")]
        [SerializeField] private float _powerAreaScale = 2f;
        [Tooltip("회복 유닛은 초당 피해 대신 초당 회복량 × 이 값")]
        [SerializeField] private float _powerHealScale = 1f;
        [Tooltip("유닛 전투력에 곱하는 표시 배율")]
        [SerializeField] private float _powerDisplayScale = 1f;
        [Tooltip("초당 유닛 생산 수 표시 — 최근 이만큼(초) 동안 전장에 나온 유닛 수를 이 시간으로 나눈다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _spawnRateWindowSeconds = 10f;

        [Header("시체 — 죽은 유닛의 모습이 뒤쪽(자기 성 쪽)으로 날려 보내진다(연출만 — 규칙과 무관). 화면 = 가로 71 × 세로 40칸")]
        [Tooltip("날리는 세기(초당 칸) — 이 사이에서 무작위")]
        [SuffixLabel("칸/초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseSpeedMin = 32f;
        [SuffixLabel("칸/초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseSpeedMax = 40f;
        [Tooltip("발사 각도(도) — 수평에서 위로. 방향은 언제나 유닛의 뒤쪽")]
        [SuffixLabel("도", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseAngle = 35f;
        [Tooltip("발사 각도 편차 — ±이 값(도) 안에서 무작위")]
        [SuffixLabel("도", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseAngleSpread = 10f;
        [Tooltip("시체를 끌어내리는 중력(초당 칸/초) — 낮을수록 곧게 멀리 날아간다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseGravity = 12f;
        [Tooltip("도는 속도(초당 도) — 이 사이에서 무작위. 방향은 언제나 뒤로 구르는 쪽")]
        [SuffixLabel("도/초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseSpinMin = 360f;
        [SuffixLabel("도/초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseSpinMax = 540f;
        [Tooltip("이만큼(게임 시간 초) 지나면 사라진다. 그 전에 화면 아래로 빠져도 사라진다")]
        [SuffixLabel("초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _corpseLifetime = 5f;
        [Tooltip("동시에 떠 있는 시체 수 상한 — 넘으면 가장 오래된 것부터 돌려 쓴다. 전투 씬을 불러올 때 읽는다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private int _corpseMax = 128;

        [Header("단두대 혁명 도끼 — 본진이 던지는 큰 도끼가 돌며 포물선으로 날아가 닿는 순간 즉사 판정(사용자 10/14)")]
        [Tooltip("도끼 그림(Sprites/Fx/FX_Axe.png — 같은 이름으로 덮어쓰면 바로 반영)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _axeSprite;
        [Tooltip("성에서 대상까지 날아가는 시간(게임 시간 초)")]
        [SuffixLabel("초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _axeFlightSeconds = 0.9f;
        [Tooltip("포물선 꼭짓점 — 출발 · 대상 중 높은 쪽보다 이만큼(칸) 위")]
        [SuffixLabel("칸", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _axeArcHeight = 8f;
        [Tooltip("도는 속도(도/초)")]
        [SuffixLabel("도/초", true)]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _axeSpin = 900f;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public float CorpseSpeedMin => _corpseSpeedMin;
        public float CorpseSpeedMax => _corpseSpeedMax;
        public float CorpseAngle => _corpseAngle;
        public float CorpseAngleSpread => _corpseAngleSpread;
        public float CorpseGravity => _corpseGravity;
        public float CorpseSpinMin => _corpseSpinMin;
        public float CorpseSpinMax => _corpseSpinMax;
        public float CorpseLifetime => _corpseLifetime;
        public int CorpseMax => _corpseMax;
        public Sprite AxeSprite => _axeSprite;
        public float AxeFlightSeconds => _axeFlightSeconds;
        public float AxeArcHeight => _axeArcHeight;
        public float AxeSpin => _axeSpin;

        public WorldConfig World => _world;
        public int StartFloors => _startFloors;
        public int MaxFloors => _maxFloors;
        // raises = 지금까지 올린 횟수(0부터)
        public int FloorCost(int raises) => _floorCosts[Mathf.Min(raises, _floorCosts.Length - 1)];
        public float ClimbRest => _climbRest;
        public float ClimbSlowerThan => _climbSlowerThan;
        public float ClimbJumpExtra => _climbJumpExtra;
        public float TurnBackRange => _turnBackRange;
        public float TurnBackExit => _turnBackExit;
        public float TargetHold => _targetHold;
        public float PowerAreaScale => _powerAreaScale;
        public float PowerHealScale => _powerHealScale;
        public float PowerDisplayScale => _powerDisplayScale;
        public float SpawnRateWindowSeconds => _spawnRateWindowSeconds;
        public float MaxUnitSize => _maxUnitSize;
        public float StompDamagePerSecond => _stompDamagePerSecond;
        public float DeathmatchSeconds => _deathmatchSeconds;
        public float BoardStopSeconds => _deathmatchSeconds + _boardGraceSeconds;
        public float StalemateSeconds => _stalemateSeconds;
        public float StallAdvanceSeconds => _stallAdvanceSeconds;
        public int StorageSlots => _storageSlots;
        // round는 0부터(RunState.Round) — 0이면 0
        public float BaseHpBonus(int round) => _baseHpPerRound * round;
        // 턴 수입의 바탕 — 땅 수와 승리 보너스는 그 위에 얹는다(RunState.Income · WorldRun.IncomeOf)
        public int ShopGold => _roundGoldStart;
        public int WinBonus => _winBonus;
        public int RerollCost => _rerollCost;
        public float SellRatio => _sellRatio;
        public int OfferCount => _offerCount;
        public float HomeNationShare => _homeNationShare;
        public BuildingDefinition[] ShopPool => _shopPool;
        public UpgradeDefinition[] Upgrades => _upgrades;
        public UpgradeDefinition[] SpecialUpgrades => _specialUpgrades;
        public int SpecialRegions => _specialRegions;
        public int PriceOf(BuildingRarity rarity) => _rarityPrices[(int)rarity];
        // level = OwnedBuilding.Level(1 = 시작 등급 → 1)
        public float RankCooldownScale(int level) => Mathf.Pow(_rankCooldownFactor, level - 1);

        // tier = 상점 등급 단계(0부터 — RunState.ShopTier) — 그 단계의 줄을 그대로 읽는다(줄 사이를 섞지 않는다). 줄 수를 넘으면 마지막 줄
        public float RarityWeight(BuildingRarity rarity, int tier)
            => _rarityWeightsByTier[Mathf.Clamp(tier, 0, _rarityWeightsByTier.Length - 1)].Weights[(int)rarity];

        public MapDefinition[] Maps => _maps;

        public Color LordKeyColor => _lordKeyColor;

        // 전투 경과 시간까지 오른 가속 단계 수 — 시작 시각에 첫 증가, 이후 간격마다
        private int RampStepsAt(float elapsed)
            => elapsed < _rampStartSeconds ? 0 : Mathf.FloorToInt((elapsed - _rampStartSeconds) / _rampStepSeconds) + 1;

        public float DamageMultiplierAt(float elapsed) => 1f + RampStepsAt(elapsed) * _rampPerStep;
        public int ExtraUnitsAt(float elapsed) => RampStepsAt(elapsed) * _rampUnitsPerStep;
    }
}

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

        [Header("런")]
        [SerializeField] private int _lives = 3;
        [Tooltip("라운드가 하나 오를 때마다 양쪽 성 최대 체력에 더하는 값 — N라운드 = 성 에셋 체력 + 이 값 × (N - 1). 기물의 성 체력 보너스(+%·×)는 이 합에 붙는다")]
        [SerializeField] private float _baseHpPerRound = 0f;
        [Tooltip("이만큼 이기면 런 클리어")]
        [SerializeField] private int _winsToClear = 10;

        [Header("상점")]
        [Tooltip("1라운드 상점에서 받는 골드 (남은 골드는 이월)")]
        [UnityEngine.Serialization.FormerlySerializedAs("_roundGold")]
        [SerializeField] private int _roundGoldStart = 10;
        [Tooltip("라운드가 하나 오를 때마다 상점 골드에 더하는 값 — N라운드 = 시작 + 이 값 × (N - 1)")]
        [SerializeField] private int _roundGoldPerRound = 0;
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

        [Header("등급업 — 생산 기물은 등급이 올라도 한 번에 나오는 수가 그대로고, 쿨다운이 준다(사용자 10/10)")]
        [Tooltip("생산 기물(유닛을 내는 기물)의 등급이 하나 오를 때마다 쿨다운에 곱한다 — 0.8이면 8 → 6.4 → 5.12초")]
        [SerializeField] private float _rankCooldownFactor = 0.8f;

        [Header("맵 — 라운드마다 상점에 들어올 때 하나")]
        [Tooltip("0번 = 1라운드 맵(기본). 2라운드부터는 이 목록 전체에서 같은 확률로")]
        [SerializeField] private MapDefinition[] _maps = new MapDefinition[0];

        [Header("적")]
        [Tooltip("적의 빌드 방향 — 런을 시작할 때 하나를 같은 확률로 뽑고, 적은 런 내내 그 방향으로 상점을 돈다(Run/EnemyShopper)")]
        [SerializeField] private EnemyBuildProfile[] _enemyBuilds = new EnemyBuildProfile[0];
        [Tooltip("적이 라운드마다 받는 상점 골드에 곱한다 — 1 = 플레이어와 같다(반올림)")]
        [SerializeField] private float _enemyGoldScale = 1f;

        [Header("가속 — 전투가 끝나지 않는 것을 막는다 (공격력 · 생산 수)")]
        [Tooltip("이 시각(초)에 첫 증가")]
        [SerializeField] private float _rampStartSeconds = 60f;
        [SerializeField] private float _rampStepSeconds = 10f;
        [Tooltip("한 번에 원래 공격력의 몇 배를 더하나 (단리, 0.1 = +10%)")]
        [SerializeField] private float _rampPerStep = 0.1f;
        [Tooltip("한 번에 모든 건물의 한 번 생산 수에 더하는 마리 수 (양 진영)")]
        [SerializeField] private int _rampUnitsPerStep = 1;

        [Header("데스매치 — 오래 끄는 전투를 남은 유닛으로 끝낸다")]
        [Tooltip("이 시각(초)에 양쪽 본진이 화살을 멈춘다")]
        [SerializeField] private float _deathmatchSeconds = 180f;
        [Tooltip("본진이 멈춘 뒤 기물이 이만큼(초) 더 발동하고 멈춘다(생산 포함)")]
        [SerializeField] private float _boardGraceSeconds = 10f;
        [Tooltip("교착 — 기물이 멈춘 뒤 이만큼(초) 동안 아무도 피해를 받지 않고 죽지 않으면 아군 승리. 가장 느린 유닛이 가장 느린 맵에서 성에서 성까지 걷는 시간보다 길어야 한다")]
        [SerializeField] private float _stalemateSeconds = 240f;

        [Header("유닛")]
        [Tooltip("유닛 크기 상한 — 1 = 기본 크기, 3 = 기본의 3배(+200%). 넘치는 성장은 버린다")]
        [SerializeField] private float _maxUnitSize = 3f;
        [Tooltip("밟기 — 적 머리 위에 서 있는 동안 그 적에게 초당 내 공격력의 이 비율만큼 피해 (모든 유닛)")]
        [SerializeField] private float _stompDamagePerSecond = 0.5f;
        [Tooltip("모든 유닛이 앞 아군 위로 한 번 뛰면(행군·싸움) 이만큼(초) 쉬고 다시 뛸 수 있다 — 너무 자주 뛰어 산이 쌓이기 전에 넘어가지 않게. 0 = 쉬지 않음")]
        [SerializeField] private float _climbRest = 0f;
        [Tooltip("앞 아군이 내 이동 속도의 이 비율보다 느리게 움직여야 그 위로 올라탄다 — 낮출수록 거의 멈춘 아군에만 오른다")]
        [SerializeField] private float _climbSlowerThan = 0.5f;
        [Tooltip("등반 점프 높이 = 내 키 + 이 칸 수. 2면 한 번에 두 층을 넘는다")]
        [SerializeField] private float _climbJumpExtra = 2f;

        [Header("전투력 — 체력바 밑 표시. 유닛 전투력 = √(지금 체력 × 초당 피해) × 표시 배율, 진영 합계")]
        [Tooltip("범위 공격(화염·휩쓸기) 유닛의 초당 피해에 곱한다 — 한 번에 여럿을 친다")]
        [SerializeField] private float _powerAreaScale = 2f;
        [Tooltip("회복 유닛은 초당 피해 대신 초당 회복량 × 이 값")]
        [SerializeField] private float _powerHealScale = 1f;
        [Tooltip("유닛 전투력에 곱하는 표시 배율")]
        [SerializeField] private float _powerDisplayScale = 1f;
        [Tooltip("초당 유닛 생산 수 표시 — 최근 이만큼(초) 동안 전장에 나온 유닛 수를 이 시간으로 나눈다")]
        [SerializeField] private float _spawnRateWindowSeconds = 10f;

        [Header("시체 — 죽은 유닛의 모습이 뒤쪽(자기 성 쪽)으로 날려 보내진다(연출만 — 규칙과 무관). 화면 = 가로 71 × 세로 40칸")]
        [Tooltip("날리는 세기(초당 칸) — 이 사이에서 무작위")]
        [SerializeField] private float _corpseSpeedMin = 32f;
        [SerializeField] private float _corpseSpeedMax = 40f;
        [Tooltip("발사 각도(도) — 수평에서 위로. 방향은 언제나 유닛의 뒤쪽")]
        [SerializeField] private float _corpseAngle = 35f;
        [Tooltip("발사 각도 편차 — ±이 값(도) 안에서 무작위")]
        [SerializeField] private float _corpseAngleSpread = 10f;
        [Tooltip("시체를 끌어내리는 중력(초당 칸/초) — 낮을수록 곧게 멀리 날아간다")]
        [SerializeField] private float _corpseGravity = 12f;
        [Tooltip("도는 속도(초당 도) — 이 사이에서 무작위. 방향은 언제나 뒤로 구르는 쪽")]
        [SerializeField] private float _corpseSpinMin = 360f;
        [SerializeField] private float _corpseSpinMax = 540f;
        [Tooltip("이만큼(게임 시간 초) 지나면 사라진다. 그 전에 화면 아래로 빠져도 사라진다")]
        [SerializeField] private float _corpseLifetime = 5f;
        [Tooltip("동시에 떠 있는 시체 수 상한 — 넘으면 가장 오래된 것부터 돌려 쓴다. 전투 씬을 불러올 때 읽는다")]
        [SerializeField] private int _corpseMax = 128;

        public float CorpseSpeedMin => _corpseSpeedMin;
        public float CorpseSpeedMax => _corpseSpeedMax;
        public float CorpseAngle => _corpseAngle;
        public float CorpseAngleSpread => _corpseAngleSpread;
        public float CorpseGravity => _corpseGravity;
        public float CorpseSpinMin => _corpseSpinMin;
        public float CorpseSpinMax => _corpseSpinMax;
        public float CorpseLifetime => _corpseLifetime;
        public int CorpseMax => _corpseMax;

        public int StartFloors => _startFloors;
        public int MaxFloors => _maxFloors;
        // raises = 지금까지 올린 횟수(0부터)
        public int FloorCost(int raises) => _floorCosts[Mathf.Min(raises, _floorCosts.Length - 1)];
        public float ClimbRest => _climbRest;
        public float ClimbSlowerThan => _climbSlowerThan;
        public float ClimbJumpExtra => _climbJumpExtra;
        public float PowerAreaScale => _powerAreaScale;
        public float PowerHealScale => _powerHealScale;
        public float PowerDisplayScale => _powerDisplayScale;
        public float SpawnRateWindowSeconds => _spawnRateWindowSeconds;
        public float MaxUnitSize => _maxUnitSize;
        public float StompDamagePerSecond => _stompDamagePerSecond;
        public float DeathmatchSeconds => _deathmatchSeconds;
        public float BoardStopSeconds => _deathmatchSeconds + _boardGraceSeconds;
        public float StalemateSeconds => _stalemateSeconds;
        public int StorageSlots => _storageSlots;
        public int Lives => _lives;
        // round는 0부터(RunState.Round) — 0이면 0
        public float BaseHpBonus(int round) => _baseHpPerRound * round;
        public int WinsToClear => _winsToClear;
        // round는 0부터(RunState.Round) — 0이면 시작값
        public int RoundGold(int round) => _roundGoldStart + _roundGoldPerRound * round;
        public int WinBonus => _winBonus;
        public int RerollCost => _rerollCost;
        public float SellRatio => _sellRatio;
        public int OfferCount => _offerCount;
        public BuildingDefinition[] ShopPool => _shopPool;
        public int PriceOf(BuildingRarity rarity) => _rarityPrices[(int)rarity];
        // level = OwnedBuilding.Level(1 = 시작 등급 → 1)
        public float RankCooldownScale(int level) => Mathf.Pow(_rankCooldownFactor, level - 1);

        // round는 0부터 — 0이면 First, WinsToClear - 1 이상이면 Last
        public float RarityWeight(BuildingRarity rarity, int round)
            => Mathf.Lerp(_rarityWeightsFirst[(int)rarity], _rarityWeightsLast[(int)rarity], round / (float)Mathf.Max(1, _winsToClear - 1));

        // round는 0부터 — 0이면 첫 맵, 이후 무작위. 목록이 비면 null
        public MapDefinition MapFor(int round)
            => _maps.Length == 0 ? null : _maps[round == 0 ? 0 : Random.Range(0, _maps.Length)];

        public MapDefinition[] Maps => _maps;

        public EnemyBuildProfile[] EnemyBuilds => _enemyBuilds;
        public EnemyBuildProfile RandomEnemyBuild() => _enemyBuilds[Random.Range(0, _enemyBuilds.Length)];
        public float EnemyGoldScale => _enemyGoldScale;

        // 전투 경과 시간까지 오른 가속 단계 수 — 시작 시각에 첫 증가, 이후 간격마다
        private int RampStepsAt(float elapsed)
            => elapsed < _rampStartSeconds ? 0 : Mathf.FloorToInt((elapsed - _rampStartSeconds) / _rampStepSeconds) + 1;

        public float DamageMultiplierAt(float elapsed) => 1f + RampStepsAt(elapsed) * _rampPerStep;
        public int ExtraUnitsAt(float elapsed) => RampStepsAt(elapsed) * _rampUnitsPerStep;
    }
}

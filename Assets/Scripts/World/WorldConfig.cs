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
        [Tooltip("상점 등급 단계의 상한(단계 0 = 지금의 1라운드 진열 확률 · 9 = 10라운드)")]
        [SerializeField] private int _shopTierMax = 9;
        [Tooltip("상점 등급 단계 = (턴 − 1) × 상한 ÷ 이 값(내림) — 이 턴 수가 지나면 상한에 닿는다. 상한과 같은 값이면 턴마다 한 단계")]
        [SuffixLabel("턴", true)]
        [SerializeField] private int _shopTierTurns = 14;
        [Tooltip("반란 뒤 들어선 새 영주가 생기자마자 보는 상점 횟수(그 턴의 수입으로) — 탑은 시작 층수(BattleConfig)")]
        [SerializeField] private int _newLordShops = 1;
        [Tooltip("영주를 멸망시키고 지역 특수(신화) 업그레이드를 고를 때, 고르지 않고 건너뛰면 받는 골드(플레이어만 — 사용자 10/15)")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _specialSkipGold = 15;

        [Header("탑 전투력 — AI끼리의 승패 주사위가 읽는다(World/TowerPower)")]
        [Tooltip("유닛을 쿨다운마다 내지 않는 기물(강화 · 타워 · 패시브)은 쓴 골드(구매가 × 합쳐진 수) × 이 값을 전투력에 얹는다. 0 = 생산 기물만 센다")]
        [SerializeField] private float _powerPerGold = 1f;

        [Header("최후의 연합 — 내 땅이 이 수 이상이 되면 남은 AI 영주들이 가장 센 탑의 영주 아래 하나로 뭉친다(턴의 정산 끝에)")]
        [SerializeField] private bool _coalition = true;
        [SuffixLabel("개", true)]
        [SerializeField] private int _coalitionLands = 15;
        [Tooltip("연합을 다 먹으려면 이겨야 하는 횟수 — 한 번 이길 때마다 뭉칠 때 땅 수 ÷ 이 값(올림)만큼 넘어오고, 마지막 승리에 남은 땅 전부")]
        [SuffixLabel("번", true)]
        [SerializeField] private int _coalitionWins = 3;
        [Tooltip("연합의 탑을 합칠 때 나머지 영주의 탑 · 보관함을 판 골드에 곱하는 값 — 1이면 판 골드를 전부 쓴다(남은 골드는 그대로 넘어간다)")]
        [SerializeField] private float _coalitionGoldScale = 0.3f;

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

        [Tooltip("국가 아이콘 — 칸 순서 = BuildingNation(공용 · 킹덤 · 티크톡). 국가를 고르는 버튼에 쓴다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _nationIcons = new Sprite[0];
        [Tooltip("설명 글 줄 안 아이콘(UI/InlineIcons) — 칸 순서 = 공격력 · 체력 · 공격속도 · 쿨다운 · 골드 · 유닛 수(16×16 원본 크기, 사용자 10/21)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _inlineIcons = new Sprite[0];
        [Tooltip("국가 고르기 카드 — 칸 순서 = BuildingNation. 대표 유닛(큰 그림, 정수배로 키워 내 색으로) · 설명 글(깔끔 폰트, 사용자 10/15)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UnitDefinition[] _nationUnits = new UnitDefinition[0];
        [HideIf(nameof(HideNonBalance))]
        [TextArea(2, 4)]
        [SerializeField] private string[] _nationDescriptions = new string[0];
        [Tooltip("UI 테두리 조각(UI/NationFrame) — 칸 순서 = BuildingNation, 내 국가의 것을 쓴다(공용 칸이 비면 흰 선). 변 = 위쪽 변 한 토막(맨 윗줄이 바깥) · 모서리 = 왼쪽 위. 읽기 켜진 그림이어야 한다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _frameEdges = new Sprite[0];
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _frameCorners = new Sprite[0];

        [Header("영주 닉네임 — 지도의 영주 그림 위 · 왼쪽 패널 · 승리 글(임시 이름, 사용자 10/12)")]
        [Tooltip("이름 목록(값은 에셋에 — 흔한 외국 이름의 한글 표기, 사용자 10/12) — 한 런에서 영주마다 하나씩 겹치지 않게 돌려 쓰고, 다 쓰면 뒤에 숫자를 붙인다(제임스2)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string[] _lordNames = new string[0];
        [Tooltip("AI 영주의 이름 뒤에 붙는 글")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _lordNameSuffix = "_AI";

        // 번호가 이웃한 영주의 이름이 목록에서 나란하지 않게 건너뛰며 고른다 — 건너뛰는 폭은 이름 수와 서로소여야 전부 한 번씩 돈다(아니면 한 칸씩)
        private const int NameStride = 7;

        // index번째 영주의 닉네임(index ≥ 0) — 이름 수만큼은 서로 다르고, 그 뒤로는 숫자가 붙는다. offset = 목록에서 시작하는 자리(런마다 다르게). 규칙은 이 값을 읽지 않는다(표시용)
        public string LordName(int index, int offset)
        {
            int count = _lordNames.Length;
            if (count == 0)
                return _lordNameSuffix;
            int stride = Gcd(NameStride, count) == 1 ? NameStride : 1;
            int round = index / count;
            return _lordNames[(index + offset) % count * stride % count] + (round > 0 ? (round + 1).ToString() : "") + _lordNameSuffix;
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

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
        public Sprite NationIcon(BuildingNation nation) => _nationIcons[(int)nation];
        // 설명 글 줄 안 아이콘(UI/InlineIcons) — 칸 순서 = InlineIcons.Kind. 비면 그 낱말엔 아이콘 없음
        public Sprite InlineIcon(InlineIcons.Kind kind) => (int)kind < _inlineIcons.Length ? _inlineIcons[(int)kind] : null;
        public UnitDefinition NationUnit(BuildingNation nation) => (int)nation < _nationUnits.Length ? _nationUnits[(int)nation] : null;
        public Sprite FrameEdge(BuildingNation nation) => (int)nation < _frameEdges.Length ? _frameEdges[(int)nation] : null;
        public Sprite FrameCorner(BuildingNation nation) => (int)nation < _frameCorners.Length ? _frameCorners[(int)nation] : null;
        public string NationDescription(BuildingNation nation) => (int)nation < _nationDescriptions.Length ? _nationDescriptions[(int)nation] : "";
        public int GoldPerLand => _goldPerLand;
        public int ShopTierMax => _shopTierMax;
        // round는 0부터(턴 − 1) — 그 턴의 상점 등급 단계(기술 연구는 부르는 쪽이 얹는다)
        public int ShopTier(int round) => Mathf.Min(round * _shopTierMax / Mathf.Max(1, _shopTierTurns), _shopTierMax);
        public float CoalitionGoldScale => _coalitionGoldScale;
        public int NewLordShops => _newLordShops;
        public int SpecialSkipGold => _specialSkipGold;
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
            CoalitionWins = _coalitionWins,
            Underdog = _underdog,
            UnderdogGoldPerLand = _underdogGoldPerLand,
            UnderdogGoldMax = _underdogGoldMax,
        };
    }
}

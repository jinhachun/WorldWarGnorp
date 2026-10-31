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

        [Header("목숨 — 플레이어만. 지면 −1(이겨도 늘지 않는다) · 0이 되면 국가 위기 · 위기에서 지면 게임 오버")]
        [SerializeField] private int _livesStart = 6;

        [Header("행복도 — AI 영주만. 0이 되면 반란으로 멸망한다")]
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

        [Header("연합 — 내 땅이 이 수 이상이 되면 남은 AI 영주들이 유령 영주마다 하나씩, 유령이 없으면 가장 센 탑의 영주 아래 하나로 뭉친다(턴의 정산 끝에)")]
        [SerializeField] private bool _coalition = true;
        [SuffixLabel("개", true)]
        [SerializeField] private int _coalitionLands = 15;
        [Tooltip("연합을 다 먹으려면 이겨야 하는 횟수 — 한 번 이길 때마다 뭉칠 때 땅 수 ÷ 이 값(올림)만큼 넘어오고, 마지막 승리에 남은 땅 전부")]
        [SuffixLabel("번", true)]
        [SerializeField] private int _coalitionWins = 3;
        [Tooltip("연합의 탑을 합칠 때 나머지 영주의 탑 · 보관함을 판 골드에 곱하는 값 — 1이면 판 골드를 전부 쓴다(남은 골드는 그대로 넘어간다)")]
        [SerializeField] private float _coalitionGoldScale = 0.3f;
        [Tooltip("유령 연합(유령 영주마다 하나 — 남은 AI 영주는 가장 가까운 유령에게 흡수된다)을 다 먹으려면 이겨야 하는 횟수 = 1 + 뭉칠 때 땅 수가 이 값들 이상인 개수. {4, 8} = 1~3개 1번 · 4~7개 2번 · 8개 이상 3번. 유령이 없으면 위 「이겨야 하는 횟수」(사용자 10/27)")]
        [SerializeField] private int[] _ghostCoalitionWinSteps = { 4, 8 };
        [Tooltip("연합 수의 상한 — 뭉칠 때 유령이 더 많으면 땅이 많은 유령(같으면 탑 전투력 · 번호 순)이 바탕이 되고, 나머지 유령은 AI 영주처럼 가장 가까운 바탕에 흡수된다(사용자 10/28)")]
        [SuffixLabel("개", true)]
        [SerializeField] private int _maxCoalitions = 3;

        [Header("약자 보정 — AI 영주가 턴 수입에 더 받는 골드 = (가장 큰 영주의 땅 수 − 자기 땅 수) × 땅당 골드, 상한까지. 플레이어는 받지 않는다")]
        [SerializeField] private bool _underdog = true;
        [Tooltip("땅 1개의 수입(땅당 골드)보다 작아야 땅이 많은 영주가 언제나 더 번다")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _underdogGoldPerLand = 1;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _underdogGoldMax = 10;

        [Header("거리 배율 — AI 영주의 턴마다 얻는 골드(기본 + 땅 + 곡창) × (기본 배율 + 거리 × 턴 × 증가), 상한까지. 거리 = 내 시작 땅에서 그 영주의 시작 땅까지 건너는 영주 땅 수(사용자 10/27)")]
        [Tooltip("거리 0 · 1턴에 가까운 값 — 1보다 작으면 내 가까운 영주는 덜 번다")]
        [SuffixLabel("배", true)]
        [SerializeField] private float _distanceGoldBase = 0.8f;
        [Tooltip("거리 1 · 턴 1마다 늘어나는 배율")]
        [SuffixLabel("배", true)]
        [SerializeField] private float _distanceGoldPerStep = 0.03f;
        [SuffixLabel("배", true)]
        [SerializeField] private float _distanceGoldMax = 2.5f;

        [Header("유령 영주 — 다른 런 기록이 앉는 영주(아웃게임기획 §9)")]
        [Tooltip("유령 수의 상한 = 런 시작 때 상대 영주 수 × 이 값(내림). 나머지는 AI — 반란으로 선 영주도 이 상한 안에서만 유령이 된다(사용자 10/27)")]
        [SerializeField] private float _ghostShare = 0.5f;

        [Header("이벤트 땅 — 자리는 지도에 고정(산맥 · 강 띠 — 월드 지도 굽기), 종류만 런마다 섞는다(아웃게임기획 §3-1)")]
        [Tooltip("종류마다 하나 — 이름 · 아이콘 · 툴팁 글 · 수치(World/EventLandDefinition)")]
        [SerializeField] private EventLandDefinition[] _events = new EventLandDefinition[0];

        [Header("나라 소식 · 외교 — 플레이어만, 턴이 열릴 때(수입 뒤 · 상점 전) 팝업(아웃게임기획 §3-2)")]
        [Tooltip("나라 소식이 뜨는 턴")]
        [SerializeField] private int[] _newsTurns = { 3, 6, 9, 12 };
        [Tooltip("무작위로 뽑는 소식 — 런 안에서 겹치지 않는다(건국 기념제는 아래 칸)")]
        [SerializeField] private NationNewsDefinition[] _news = new NationNewsDefinition[0];
        [Tooltip("이 턴에는 무작위 대신 건국 기념제")]
        [SuffixLabel("턴", true)]
        [SerializeField] private int _festivalTurn = 12;
        [SerializeField] private NationNewsDefinition _festival;
        [Tooltip("외교 제안이 오는 턴의 후보 — 런을 시작할 때 이 중 하나를 골라 그 턴에 한 번(나라 소식 다음)")]
        [SerializeField] private int[] _diplomacyTurns = { 3, 6 };
        [Tooltip("외교 팝업 글 — 제목 · 제안 줄({0} 국가 · {1} 영주 이름) · 받아들임 · 그 아래 효과 두 줄({0} 국가 — 침략 줄은 그 영주가 이웃일 때만) · 거절")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _diplomacyTitle = "통상 제안";
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _diplomacyLine = "{0}의 {1} 영주가 통상을 제안합니다.";
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _diplomacyAccept = "받아들입니다";
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _diplomacyTruce = "이번 턴 동안 이 영주를 침략할 수 없습니다.";
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _diplomacyTrade = "앞으로 {0} 기물이 상점에 뜹니다.";
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _diplomacyDecline = "거절합니다";

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
        [Tooltip("국가 고르기 화면(UI/NationCard) — 칸 순서 = BuildingNation. 설명 글은 첫 문장(마침표 앞)이 국가의 한 줄로 쓰인다(깔끔 폰트, 사용자 10/15)")]
        [HideIf(nameof(HideNonBalance))]
        [TextArea(2, 4)]
        [SerializeField] private string[] _nationDescriptions = new string[0];
        [Tooltip("국가 고르기 화면의 무대(UI/NationCard, 사용자 10/21 B안) — 칸 순서 = BuildingNation. 빌드 갈래 이름은 기물기획 §8 흐름 이름 그대로")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private NationShowcase[] _nationShowcases = new NationShowcase[0];
        [Tooltip("UI 테두리 조각(UI/NationFrame) — 칸 순서 = BuildingNation, 내 국가의 것을 쓴다(공용 칸이 비면 흰 선). 변 = 위쪽 변 한 토막(맨 윗줄이 바깥) · 모서리 = 왼쪽 위. 읽기 켜진 그림이어야 한다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _frameEdges = new Sprite[0];
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _frameCorners = new Sprite[0];
        [Tooltip("전장 본진(성) 그림(Bases/Tower) — 칸 순서 = BuildingNation, 그 진영 국가의 것을 쓴다(사용자 10/22). 128×64 · 같은 몸통 · 같은 성문 자리 · 가운데 성벽 윗면은 아래에서 35px(탑 1층 바닥). 파란 자리(#81ABFF)는 영주 색. 비면 씬에 놓인 그림 그대로")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _castles = new Sprite[0];
        [Tooltip("영주 몸 그림(지도 · 땅 카드 · 왼쪽 패널 초상화 · 이벤트 장면 — UI/WorldLords.BodyOf) — 칸 순서 = BuildingNation(공용 칸은 킹덤 것), 그 영주 국가의 것을 쓴다(사용자 10/30). 파란 자리(#81ABFF)만 영주 색(Core/LordPaint). 크기는 국가마다 달라도 된다 — 몸 한가운데가 자리, 깃발은 몸 왼쪽 아래 기준")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _lordBodies = new Sprite[0];
        [Tooltip("영주 깃발 가운데 사진(UI/WorldLords.PhotoOf) — 비동기 멀티가 되면 영주마다 스팀 프로필 사진으로 바뀐다. 지금은 AI 영주가 모두 이 임시 그림 하나를 쓴다(사용자 10/23). 영주 색으로 칠하지 않는다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _tempProfilePhoto;
        [Tooltip("플레이어(나)의 깃발 사진 — 임시(사용자 10/23). 정사각이 아니어도 된다(칸 안에 비율을 지켜 맞춘다)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _playerProfilePhoto;
        [Tooltip("지도에서 침략할 수 있는 땅의 표시 자리에 뜨는 칼(UI/WorldMapView) — 「침략」 버튼과 같은 그림, 원본 크기")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _invadeMark;
        [Tooltip("지도 반란 — 들고일어난 백성 무리(UI/WorldLords.Rebel). 흰색, 막대 끝에 대화재의 불길을 얹어 횃불이 된다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _mapRebels;
        [Tooltip("지도 영주 소멸 — 사라진 영주 자리에 남는 쓰러진 깃발(UI/WorldLords). 영주 색으로 칠하고 약 1초에 걸쳐 흐려진다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _mapFallenFlag;
        [Tooltip("유령 영주(다른 플레이어의 기록) 표시 — 지도 머리 위 이름 옆(UI/WorldLords). 흰색")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _ghostBadge;
        [Tooltip("국가 위기 장면에서 백성이 든 팻말 — 판 안쪽 검은 칸에 업그레이드 아이콘이 원본 크기로 들어간다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _placard;
        [Tooltip("통일 장면(UI/EventVisitPopup.OpenEnd)에서 내 영주 머리에 내려앉는 왕관(임시 그림, 덮어쓰면 반영)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _eventCrown;
        [Tooltip("나라 소식 장면(UI/EventVisitPopup.BuildNews) 그림(임시 그림, 덮어쓰면 반영) — 이주민 행렬: 이주민들(돌려 쓴다) · 성문 · 통행세 상자")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _newsMigrants = new Sprite[0];
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsGate;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsTollBox;
        [Tooltip("풍년! — 밭 · 볏단 · 수레(피난민 이벤트의 수레와 같은 그림)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsField;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsCart;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsSheaf;
        [Tooltip("대화재 — 물동이 · 불길(장마다 차례로 바뀐다 — 이펙트라 색을 쓴다)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsBucket;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _newsFlame = new Sprite[0];
        [Tooltip("금광 발견 — 금덩이 더미 · 갱도 받침목 틀(굴은 이벤트 방문의 굴 그림)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsGoldPile;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsMineFrame;
        [Tooltip("황충 발생 — 메뚜기(여럿 반복) · 돈자루(명장 방문도 쓴다)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsLocust;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsMoneyBag;
        [Tooltip("대상단 도착 — 상인(공용 인물) · 귀한 상자 · 짐 더미")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsMerchant;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsRareChest;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsBundles;
        [Tooltip("학자 등용 — 학자(공용 인물) · 벼슬 패 · 책")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsScholar;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsTablet;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsBooks;
        [Tooltip("연금술사의 제안 — 연금술사(공용 인물) · 솥 · 연기(장마다 차례로 — 이펙트라 회색)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsAlchemist;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsCauldron;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _newsSmoke = new Sprite[0];
        [Tooltip("건국 기념제 — 폭죽(작게 → 크게, 이펙트라 색을 쓴다) · 선물 상자 · 훈장")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite[] _newsFirework = new Sprite[0];
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsGift;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _newsMedal;

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
        public EventLandDefinition EventOf(EventLandKind kind) => System.Array.Find(_events, e => e != null && e.Kind == kind);
        public int[] NewsTurns => _newsTurns;
        public NationNewsDefinition[] News => _news;
        public int FestivalTurn => _festivalTurn;
        public NationNewsDefinition Festival => _festival;
        public int[] DiplomacyTurns => _diplomacyTurns;
        public string DiplomacyTitle => _diplomacyTitle;
        public string DiplomacyLine => _diplomacyLine;
        public string DiplomacyAccept => _diplomacyAccept;
        public string DiplomacyTruce => _diplomacyTruce;
        public string DiplomacyTrade => _diplomacyTrade;
        public string DiplomacyDecline => _diplomacyDecline;
        // 번호가 이웃한 영주(시작 때 = 이웃한 땅)의 색이 색상환에서 멀어지게 건너뛰며 고른다 — 건너뛰는 폭은 색 수(28)와 서로소여야 전부 한 번씩 돈다
        private const int ColorStride = 11;
        public Color LordColor(int lord) => _lordColors[lord * ColorStride % _lordColors.Length];
        public Sprite NationIcon(BuildingNation nation) => _nationIcons[(int)nation];
        // 설명 글 줄 안 아이콘(UI/InlineIcons) — 칸 순서 = InlineIcons.Kind. 비면 그 낱말엔 아이콘 없음
        public Sprite InlineIcon(InlineIcons.Kind kind) => (int)kind < _inlineIcons.Length ? _inlineIcons[(int)kind] : null;
        public NationShowcase Showcase(BuildingNation nation) => (int)nation < _nationShowcases.Length ? _nationShowcases[(int)nation] : null;
        public Sprite FrameEdge(BuildingNation nation) => (int)nation < _frameEdges.Length ? _frameEdges[(int)nation] : null;
        public Sprite FrameCorner(BuildingNation nation) => (int)nation < _frameCorners.Length ? _frameCorners[(int)nation] : null;
        public Sprite Castle(BuildingNation nation) => (int)nation < _castles.Length ? _castles[(int)nation] : null;
        public Sprite LordBody(BuildingNation nation) => _lordBodies[(int)nation];
        public Sprite TempProfilePhoto => _tempProfilePhoto;
        public Sprite PlayerProfilePhoto => _playerProfilePhoto;
        public Sprite InvadeMark => _invadeMark;
        public Sprite MapRebels => _mapRebels;
        public Sprite MapFallenFlag => _mapFallenFlag;
        public Sprite GhostBadge => _ghostBadge;
        public Sprite Placard => _placard;
        public Sprite EventCrown => _eventCrown;
        public Sprite[] NewsMigrants => _newsMigrants;
        public Sprite NewsGate => _newsGate;
        public Sprite NewsTollBox => _newsTollBox;
        public Sprite NewsField => _newsField;
        public Sprite NewsCart => _newsCart;
        public Sprite NewsSheaf => _newsSheaf;
        public Sprite NewsBucket => _newsBucket;
        public Sprite[] NewsFlame => _newsFlame;
        public Sprite NewsGoldPile => _newsGoldPile;
        public Sprite NewsMineFrame => _newsMineFrame;
        public Sprite NewsLocust => _newsLocust;
        public Sprite NewsMoneyBag => _newsMoneyBag;
        public Sprite NewsMerchant => _newsMerchant;
        public Sprite NewsRareChest => _newsRareChest;
        public Sprite NewsBundles => _newsBundles;
        public Sprite NewsScholar => _newsScholar;
        public Sprite NewsTablet => _newsTablet;
        public Sprite NewsBooks => _newsBooks;
        public Sprite NewsAlchemist => _newsAlchemist;
        public Sprite NewsCauldron => _newsCauldron;
        public Sprite[] NewsSmoke => _newsSmoke;
        public Sprite[] NewsFirework => _newsFirework;
        public Sprite NewsGift => _newsGift;
        public Sprite NewsMedal => _newsMedal;
        public string NationDescription(BuildingNation nation) => (int)nation < _nationDescriptions.Length ? _nationDescriptions[(int)nation] : "";
        public int GoldPerLand => _goldPerLand;
        public int ShopTierMax => _shopTierMax;
        // round는 0부터(턴 − 1) — 그 턴의 상점 등급 단계(기술 연구는 부르는 쪽이 얹는다)
        public int ShopTier(int round) => Mathf.Min(round * _shopTierMax / Mathf.Max(1, _shopTierTurns), _shopTierMax);
        public float CoalitionGoldScale => _coalitionGoldScale;
        public int NewLordShops => _newLordShops;
        public int SpecialSkipGold => _specialSkipGold;
        public float PowerPerGold => _powerPerGold;
        public float GhostShare => _ghostShare;
        // AI 영주의 턴마다 얻는 골드에 곱하는 값(WorldRun.IncomeOf)
        public float DistanceGoldScale(int distance, int turn) => Mathf.Min(_distanceGoldMax, _distanceGoldBase + distance * turn * _distanceGoldPerStep);

        public WorldRules ToRules() => new WorldRules
        {
            LivesStart = _livesStart,
            HappinessStart = _happinessStart,
            HappinessMax = _happinessMax,
            WinHappiness = _winHappiness,
            LossLandSteps = (int[])_lossLandSteps.Clone(),
            TemperWeights = (float[])_temperWeights.Clone(),
            Coalition = _coalition,
            CoalitionLands = _coalitionLands,
            CoalitionWins = _coalitionWins,
            GhostCoalitionWinSteps = (int[])_ghostCoalitionWinSteps.Clone(),
            MaxCoalitions = _maxCoalitions,
            Underdog = _underdog,
            UnderdogGoldPerLand = _underdogGoldPerLand,
            UnderdogGoldMax = _underdogGoldMax,
        };
    }

    // 국가 고르기 화면의 무대 한 벌 — 표시용(규칙은 읽지 않는다)
    [System.Serializable]
    public class NationShowcase
    {
        [Tooltip("국가 기호 — 목록 줄(32px) · 무대 이름 옆(64px). 16px 기호(국가 아이콘)의 ×2 · ×4 원본")]
        public Sprite EmblemSmall;
        public Sprite Emblem;
        [Tooltip("바닥선 위에 왼쪽부터 서는 유닛(내 색으로)")]
        public UnitDefinition[] Units = new UnitDefinition[0];
        [Tooltip("바닥선 오른쪽의 랜드마크(128×64 · PPU 8 — 무대에서 정수배로)")]
        public Sprite Landmark;
        [Tooltip("무대 뒤 먼 산 실루엣(전장 배경 그림 — 어둡게 깔린다)")]
        public Sprite Backdrop;
        [Tooltip("바닥선 아래 빌드 갈래(2~3개)")]
        public NationPath[] Paths = new NationPath[0];
    }

    [System.Serializable]
    public class NationPath
    {
        [Tooltip("대표 기물 — 아이콘을 쓴다")]
        public BuildingDefinition Building;
        [Tooltip("기물기획 §8 흐름 이름(「킹덤 – 」 뒤)")]
        public string Name;
        [Tooltip("한 줄 요약(~합니다.체)")]
        public string Line;
    }
}

using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 이벤트 땅 한 종류의 데이터(아웃게임기획 §3-1 이벤트 표) — 이름 · 아이콘 · 툴팁 글 · 수치. 규칙(후보를 뽑고 효과를 거는 것)은 RunState(Run/RunState.Events)에 있다.
    // 종류마다 쓰는 칸만 쓴다 — 칸 이름 앞의 종류 이름을 볼 것. WorldConfig 「이벤트 땅」이 종류마다 하나씩 든다
    [CreateAssetMenu(menuName = "GnorpWar/Event Land")]
    public class EventLandDefinition : ScriptableObject
    {
        [SerializeField] private EventLandKind _kind;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _icon;
        [Tooltip("큰 아이콘(64×64) — 왼쪽 패널 초상화")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _iconLarge;
        [Tooltip("방문 장면의 소품 — 물레방아 · 피난민(고른 탑 층이 그 위로 간다). 다른 종류는 방문 장면(UI/EventVisitPopup)이 제 소품을 든다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _prop;
        [Tooltip("툴팁 글(한 줄 = 한 효과, CLAUDE.md §5-2) — {0} · {1}에 지금 값이 들어간다. 종류마다 무엇이 들어가는지는 RunState.EventText")]
        [HideIf(nameof(HideNonBalance))]
        [TextArea(2, 4)]
        [SerializeField] private string _description = "";

        [Tooltip("후보 수 — 신전(올릴 것이 없을 때 새로 얻을 업그레이드) · 용병 상인 · 훈련장 · 피의 제단 · 선동가. 대장간 · 신전(올리기)은 올릴 수 있는 것 전부")]
        [SuffixLabel("개", true)]
        [SerializeField] private int _choices = 3;
        [Tooltip("신전 — 올릴 것이 없을 때 새로 얻는 공용 업그레이드의 등급")]
        [SerializeField] private BuildingRarity _newRarity = BuildingRarity.Rare;
        [Tooltip("훈련장 — 고른 유닛의 체력 영구히 +N(고정)")]
        [SerializeField] private int _hp = 20;
        [Tooltip("훈련장 — 고른 유닛의 공격력 영구히 +N(고정)")]
        [SerializeField] private int _attack = 2;
        [Tooltip("피의 제단 — 바치는 목숨. 목숨이 이보다 많아야 바칠 수 있다")]
        [SerializeField] private int _lifeCost = 1;
        [Tooltip("선동가 — 고른 AI 영주의 행복도 −N(0이면 그 턴 정산에 반란)")]
        [SerializeField] private int _happinessDrop = 2;
        [Tooltip("보물 창고 — 골드 = 이 값 + 턴 × 아래 값")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _goldBase = 10;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _goldPerTurn = 2;
        [Tooltip("물레방아 — 고른 기물의 쿨다운 영구히 ×N(곱연산)")]
        [SerializeField] private float _cooldownFactor = 0.75f;
        [Tooltip("피난민 — 고른 기물이 소환하는 유닛 수 영구히 +N")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _summons = 1;
        [Tooltip("지형 성소 — 지형마다 얻는 업그레이드(그 업그레이드의 「지형」 칸으로 찾는다)")]
        [SerializeField] private UpgradeDefinition[] _shrines = new UpgradeDefinition[0];

        private bool HideNonBalance => BalanceView.HideNonBalance;

        public EventLandKind Kind => _kind;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public Sprite IconLarge => _iconLarge;
        public Sprite Prop => _prop;
        public int Choices => _choices;
        public BuildingRarity NewRarity => _newRarity;
        public int Hp => _hp;
        public int Attack => _attack;
        public int LifeCost => _lifeCost;
        public int HappinessDrop => _happinessDrop;
        public int GoldAt(int turn) => _goldBase + turn * _goldPerTurn;
        public float CooldownFactor => _cooldownFactor;
        public int Summons => _summons;

        public UpgradeDefinition ShrineOf(MapDefinition terrain) => System.Array.Find(_shrines, upgrade => upgrade != null && upgrade.Terrain == terrain);

        public string Describe(params object[] values) => string.Format(_description, values);
    }
}

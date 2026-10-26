using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 맵 한 종 — 상점에 들어올 때 라운드마다 하나를 뽑는다(RunState.Map). 양 진영에 똑같이 걸린다.
    // 유닛 수치는 전투 시작 때 StatBook에(BattleManager), 기물 쿨다운·발동은 BoardRunner가, 땅 모양은 Ground가 읽는다
    [CreateAssetMenu(menuName = "GnorpWar/Map")]
    public class MapDefinition : ScriptableObject
    {
        // 전장 연출(Fx/TerrainFx)의 종류 — 땅 장식 · 떠다니는 입자(사용자 10/20: 전장 자체가 그 지형으로 보이게)
        public enum TerrainLook { Plain, Forest, Grassland, Ice, Jungle, Swamp }

        // 병종 태그로 고른 유닛의 스탯 하나에 곱한다(기획서 §6 곱연산)
        [System.Serializable]
        public class UnitRule : IUnitFilter, ICategoryFilter
        {
            [Tooltip("이 병종 태그의 유닛만 — 비우면 모든 유닛")]
            [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
            [SerializeField] private UnitStat _stat;
            [SerializeField] private float _multiply = 1f;

            public StatModifier Modifier => new StatModifier(_stat, StatOp.Multiply, _multiply);

            public bool AppliesTo(UnitDefinition unit)
                => _categories.Length == 0 || System.Array.IndexOf(_categories, unit.Category) >= 0;

            public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

            // "궁병의 공격력 ×0.5" · "경기병과 중기병의 이동속도 ×1.5" · "모든 유닛의 이동속도 ×0.7"
            public string Describe()
            {
                string who = _categories.Length == 0 ? "모든 유닛" : _categories[0].DisplayName;
                for (int i = 1; i < _categories.Length; i++)
                    who += ((who[who.Length - 1] - 0xAC00) % 28 != 0 ? "과 " : "와 ") + _categories[i].DisplayName;   // 받침이 있으면 "과"
                return $"{who}의 {Modifier.Describe()}";
            }
        }

        // 땅 모양 — 판마다 이 범위 안에서 언덕·골짜기·고원을 무작위로 놓는다(Ground)
        [System.Serializable]
        public class TerrainStyle
        {
            [Tooltip("지형 덩어리(언덕·골짜기·고원) 수 범위")]
            public Vector2Int FeatureCount = new Vector2Int(2, 4);
            [Tooltip("덩어리 높이(칸) 범위")]
            public Vector2 FeatureHeight = new Vector2(1.5f, 4f);
            [Tooltip("덩어리 폭(칸) 범위")]
            public Vector2 FeatureWidth = new Vector2(12f, 30f);
            [Tooltip("덩어리가 골짜기(아래로 파임)일 확률")]
            [Range(0f, 1f)] public float ValleyChance = 0.3f;
            [Tooltip("언덕이 윗면이 평평한 고원일 확률")]
            [Range(0f, 1f)] public float PlateauChance = 0.25f;
            [Tooltip("잔물결 높이(칸) — 0이면 매끈")]
            public float Roughness = 0.3f;
            [Tooltip("가장 가파른 경사(높이/가로) — 유닛이 걸어 오를 수 있게")]
            public float MaxSlope = 0.55f;
        }

        // 뒷배경 한 겹 — 유닛·탑·땅보다 뒤, 검은 배경 위에 흐린 회색 실루엣을 맵 폭만큼 늘어놓는다(Ground). 충돌 없음
        [System.Serializable]
        public class BackdropLayer
        {
            [Tooltip("이 중에서 하나씩 무작위로(좌우도 무작위로 뒤집는다). 그림은 흰색으로 — 밝기는 아래 값이 정한다")]
            public Sprite[] Sprites = new Sprite[0];
            [Tooltip("밝기 — 흰색의 몇 배인가. 유닛·땅(흰색)이 묻히지 않게 0.25 아래로")]
            [Range(0f, 1f)] public float Brightness = 0.18f;
            [Tooltip("그림 중심 사이 간격(칸) 범위")]
            public Vector2 Spacing = new Vector2(6f, 10f);
            [Tooltip("그림 밑변 높이(칸) 범위 — 평평한 땅 윗면 기준. 음수면 그만큼 땅 속에 묻힌다")]
            public Vector2 Bottom = new Vector2(-3f, -3f);
            [Tooltip("카메라를 따라오는 비율 — 0이면 땅과 같이 움직이고, 클수록 멀리 있는 것처럼 느리게 흐른다")]
            [Range(0f, 1f)] public float Parallax = 0f;
        }

        // 에디터 전용(에셋에 안 쓰인다) — 꺼진 값인 효과 칸은 숨기고, 이걸 켜면 전부 보여 새 효과를 넣을 수 있다
        [ShowInInspector, PropertyOrder(-1), LabelText("꺼진 효과도 보기")]
#pragma warning disable 0649   // 값은 인스펙터(Odin)가 넣는다
        [System.NonSerialized] private bool _showOffEffects;
#pragma warning restore 0649
        private bool ShowUnitRules => _showOffEffects || _unitRules.Length > 0;
        private bool ShowCooldownRate => _showOffEffects || _cooldownRate != 1f;
        private bool ShowFireAllInterval => _showOffEffects || _fireAllInterval > 0f;
        private bool ShowPassiveBuildingsOff => _showOffEffects || _passiveBuildingsOff;
        private bool ShowFireRepeats => _showOffEffects || _fireRepeats > 1;

        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
        [Tooltip("아래 수치에서 나오는 줄이 하나도 없을 때만 보이는 글(기본 맵) — 수치가 있는 맵의 글은 코드가 만든다(Description)")]
        [HideIf(nameof(HideNonBalance))]
        [TextArea] [SerializeField] private string _description = "";
        [Tooltip("맵 이름 앞 아이콘(32×32) — 상점 맵 칸 · 전투 중 맵 이름 · 월드 지도의 땅 표시")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _icon;
        [Tooltip("전장에 그리는 지형 연출(Fx/TerrainFx) — 땅 장식(풀 · 덩굴 · 물웅덩이 …) · 떠다니는 입자(눈발 · 낙엽 …)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private TerrainLook _look;
        [Tooltip("월드 지도에서 이 지형인 남의 땅 속을 칠하는 색(UI/WorldMapView — 사용자 10/26 「지형색 바탕 + 영주 색 띠」). 침략할 수 없는 땅은 이보다 어둡게")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Color _mapColor = new Color32(156, 140, 106, 255);

        [TitleGroup("유닛 — 전투 동안 양 진영에"), ShowIf(nameof(ShowUnitRules))]
        [SerializeField] private UnitRule[] _unitRules = new UnitRule[0];

        [Tooltip("쿨다운 게이지가 차는 속도 배율(가속과 곱한다) — 0.5 = 절반 속도")]
        [TitleGroup("기물 — 양 진영에"), ShowIf(nameof(ShowCooldownRate))]
        [SerializeField] private float _cooldownRate = 1f;
        [Tooltip("이 간격(초)마다 쿨다운이 있는 기물을 전부 발동 — 0이면 없음")]
        [SuffixLabel("초", true)]
        [TitleGroup("기물 — 양 진영에"), ShowIf(nameof(ShowFireAllInterval))]
        [SerializeField] private float _fireAllInterval = 0f;
        [Tooltip("「전부 발동」의 출발점으로 양쪽 성 근처에 떠 있는 그림(얼음 덩이) — 버프 선이 여기서 나간다. 비우면 탑 꼭대기에서")]
        [TitleGroup("기물 — 양 진영에"), ShowIf(nameof(ShowFireAllInterval)), HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _fireAllMarker;
        [Tooltip("쿨다운이 없는 기물은 전투에서 효과가 없다")]
        [TitleGroup("기물 — 양 진영에"), ShowIf(nameof(ShowPassiveBuildingsOff))]
        [SerializeField] private bool _passiveBuildingsOff;
        [Tooltip("쿨다운이 있는 기물이 한 번 발동할 때 효과를 몇 번 내나")]
        [TitleGroup("기물 — 양 진영에"), ShowIf(nameof(ShowFireRepeats))]
        [SerializeField] private int _fireRepeats = 1;

        [Header("지형")]
        [SerializeField] private TerrainStyle _terrain = new TerrainStyle();

        [Header("뒷배경 — 맵이 무엇인지 보이게(사용자 10/10: 전장이 아니라 뒤에 흐리게). 앞에 적은 겹이 더 뒤에 그려진다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private BackdropLayer[] _backdrop = new BackdropLayer[0];

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public TerrainLook Look => _look;
        public Color MapColor => _mapColor;
        // 설명 글 — 아래 수치 필드에서 만든다(값을 고치면 글이 따라간다, CLAUDE.md §5-2 6번). 수치로 나오는 줄이 없는 맵(기본)만 _description
        public string Description
        {
            get
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (UnitRule rule in _unitRules)
                    lines.Add(rule.Describe());
                if (_fireAllInterval > 0f)
                    lines.Add($"{_fireAllInterval:0.#}초마다 쿨다운이 있는 모든 기물을 발동합니다.");
                // 게이지가 차는 속도 2배 = 쿨다운 ×0.5
                if (_cooldownRate != 1f)
                    lines.Add($"모든 기물의 쿨다운 ×{1f / _cooldownRate:0.##}");
                if (_passiveBuildingsOff)
                    lines.Add("쿨다운이 없는 기물은 작동하지 않습니다.");
                if (FireRepeats > 1)
                    lines.Add($"쿨다운이 있는 기물은 발동할 때마다 효과를 {FireRepeats}번 냅니다.");
                return lines.Count > 0 ? string.Join("\n", lines) : _description;
            }
        }
        public UnitRule[] UnitRules => _unitRules;
        public float CooldownRate => _cooldownRate;
        public float FireAllInterval => _fireAllInterval;
        public Sprite FireAllMarker => _fireAllMarker;
        public bool PassiveBuildingsOff => _passiveBuildingsOff;
        public int FireRepeats => Mathf.Max(1, _fireRepeats);
        public TerrainStyle Terrain => _terrain;
        public BackdropLayer[] Backdrop => _backdrop;
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 건물 하나 — 쿨다운마다 발동한다. 발동하면 ① 유닛 생산(유닛 수는 등급과 무관 — 등급은 생산 기물의 쿨다운을 줄인다, CooldownAt) ② 액션들(차지·쿨감 …) ③ 켜 둔 효과 스택 +1.
    // 효과(Effects)는 이 건물이 필드에 있으면 전투 시작부터 이 진영 유닛에게 켜진다
    [CreateAssetMenu(menuName = "GnorpWar/Building")]
    public class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "";
        [Tooltip("칸·진열 카드에 그리는 그림 — 흰색으로 그리고 칸에서 색을 입힌다")]
        [SerializeField] private Sprite _icon;
        [Tooltip("생산·액션으로 못 쓰는 발동 효과 — 툴팁에서 \"N초마다\" 뒤에 붙는 구절 (CLAUDE.md §5-2)")]
        [TextArea]
        [SerializeField] private string _description = "";
        [Tooltip("가격·상점 출현 확률은 등급에서 나온다 — BattleConfig")]
        [SerializeField] private BuildingRarity _rarity;
        [SerializeField] private BuildingNation _nation;
        [Tooltip("공용은 None")]
        [SerializeField] private BuildingFaction _faction;
        [Tooltip("발동 간격(초) — 전투 시작 후 이 시간이 지나 첫 발동. 0 = 쿨다운 없음(발동하지 않고 늘 켜진 효과만)")]
        [SerializeField] private float _cooldown = 5f;

        [Header("생산")]
        [Tooltip("비우면 생산하지 않는 건물(유틸)")]
        [SerializeField] private UnitDefinition _unit;
        [Tooltip("한 번에 나오는 수 — 등급이 올라도 그대로(등급은 쿨다운을 줄인다, BattleConfig)")]
        [SerializeField] private int _unitCount = 1;

        [Header("유틸")]
        [SerializeField] private BuildingAction[] _actions = new BuildingAction[0];
        [Tooltip("전투 시작부터 이 진영에 켜지는 유닛 효과 — 발동마다 스택 +1")]
        [SerializeField] private UnitEffect[] _effects = new UnitEffect[0];
        [Tooltip("늘 켜진 기물 능력 — 전투 시작·인접 기물 소환 등")]
        [SerializeField] private BuildingPassive[] _passives = new BuildingPassive[0];

        [Header("툴팁")]
        [Tooltip("툴팁 글에 이름이 나오는 유닛 — 나오는 순서대로(소환 유닛 포함). 기물 판 아래로 유닛 판을 이 순서로 쌓는다. 병종 태그는 넣지 않는다")]
        [SerializeField] private UnitDefinition[] _mentionedUnits = new UnitDefinition[0];

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public BuildingRarity Rarity => _rarity;
        public BuildingNation Nation => _nation;
        public BuildingFaction Faction => _faction;
        public float Cooldown => _cooldown;
        public UnitDefinition Unit => _unit;
        // 에셋에 적힌 한 번 소환 수(전투 가속분 없이) — 전투 밖 계산(World/TowerPower)
        public int UnitCount => _unitCount;
        // 지금 한 번 발동에 소환하는 수(전투 가속분 포함) — 발동(BoardRunner)과 툴팁·칸 숫자가 같이 읽는다
        public int SummonCount => _unitCount + BattleManager.ExtraUnits;

        // 그 등급의 원래 쿨다운 — 생산 기물은 등급마다 준다(BattleConfig, 8 → 6.4 → 5.12), 그 밖의 기물은 Cooldown 그대로.
        // 풍차·가속·맵 배율은 이 값 위에 곱해진다. 발동(BoardRunner)과 툴팁이 같이 읽는다
        public float CooldownAt(int level)
            => _unit == null || BattleManager.Config == null ? _cooldown : _cooldown * BattleManager.Config.RankCooldownScale(level);

        public BuildingAction[] Actions => _actions;
        public UnitEffect[] Effects => _effects;
        public BuildingPassive[] Passives => _passives;
        public UnitDefinition[] MentionedUnits => _mentionedUnits;
    }
}

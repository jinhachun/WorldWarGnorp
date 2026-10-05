using UnityEngine;

namespace GnorpWar
{
    // 건물 하나 — 쿨다운마다 발동한다. 발동하면 ① 유닛 생산(유닛 수 × 레벨) ② 액션들(차지·쿨감 …) ③ 켜 둔 효과 스택 +1.
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
        [Tooltip("발동 간격(초) — 전투 시작 후 이 시간이 지나 첫 발동")]
        [SerializeField] private float _cooldown = 5f;

        [Header("생산")]
        [Tooltip("비우면 생산하지 않는 건물(유틸)")]
        [SerializeField] private UnitDefinition _unit;
        [Tooltip("Lv1 한 번에 나오는 수 — 실제 = 이 값 × 레벨")]
        [SerializeField] private int _unitCount = 1;

        [Header("유틸")]
        [SerializeField] private BuildingAction[] _actions = new BuildingAction[0];
        [Tooltip("전투 시작부터 이 진영에 켜지는 유닛 효과 — 발동마다 스택 +1")]
        [SerializeField] private UnitEffect[] _effects = new UnitEffect[0];

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public BuildingRarity Rarity => _rarity;
        public float Cooldown => _cooldown;
        public UnitDefinition Unit => _unit;
        public int UnitCount => _unitCount;
        public BuildingAction[] Actions => _actions;
        public UnitEffect[] Effects => _effects;
    }
}

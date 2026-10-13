using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 궁병이 내 타워들의 효과를 함께 받는다 (화살 강화기 신화 — 기획서 §7-1).
    // 타워 스탯 변경(공격력 · 공격속도 · 사거리 · 화살 수 · 체력 — 전투 동안 · 영구히 전부)이 그 유닛에게 같은 방식으로 얹힌다. 넘기는 곳은 TowerBook.ShareWithUnits
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower Effects To Units")]
    public class TowerEffectsToUnitsPassive : BuildingPassive, IUnitFilter, ICategoryFilter
    {
        [Tooltip("타워 효과를 함께 받는 병종")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];

        public bool AppliesTo(UnitDefinition unit) => System.Array.IndexOf(_categories, unit.Category) >= 0;
        public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

        public override void OnBattleStart(BoardRunner board, int slot, int level) => TowerBook.ShareWithUnits(board.Team, this);

        public override string Describe(int level)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (UnitCategory category in _categories)
                names.Add(category.DisplayName);
            return $"내 {string.Join("·", names)}이 내 타워들의 효과를 함께 받습니다.";
        }
    }
}

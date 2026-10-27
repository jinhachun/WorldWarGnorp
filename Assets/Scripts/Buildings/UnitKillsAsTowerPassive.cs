using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 유닛(고른 병종)의 처치가 내 타워들의 「처치할 때마다」 효과도 일으킨다 — 내 본진이 처치한 것으로 친다(Base.NotifyKill:
    // 기물의 처치 훅 · 유닛 효과의 타워 처치 훅 · 처치 시 추가 화살은 본진에서). 전투 동안 (사격 통제소 신화 — 사용자 10/28)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Unit Kills As Tower")]
    public class UnitKillsAsTowerPassive : BuildingPassive, IUnitFilter, ICategoryFilter
    {
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];

        public bool AppliesTo(UnitDefinition unit) => System.Array.IndexOf(_categories, unit.Category) >= 0;
        public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

        public override void OnBattleStart(BoardRunner board, int slot, int level) => TowerBook.CountKillsAsTower(board.Team, this);

        public override string Describe(int level)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (UnitCategory category in _categories)
                names.Add(category.DisplayName);
            return $"{string.Join("·", names)}의 처치도 타워 처치로 셉니다.";
        }
    }
}

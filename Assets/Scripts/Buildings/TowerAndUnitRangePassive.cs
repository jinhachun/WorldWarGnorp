using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 전투 동안 내 타워들과 내 유닛(고른 병종)의 사거리 +N% (감시 망원경: 내 궁병, +10% · 등급마다 +5%p — 사용자 10/28).
    // 타워는 TowerBook 전투 동안 몫, 유닛은 TowerBook.AddUnitRange. 사격 통제소가 있으면 궁병은 타워 몫의 비율만큼을 한 번 더 받는다(타워 사거리가 오른 것이므로)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower And Unit Range")]
    public class TowerAndUnitRangePassive : BuildingPassive, IUnitFilter, ICategoryFilter
    {
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [Tooltip("합연산(0.1 = +10%), 시작 등급")]
        [SerializeField] private float _percent = 0.1f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _percentPerTier = 0.05f;

        public bool AppliesTo(UnitDefinition unit) => System.Array.IndexOf(_categories, unit.Category) >= 0;
        public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

        public float PercentAt(int level) => OwnedBuilding.Scaled(_percent, _percentPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            float percent = PercentAt(level);
            TowerBook.AddForBattle(board.Team, new TowerModifier(TowerStat.Range, StatOp.Percent, percent));
            TowerBook.AddUnitRange(board.Team, this, percent);
            BuffLinks.ToTowers(board.Team, slot, BuffKind.Stat, BuffLabel.Of(new TowerModifier(TowerStat.Range, StatOp.Percent, percent)));
        }

        public override string Describe(int level)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (UnitCategory category in _categories)
                names.Add(category.DisplayName);
            return $"타워와 {string.Join("·", names)} 사거리 {new TowerModifier(TowerStat.Range, StatOp.Percent, PercentAt(level)).ValueText}";
        }

        public override string Headline(int level) => $"{PercentAt(level) * 100f:0.##}%";
    }
}

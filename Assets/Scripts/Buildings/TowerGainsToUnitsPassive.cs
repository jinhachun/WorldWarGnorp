using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 타워들의 스탯이 오를 때마다 내 유닛(고른 병종)의 같은 스탯도 그 비율만큼 오른다 — 전투 동안
    // (사격 통제소: 내 궁병, 희귀 60% · 영웅 80% · 전설 100% — 사용자 10/28). 전투 중 오른 몫과 전투 전에 이미 오른 몫(영구히)을 둘 다 센다.
    // 같은 이름의 스탯만 — 공격력 · 공격속도 · 체력은 유닛 스탯에, 사거리 · 한 번에 쏘는 화살 수는 그 유닛의 사거리 · 발 수에 얹힌다(TowerBook.ShareWithUnits).
    // 화살 강화기 신화(100%)와 함께면 비율이 더해진다 — 기물마다 따로(사용자 결정)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower Gains To Units")]
    public class TowerGainsToUnitsPassive : BuildingPassive, IUnitFilter, ICategoryFilter
    {
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [Tooltip("시작 등급의 비율(0.6 = 60%)")]
        [SerializeField] private float _ratio = 0.6f;
        [Tooltip("등급이 오를 때마다 비율에 더한다")]
        [SerializeField] private float _ratioPerTier = 0.2f;

        public bool AppliesTo(UnitDefinition unit) => System.Array.IndexOf(_categories, unit.Category) >= 0;
        public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

        public float RatioAt(int level) => OwnedBuilding.Scaled(_ratio, _ratioPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            TowerBook.ShareWithUnits(board.Team, this, RatioAt(level));
            BuffLinks.ToPoint(board.Team, slot, board.SpawnPoint, BuffKind.Stat);   // 이 층 → 궁병이 나오는 성문 앞(전투 시작엔 아직 유닛이 없다)
        }

        public override string Describe(int level)
            => $"타워 스탯이 오르면 {Names()}도 그 {RatioAt(level) * 100f:0.##}%만큼 오릅니다.";

        public override string Headline(int level) => $"{RatioAt(level) * 100f:0.##}%";

        private string Names()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (UnitCategory category in _categories)
                names.Add(category.DisplayName);
            return string.Join(" · ", names);
        }
    }
}

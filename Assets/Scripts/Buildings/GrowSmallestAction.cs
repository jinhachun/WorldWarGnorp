using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 이 병종 중 가장 작은 아군의 크기를 키운다 (순례지: 중보병 +10%)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Grow Smallest")]
    public class GrowSmallestAction : BuildingAction
    {
        [SerializeField] private UnitCategory _category;
        [Tooltip("기준 크기 대비 — 0.1 = +10%")]
        [SerializeField] private float _amount = 0.1f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _amountPerTier = 0.1f;

        private float Amount(int level) => OwnedBuilding.Scaled(_amount, _amountPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Unit smallest = null;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team && UnitAliases.Is(unit.Team, unit.Definition, _category) && (smallest == null || unit.Size < smallest.Size))
                    smallest = unit;
            if (smallest != null)
            {
                smallest.Grow(Amount(level));
                BuffLinks.ToPoint(board.Team, slot, smallest.Position, BuffKind.Stat);
            }
        }

        public override string Describe(int level) => $"가장 작은 {_category.DisplayName} 크기 +{Amount(level) * 100f:0.#}%";
        public override string Headline(int level) => $"+{Amount(level) * 100f:0.#}%";
    }
}

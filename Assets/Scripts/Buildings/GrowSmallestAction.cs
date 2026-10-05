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

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Unit smallest = null;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team && unit.Definition.Category == _category && (smallest == null || unit.Size < smallest.Size))
                    smallest = unit;
            if (smallest != null)
                smallest.Grow(_amount);
        }

        public override string Describe(int level) => $"가장 작은 {_category.DisplayName}의 크기 +{_amount * 100f:0.#}%";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 모든 아군이 최대 체력의 일정 비율을 회복한다 (종탑)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Heal All")]
    public class HealAllAction : BuildingAction
    {
        [Tooltip("최대 체력 대비 — 0.05 = 5%")]
        [SerializeField] private float _ratio = 0.05f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _ratioPerTier = 0.05f;

        private float Ratio(int level) => OwnedBuilding.Scaled(_ratio, _ratioPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            float ratio = Ratio(level);
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team)
                {
                    unit.Heal(unit.MaxHp * ratio);
                    sum += unit.Position;
                    count++;
                }
            if (count > 0)
                BuffLinks.ToPoint(board.Team, slot, sum / count, BuffKind.Heal);
        }

        public override string Describe(int level) => $"모든 아군이 최대 체력의 {Ratio(level) * 100f:0.#}%를 회복합니다.";
        public override string Headline(int level) => $"{Ratio(level) * 100f:0.#}%";
    }
}

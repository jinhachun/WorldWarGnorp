using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 모든 적 유닛이 최대 체력의 일정 비율을 잃는다 (종탑 신화 — 기획서 §7-1) — HealAllAction의 거울.
    // 「잃습니다」는 피해가 아니다 — 이것만으로는 죽지 않는다(승천의 전당과 같은 규칙, Unit.LoseMaxHpRatio)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Lose Hp All")]
    public class LoseHpAllAction : BuildingAction
    {
        [Tooltip("최대 체력 대비 — 0.05 = 5%")]
        [SerializeField] private float _ratio = 0.05f;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team != board.Team)
                    unit.LoseMaxHpRatio(_ratio);
            BuffLinks.ToUnitsOf(board.Team, slot, board.Team == Team.Ally ? Team.Enemy : Team.Ally, null, BuffKind.Debuff);
        }

        public override string Describe(int level) => $"모든 적이 최대 체력의 {_ratio * 100f:0.#}%를 잃습니다.";
    }
}

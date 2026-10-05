using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 모든 아군이 최대 체력의 일정 비율을 회복한다 (종탑)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Heal All")]
    public class HealAllAction : BuildingAction
    {
        [Tooltip("최대 체력 대비 — 0.05 = 5%")]
        [SerializeField] private float _ratio = 0.05f;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team)
                    unit.Heal(unit.MaxHp * _ratio);
        }

        public override string Describe(int level) => $"모든 아군이 최대 체력의 {_ratio * 100f:0.#}%를 회복합니다.";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 내 타워(본진)가 최대 체력의 일정 비율을 회복한다 (성채)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Heal Tower")]
    public class HealTowerAction : BuildingAction
    {
        [Tooltip("최대 체력 대비 — 0.1 = 10%")]
        [SerializeField] private float _ratio = 0.1f;

        public override void Execute(BoardRunner board, int slot, int level) => Base.For(board.Team)?.HealRatio(_ratio);

        public override string Describe(int level) => $"내 타워가 최대 체력의 {_ratio * 100f:0.#}%를 회복합니다.";
    }
}

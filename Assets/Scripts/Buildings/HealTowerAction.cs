using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 내 타워 전부(본진 + 작은 타워)가 최대 체력의 일정 비율을 회복한다 (성채)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Heal Tower")]
    public class HealTowerAction : BuildingAction
    {
        [Tooltip("최대 체력 대비 — 0.1 = 10%")]
        [SerializeField] private float _ratio = 0.1f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _ratioPerTier = 0.05f;

        private float Ratio(int level) => OwnedBuilding.Scaled(_ratio, _ratioPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Base.HealTowers(board.Team, Ratio(level));
            BuffLinks.ToTowers(board.Team, slot, BuffKind.Stat);
        }

        public override string Describe(int level) => $"내 타워들이 최대 체력의 {Ratio(level) * 100f:0.#}%를 회복합니다.";
        public override string Headline(int level) => $"{Ratio(level) * 100f:0.#}%";
    }
}

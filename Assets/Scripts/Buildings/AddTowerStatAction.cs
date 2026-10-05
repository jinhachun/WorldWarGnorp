using UnityEngine;

namespace GnorpWar
{
    // 발동할 때마다 전투 동안 내 타워(본진)의 스탯을 올린다 (화살 강화기)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Add Tower Stat")]
    public class AddTowerStatAction : BuildingAction
    {
        [SerializeField] private TowerModifier _modifier = new TowerModifier(TowerStat.Attack, StatOp.Percent, 0.05f);

        public override void Execute(BoardRunner board, int slot, int level) => TowerBook.AddForBattle(board.Team, _modifier);

        public override string Describe(int level) => $"전투 동안 내 타워의 {_modifier.Name} {_modifier.ValueText}";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 발동할 때마다 전투 동안 내 타워(본진)의 스탯을 올린다 (화살 강화기)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Add Tower Stat")]
    public class AddTowerStatAction : BuildingAction
    {
        [SerializeField] private TowerModifier _modifier = new TowerModifier(TowerStat.Attack, StatOp.Percent, 0.05f);
        [Tooltip("등급이 오를 때마다 _modifier 값에 더한다")]
        [SerializeField] private float _perTier = 0.05f;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            TowerModifier modifier = _modifier.Scaled(_perTier, level);
            TowerBook.AddForBattle(board.Team, modifier);
            board.AddBattleGain(slot, modifier.Op, modifier.Value);
            BuffLinks.ToTowers(board.Team, slot, BuffKind.Stat, BuffLabel.Of(modifier));
        }

        public override bool BattleGain(out StatOp op)
        {
            op = _modifier.Op;
            return true;
        }

        public override string Describe(int level) => $"전투 동안 내 타워들의 {_modifier.Name} {_modifier.Scaled(_perTier, level).ValueText}";
        public override string Headline(int level) => _modifier.Scaled(_perTier, level).ValueText;
    }
}

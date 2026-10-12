using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 전투 동안 적 유닛 전부의 스탯을 바꾼다 (풍차 신화: 적 유닛의 이동속도 ×0.5 — 기획서 §7-1).
    // 유닛 효과(UnitEffect)는 켠 진영의 유닛에게만 돌므로, 상대 진영의 「전투 동안」 스탯 목록(StatBook)에 직접 건다 — 나중에 나오는 적도 받는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Enemy Stat")]
    public class EnemyStatPassive : BuildingPassive, IUnitFilter
    {
        [SerializeField] private StatModifier _modifier = new StatModifier(UnitStat.MoveSpeed, StatOp.Multiply, 0.5f);

        public bool AppliesTo(UnitDefinition unit) => true;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
            => StatBook.AddForBattle(board.Team == Team.Ally ? Team.Enemy : Team.Ally, this, _modifier);

        public override string Describe(int level) => $"적 유닛의 {_modifier.Describe()}";
    }
}

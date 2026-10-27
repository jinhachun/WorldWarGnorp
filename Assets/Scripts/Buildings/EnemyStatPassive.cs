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
        {
            Team foe = board.Team == Team.Ally ? Team.Enemy : Team.Ally;
            StatBook.AddForBattle(foe, this, _modifier);
            // 이 층 → 적 유닛이 나오는 적 성문 앞(디버프) — 전투 시작엔 아직 적 유닛이 없다
            if (BoardRunner.For(foe) is BoardRunner foeBoard)
                BuffLinks.ToPoint(board.Team, slot, foeBoard.SpawnPoint, BuffKind.Debuff);
        }

        public override string Describe(int level) => $"적 {_modifier.Describe()}";
    }
}

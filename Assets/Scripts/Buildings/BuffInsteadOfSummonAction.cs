using UnityEngine;

namespace GnorpWar
{
    // 그 유닛이 내 진영에 이미 살아 있으면, 이 기물은 유닛을 소환하는 대신 전투 동안 그 유닛 전부에게 스탯을 건다
    // (대족장의 분노: 45초마다 미노타우로스 1기 소환, 내 미노타우로스가 이미 있으면 대신 모든 내 미노타우로스의 최대 체력 +100% — 트라이브 명세 §4 · §5).
    // 판정은 발동 순간이다 — 소환을 막는 곳은 BoardRunner.Fire(ReplacesSummon), 스탯을 거는 곳은 Execute
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Buff Instead Of Summon")]
    public class BuffInsteadOfSummonAction : BuildingAction
    {
        [Tooltip("이 유닛이 이미 있으면 소환 대신 강화 — 기물이 소환하는 유닛과 같은 것을 넣는다")]
        [SerializeField] private UnitDefinition _unit;
        [SerializeField] private StatModifier _modifier = new StatModifier(UnitStat.MaxHp, StatOp.Percent, 1f);

        // 이번 발동에서 소환을 대신했나(진영별) — ReplacesSummon이 정하고 바로 뒤의 Execute가 읽는다
        [System.NonSerialized] private readonly bool[] _replaced = new bool[2];

        public override bool ReplacesSummon(BoardRunner board, int slot)
        {
            bool any = false;
            foreach (Unit other in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                any |= other.IsAlive && other.Team == board.Team && other.Definition == _unit;
            return _replaced[(int)board.Team] = any;
        }

        public override void Execute(BoardRunner board, int slot, int level)
        {
            if (!_replaced[(int)board.Team])
                return;
            foreach (Unit other in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (other.IsAlive && other.Team == board.Team && other.Definition == _unit)
                    other.AddModifier(_modifier);
            BuffLinks.ToUnitsOf(board.Team, slot, board.Team, u => u.Definition == _unit, BuffKind.Stat, BuffLabel.Of(_modifier));
        }

        public override string Describe(int level)
            => $"{_unit.DisplayName}가 이미 있으면 소환 대신\n전투 동안 모든 {_unit.DisplayName} {_modifier.Describe()}";
    }
}

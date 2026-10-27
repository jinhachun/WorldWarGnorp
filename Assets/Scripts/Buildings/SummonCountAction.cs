using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 기물의 원래 소환 대신 unit을 N기 소환한다 (이동식 공장 신화 — 톱니병사 3기, 사용자 10/20). 징병제(ExtraSummons) · 제물의 의식(Slot.ExtraSummons)은 그대로 더한다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Summon Count")]
    public class SummonCountAction : BuildingAction
    {
        [SerializeField] private UnitDefinition _unit;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 3;

        public override bool ReplacesSummon(BoardRunner board, int slot) => true;

        public override void Execute(BoardRunner board, int slot, int level) => board.Summon(_unit, _count + board.ExtraSummons + board.Slots[slot].ExtraSummons + board.Slots[slot].Building.ExtraSummons, slot);

        public override string Describe(int level) => $"이 기물이 한 번에 소환하는 {_unit.DisplayName}가 {_count}기가 됩니다.";
    }
}

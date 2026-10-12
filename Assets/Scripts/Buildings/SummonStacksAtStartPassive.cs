using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 때 이 기물에 쌓인 스택만큼 유닛을 소환한다 (노동운동가 스패너 신화 — 기획서 §7-1).
    // 스택은 이 기물(OwnedBuilding)의 것이다 — 리롤로 쌓인다(RerollStackPassive)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Summon Stacks At Start")]
    public class SummonStacksAtStartPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _unit;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            if (self.Stacks <= 0)
                return;
            board.Summon(_unit, self.Stacks, slot);
            OwnedBuilding.NotifyEffect(self);
        }

        public override string Describe(int level) => $"전투 시작 시, 스택만큼 {_unit.DisplayName}를 소환합니다.";
    }
}

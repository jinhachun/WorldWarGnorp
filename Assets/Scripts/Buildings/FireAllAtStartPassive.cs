using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시 내 모든 기물을 즉시 N회 발동시킨다 (징집 포고문 신화 — 기획서 §7-1).
    // 쿨다운이 없는 기물은 발동하지 않는다(FireNow가 false)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Fire All At Start")]
    public class FireAllAtStartPassive : BuildingPassive
    {
        [SerializeField] private int _times = 1;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);   // 발동된 기물은 BoardRunner.Triggered로
            for (int i = 0; i < _times; i++)
                for (int target = 0; target < board.Slots.Length; target++)
                    if (board.FireNow(target))
                        BuffLinks.ToSlot(board.Team, slot, target, BuffKind.Trigger);
        }

        public override bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated) => BuildingTargets.Cooling(field, other);
        public override string Describe(int level) => $"전투 시작 시, 모든 기물을 {_times}회 발동시킵니다.";
    }
}

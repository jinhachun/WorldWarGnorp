using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시 바로 위·아래 기물들을 즉시 1회 발동시킨다 (징집 포고문)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Fire Neighbors At Start")]
    public class FireNeighborsAtStartPassive : BuildingPassive
    {
        [SerializeField] private int _times = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private int _timesPerTier = 1;

        private int Times(int level) => OwnedBuilding.Scaled(_times, _timesPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);   // 발동된 이웃은 BoardRunner.Triggered로
            for (int i = 0; i < Times(level); i++)
                for (int other = 0; other < board.Slots.Length; other++)
                    if (board.Linked(slot, other, Mathf.Abs(other - slot) == 1) && board.FireNow(other))
                        BuffLinks.ToSlot(board.Team, slot, other, BuffKind.Trigger);
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => Mathf.Abs(other - slot) == 1 && target.Cooldown > 0f;

        public override bool UsesAdjacency => true;
        public override string Describe(int level) => $"전투 시작 시, 바로 위·아래 기물들을 즉시 {Times(level)}회 발동시킵니다.";
        public override string Headline(int level) => $"{Times(level)}회";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시 인접 기물들을 즉시 1회 발동시킨다 (징집 포고문)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Fire Neighbors At Start")]
    public class FireNeighborsAtStartPassive : BuildingPassive
    {
        [SerializeField] private int _times = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private int _timesPerTier = 1;

        private int Times(int level) => OwnedBuilding.Scaled(_times, _timesPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            for (int i = 0; i < Times(level); i++)
            {
                board.FireNow(slot - 1);
                board.FireNow(slot + 1);
            }
        }

        public override string Describe(int level) => $"전투 시작 시, 인접 기물들을 즉시 {Times(level)}회 발동시킵니다.";
        public override string Headline(int level) => $"{Times(level)}회";
    }
}

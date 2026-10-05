using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시 인접 기물들을 즉시 1회 발동시킨다 (징집 포고문)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Fire Neighbors At Start")]
    public class FireNeighborsAtStartPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            board.FireNow(slot - 1);
            board.FireNow(slot + 1);
        }

        public override string Describe(int level) => "전투 시작 시, 인접 기물들을 즉시 1회 발동시킵니다.";
    }
}

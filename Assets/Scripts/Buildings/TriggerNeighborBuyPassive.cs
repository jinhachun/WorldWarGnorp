using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시 인접 기물들의 「구매할 때마다,」 효과를 발동시킨다 (영악한 투자가) — 이 보드의 스팀바론도 적용
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Trigger Neighbor Buy")]
    public class TriggerNeighborBuyPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding[] field = System.Array.ConvertAll(board.Slots, s => s.Building);
            int repeats = BuyRepeats(field);
            foreach (int neighbor in new[] { slot - 1, slot + 1 })
                if (neighbor >= 0 && neighbor < field.Length && !OwnedBuilding.IsEmpty(field[neighbor]))
                    TriggerBuyEffects(field[neighbor], board.Team, repeats);
        }

        public override string Describe(int level) => "전투 시작 시, 인접 기물들의 「구매할 때마다,」 효과를 발동시킵니다.";
    }
}

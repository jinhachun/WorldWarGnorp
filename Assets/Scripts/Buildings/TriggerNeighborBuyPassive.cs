using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시 바로 위·아래 기물들의 「구매할 때마다,」 효과를 발동시킨다 (영악한 투자가) — 이 보드의 스팀바론도 적용
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Trigger Neighbor Buy")]
    public class TriggerNeighborBuyPassive : BuildingPassive
    {
        [Tooltip("발동시키는 횟수")]
        [SerializeField] private int _times = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private int _timesPerTier = 1;

        private int Times(int level) => OwnedBuilding.Scaled(_times, _timesPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);   // 발동된 이웃은 TriggerBuyEffects가 알린다
            OwnedBuilding[] field = System.Array.ConvertAll(board.Slots, s => s.Building);
            int repeats = BuyRepeats(field) * Times(level);
            for (int neighbor = 0; neighbor < field.Length; neighbor++)
                if (board.Linked(slot, neighbor, Mathf.Abs(neighbor - slot) == 1) && !OwnedBuilding.IsEmpty(field[neighbor]))
                {
                    TriggerBuyEffects(field[neighbor], board.Team, repeats);
                    if (HasBuyEffects(field[neighbor].Definition))
                        BuffLinks.ToSlot(board.Team, slot, neighbor, BuffKind.Trigger);
                }
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => Mathf.Abs(other - slot) == 1 && HasBuyEffects(target);

        public override bool UsesAdjacency => true;
        // 1회면 기획서 문구 그대로("발동시킵니다")
        public override string Describe(int level)
            => $"전투 시작 시, 바로 위·아래 기물들의 「구매할 때마다,」 효과를 {(Times(level) > 1 ? $"{Times(level)}회 " : "")}발동시킵니다.";
        public override string Headline(int level) => $"{Times(level)}회";
    }
}

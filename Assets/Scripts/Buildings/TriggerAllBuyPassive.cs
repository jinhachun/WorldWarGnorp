using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시 내 모든 기물의 「구매할 때마다,」 효과를 발동시킨다 (영악한 투자가 신화 — 기획서 §7-1) — 이 보드의 스팀바론도 적용
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Trigger All Buy")]
    public class TriggerAllBuyPassive : BuildingPassive
    {
        [Tooltip("발동시키는 횟수")]
        [SerializeField] private int _times = 1;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);   // 발동된 기물은 TriggerBuyEffects가 알린다
            OwnedBuilding[] field = System.Array.ConvertAll(board.Slots, s => s.Building);
            int repeats = BuyRepeats(field) * _times;
            for (int target = 0; target < field.Length; target++)
                if (!OwnedBuilding.IsEmpty(field[target]))
                {
                    TriggerBuyEffects(field[target], board.Team, repeats);
                    if (target != slot && HasBuyEffects(field[target].Definition))
                        BuffLinks.ToSlot(board.Team, slot, target, BuffKind.Trigger);
                }
        }

        // 1회면 기획서 문구 그대로("발동시킵니다")
        public override string Describe(int level)
            => $"전투 시작 시, 내 모든 기물의 「구매할 때마다,」 효과를 {(_times > 1 ? $"{_times}회 " : "")}발동시킵니다.";
    }
}

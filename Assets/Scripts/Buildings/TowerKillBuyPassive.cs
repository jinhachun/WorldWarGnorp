using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 타워들이 적 N기를 처치할 때마다 내 모든 기물의 「구매할 때마다,」 효과를 발동시킨다 (대지주 스팀바론 — 사용자 10/20). 전투마다 0부터.
    // 발동 길은 영악한 투자가 신화(TriggerAllBuyPassive)와 같다(BuildingPassive.TriggerBuyEffects). 신화 표시(_twiceMark)가 있으면 2번씩
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower Kill Buy")]
    public class TowerKillBuyPassive : BuildingPassive
    {
        [SuffixLabel("기", true)]
        [SerializeField] private int _kills = 50;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 「구매할 때마다,」 효과를 2번씩")]
        [SerializeField] private BuildingPassive _twiceMark;

        // 이번 전투에 센 처치 수(진영별)
        [System.NonSerialized] private readonly int[] _count = new int[2];

        public override void OnBattleStart(BoardRunner board, int slot, int level) => _count[(int)board.Team] = 0;

        public override void OnTowerKill(BoardRunner board, int slot, Unit victim)
        {
            if (++_count[(int)board.Team] < _kills)
                return;
            _count[(int)board.Team] = 0;
            OwnedBuilding self = board.Slots[slot].Building;
            OwnedBuilding.NotifyEffect(self);
            OwnedBuilding[] field = System.Array.ConvertAll(board.Slots, s => s.Building);
            int repeats = (_twiceMark != null && System.Array.IndexOf(self.Passives, _twiceMark) >= 0 ? 2 : 1);
            for (int target = 0; target < field.Length; target++)
                if (!OwnedBuilding.IsEmpty(field[target]))
                {
                    TriggerBuyEffects(field[target], board.Team, repeats);
                    if (target != slot && HasBuyEffects(field[target].Definition))
                        BuffLinks.ToSlot(board.Team, slot, target, BuffKind.Trigger);
                    if (repeats > 1 && HasBuyEffects(field[target].Definition))
                        FloorMultiplier.Show(board.Team, target, repeats);   // 신화 — 2번씩 발동한 층에 「×2」
                }
        }

        // 다음 발동까지 남은 처치 수(설명 창의 회색 줄 · 탑 칸 숫자)
        public int RemainingOf(Team team) => BattleManager.Fighting ? _kills - _count[(int)team] : _kills;   // 전투 밖(상점)에서는 지난 전투의 값이 남아 있으므로 처음 값

        public override string Describe(int level) => $"내 타워들이 적 {_kills}기를 처치할 때마다, 내 모든 기물의 「구매할 때마다,」 효과를 발동시킵니다.";
    }
}

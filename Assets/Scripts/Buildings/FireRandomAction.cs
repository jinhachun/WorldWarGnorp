using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 쿨다운이 있는 다른 내 기물 중 무작위 하나를 즉시 1회 발동시킨다 (판도라의 상자 · 약탈족장 라이칸스로프) — 없으면 아무 일도 없다.
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Fire Random")]
    public class FireRandomAction : BuildingAction
    {
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 고른 기물을 2번 발동 — 라이칸스로프 신화 「즉시 발동시킨 기물은 그 발동의 효과를 2번 냅니다」")]
        [SerializeField] private BuildingPassive _twiceMark;

        private static readonly List<int> Candidates = new List<int>();

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Candidates.Clear();
            for (int i = 0; i < board.Slots.Length; i++)
                if (i != slot && board.Slots[i].Building != null && board.Slots[i].Cooldown > 0f && !FiresOthers(board.Slots[i].Building.Definition))
                    Candidates.Add(i);
            if (Candidates.Count == 0)
                return;
            int pick = Candidates[Random.Range(0, Candidates.Count)];
            int times = _twiceMark != null && System.Array.IndexOf(board.Slots[slot].Building.Passives, _twiceMark) >= 0 ? 2 : 1;
            bool fired = false;
            for (int t = 0; t < times; t++)
                fired |= board.FireNow(pick);
            if (fired)
                BuffLinks.ToSlot(board.Team, slot, pick, BuffKind.Trigger);
            if (fired && times > 1)
                FloorMultiplier.Show(board.Team, pick, times);   // 신화 — 2번 발동한 층에 「×2」
        }

        // 다른 기물을 발동시키는 기물끼리는 고르지 않는다 — 판도라 둘이 서로를 끝없이 발동시킨다
        private static bool FiresOthers(BuildingDefinition building)
        {
            foreach (BuildingAction action in building.Actions)
                if (action is FireRandomAction)
                    return true;
            return false;
        }

        // 무작위 — 고를 수 있는 후보 전부(Execute와 같은 조건)
        public override bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated)
            => BuildingTargets.Cooling(field, other) && !FiresOthers(field[other].Definition);
        public override string Describe(int level) => "다른 기물 1개를 무작위로 발동시킵니다.";
    }
}

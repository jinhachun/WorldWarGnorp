using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 쿨다운이 있는 다른 내 기물 중 무작위 하나를 즉시 1회 발동시킨다 (판도라의 상자) — 없으면 아무 일도 없다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Fire Random")]
    public class FireRandomAction : BuildingAction
    {
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
            if (board.FireNow(pick))
                BuffLinks.ToSlot(board.Team, slot, pick, BuffKind.Trigger);
        }

        // 다른 기물을 발동시키는 기물끼리는 고르지 않는다 — 판도라 둘이 서로를 끝없이 발동시킨다
        private static bool FiresOthers(BuildingDefinition building)
        {
            foreach (BuildingAction action in building.Actions)
                if (action is FireRandomAction)
                    return true;
            return false;
        }

        public override string Describe(int level) => "무작위 내 기물 1개를 즉시 1회 발동시킵니다.";
    }
}

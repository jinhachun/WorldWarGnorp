using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 아래에 있는 모든 기물을 잠시 가속한다 (물레방앗간) — 칸 번호가 작을수록 아래층
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Haste Neighbors")]
    public class HasteNeighborsAction : BuildingAction
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 1f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _secondsPerTier = 1f;

        private float Seconds(int level) => OwnedBuilding.Scaled(_seconds, _secondsPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            for (int i = 0; i < board.Slots.Length; i++)
            {
                if (!board.Linked(slot, i, i < slot))
                    continue;
                board.Haste(i, Seconds(level));
                // 쿨다운 없는 기물은 가속해도 달라지는 게 없다 — 알리지 않는다
                if (board.Slots[i].Building != null && board.Slots[i].Cooldown > 0f)
                    BuffLinks.ToSlot(board.Team, slot, i, BuffKind.Haste);
            }
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => other < slot && target.Cooldown > 0f;

        public override string Describe(int level) => $"아래에 있는 모든 기물을 {Seconds(level):0.#}초간 가속합니다.";
        public override string Headline(int level) => $"{Seconds(level):0.#}초";
    }
}

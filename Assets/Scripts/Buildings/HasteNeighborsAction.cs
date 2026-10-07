using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 인접 기물들을 잠시 가속한다 (물레방앗간)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Haste Neighbors")]
    public class HasteNeighborsAction : BuildingAction
    {
        [SerializeField] private float _seconds = 1f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _secondsPerTier = 1f;

        private float Seconds(int level) => OwnedBuilding.Scaled(_seconds, _secondsPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            board.Haste(slot - 1, Seconds(level));
            board.Haste(slot + 1, Seconds(level));
        }

        public override string Describe(int level) => $"인접 기물들을 {Seconds(level):0.#}초간 가속합니다.";
        public override string Headline(int level) => $"{Seconds(level):0.#}초";
    }
}

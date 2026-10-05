using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 인접 기물들을 잠시 가속한다 (물레방앗간)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Haste Neighbors")]
    public class HasteNeighborsAction : BuildingAction
    {
        [SerializeField] private float _seconds = 1f;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            board.Haste(slot - 1, _seconds);
            board.Haste(slot + 1, _seconds);
        }

        public override string Describe(int level) => $"인접 기물들을 {_seconds:0.#}초간 가속합니다.";
    }
}

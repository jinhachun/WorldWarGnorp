using UnityEngine;

namespace GnorpWar
{
    // [차지] 양옆 건물의 쿨다운을 N초 채워 준다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Charge Neighbors")]
    public class ChargeNeighborsAction : BuildingAction
    {
        [Tooltip("Lv1 차지 초")]
        [SerializeField] private float _seconds = 1f;
        [Tooltip("레벨이 하나 오를 때마다 더하는 초")]
        [SerializeField] private float _secondsPerLevel = 0.5f;

        private float Seconds(int level) => _seconds + _secondsPerLevel * (level - 1);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            float seconds = Seconds(level);
            board.Charge(slot - 1, seconds);
            board.Charge(slot + 1, seconds);
        }

        public override string Describe(int level) => $"양옆 건물을 {Seconds(level):0.#}초 앞당긴다.";
    }
}

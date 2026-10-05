using UnityEngine;

namespace GnorpWar
{
    // 이번 전투 동안 왼쪽 건물의 쿨다운을 줄인다 — 발동마다 지금 쿨다운에 (1 - 감소율)을 곱해 누적
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Reduce Left Cooldown")]
    public class ReduceLeftCooldownAction : BuildingAction
    {
        [Tooltip("Lv1 감소율 (0.1 = 지금 쿨다운 ×0.9)")]
        [SerializeField] private float _ratio = 0.1f;
        [SerializeField] private float _ratioPerLevel = 0.05f;

        private float Ratio(int level) => _ratio + _ratioPerLevel * (level - 1);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            board.ReduceCooldown(slot - 1, Ratio(level));
        }

        public override string Describe(int level) => $"왼쪽 건물의 대기 시간이 {Ratio(level) * 100f:0.#}% 더 줄어든다.";
    }
}

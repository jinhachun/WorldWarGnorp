using UnityEngine;

namespace GnorpWar
{
    // 왼쪽에 있는 모든 기물의 쿨다운에 곱한다 (풍차) — 여러 개면 곱으로 쌓인다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Scale Left Cooldown")]
    public class ScaleLeftCooldownPassive : BuildingPassive
    {
        [SerializeField] private float _factor = 0.8f;
        [Tooltip("등급이 오를 때마다 더한다 — 줄일수록 빨라지므로 음수")]
        [SerializeField] private float _factorPerTier = -0.1f;

        private float Factor(int level) => OwnedBuilding.Scaled(_factor, _factorPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            for (int i = 0; i < slot; i++)
                board.ScaleCooldown(i, Factor(level));
        }

        public override string Describe(int level) => $"왼쪽에 있는 모든 기물의 쿨다운 ×{Factor(level):0.##}";
        public override string Headline(int level) => $"×{Factor(level):0.##}";
    }
}

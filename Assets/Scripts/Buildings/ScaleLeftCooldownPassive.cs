using UnityEngine;

namespace GnorpWar
{
    // 왼쪽에 있는 모든 기물의 쿨다운에 곱한다 (풍차) — 여러 개면 곱으로 쌓인다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Scale Left Cooldown")]
    public class ScaleLeftCooldownPassive : BuildingPassive
    {
        [SerializeField] private float _factor = 0.8f;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            for (int i = 0; i < slot; i++)
                board.ScaleCooldown(i, _factor);
        }

        public override string Describe(int level) => $"왼쪽에 있는 모든 기물의 쿨다운 ×{_factor:0.##}";
    }
}

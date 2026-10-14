using UnityEngine;

namespace GnorpWar
{
    // 아래에 있는 모든 기물의 쿨다운에 곱한다 (풍차) — 여러 개면 곱으로 쌓인다. 칸 번호가 작을수록 아래층
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Scale Left Cooldown")]
    public class ScaleLeftCooldownPassive : BuildingPassive
    {
        [SerializeField] private float _factor = 0.8f;
        [Tooltip("등급이 오를 때마다 더한다 — 줄일수록 빨라지므로 음수")]
        [SerializeField] private float _factorPerTier = -0.1f;

        private float Factor(int level) => OwnedBuilding.Scaled(_factor, _factorPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
            for (int i = 0; i < board.Slots.Length; i++)
            {
                if (!board.Linked(slot, i, i < slot))
                    continue;
                board.ScaleCooldown(i, Factor(level));
                if (board.Slots[i].Building != null)
                    OwnedBuilding.NotifyEffect(board.Slots[i].Building);
                // 늘 켜진 효과 — 지금 한 번, 그 뒤로는 대상이 발동할 때마다 약하게(BuffLinks.Standing). 쿨다운 없는 기물은 받는 게 없다
                if (board.Slots[i].Building != null && board.Slots[i].Cooldown > 0f)
                    BuffLinks.Standing(board.Team, slot, i, BuffKind.Haste);
            }
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => other < slot && target.Cooldown > 0f;

        public override bool UsesAdjacency => true;
        public override string Describe(int level) => $"아래에 있는 모든 기물의 쿨다운 ×{Factor(level):0.##}";
        public override string Headline(int level) => $"×{Factor(level):0.##}";
    }
}

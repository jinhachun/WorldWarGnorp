using UnityEngine;

namespace GnorpWar
{
    // 「구매할 때마다,」 효과가 있는 기물을 구매하면 이 기물의 쿨다운이 영구히 ×M (이동식 공장 — 사용자 10/20).
    // 영구히 = 이 한 채의 쿨다운 배율(OwnedBuilding.ScaleCooldown — 계몽주의 · 라이칸스로프와 같은 길): 팔면 사라지고, 합치면 곱해진다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Buy Effect Bought Cooldown")]
    public class BuyEffectBoughtCooldownPassive : BuildingPassive
    {
        [Tooltip("쿨다운에 곱한다(영구히, 시작 등급)")]
        [SerializeField] private float _factor = 0.98f;
        [Tooltip("등급이 오를 때마다 _factor에 더한다(희귀 0.98 · 영웅 0.97 · 전설 0.96 — 사용자 10/20)")]
        [SerializeField] private float _factorPerTier = -0.01f;

        private float Factor(int level) => OwnedBuilding.Scaled(_factor, _factorPerTier, level);

        public override void OnAnyBought(OwnedBuilding self, OwnedBuilding bought)
        {
            if (!HasBuyEffects(bought.Definition))
                return;
            self.ScaleCooldown(Factor(self.Level));
            OwnedBuilding.NotifyEffect(self);
        }

        public override string Describe(int level) => $"「구매할 때마다,」 효과가 있는 기물을 사면\n이 기물의 쿨다운 영구히 ×{Factor(level):0.##}";
    }
}

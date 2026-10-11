using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 「구매할 때마다,」 효과가 있는 기물을 사면 산 기물 구매 가격의 일정 비율만큼 골드를 얻는다 (주식시장) — 스팀바론으로 2배가 되지 않는다(이 효과는 그 키워드 효과가 아니다)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Gold On Buy Effect")]
    public class GoldOnBuyEffectPassive : BuildingPassive
    {
        [Tooltip("구매 가격의 % — 50 = 절반. 골드는 반올림(.5는 올림)")]
        [SuffixLabel("%", true)]
        [SerializeField] private int _percent = 50;
        [Tooltip("등급이 오를 때마다 더한다(%p)")]
        [SuffixLabel("%p", true)]
        [SerializeField] private int _percentPerTier = 25;

        private int Percent(int level) => OwnedBuilding.Scaled(_percent, _percentPerTier, level);

        public override void OnAnyBought(OwnedBuilding self, OwnedBuilding bought)
        {
            if (HasBuyEffects(bought.Definition))
            {
                RunState.GainGold((RunState.PriceOf(bought.Definition) * Percent(self.Level) + 50) / 100);
                OwnedBuilding.NotifyEffect(self);
            }
        }

        public override string Describe(int level) => $"「구매할 때마다,」 효과가 있는 기물을 구매하면, 구매 가격의 {Percent(level)}%만큼 골드를 얻습니다.";
        public override string Headline(int level) => $"{Percent(level)}%";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 「구매할 때마다,」 효과가 있는 기물을 사면 골드를 얻는다 (주식시장) — 스팀바론으로 2배가 되지 않는다(이 효과는 그 키워드 효과가 아니다)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Gold On Buy Effect")]
    public class GoldOnBuyEffectPassive : BuildingPassive
    {
        [SerializeField] private int _gold = 1;

        public override void OnAnyBought(OwnedBuilding self, OwnedBuilding bought)
        {
            if (HasBuyEffects(bought.Definition))
                RunState.GainGold(_gold);
        }

        public override string Describe(int level) => $"「구매할 때마다,」 효과가 있는 기물을 구매하면, 골드를 {_gold} 얻습니다.";
    }
}

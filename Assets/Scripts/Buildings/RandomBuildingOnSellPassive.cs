using UnityEngine;

namespace GnorpWar
{
    // 「판매 시,」 이번 판에 등장하는 무작위 기물(등급 지정) 1개를 얻는다 — 자기 자신 제외 (판도라의 상자)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Random Building On Sell")]
    public class RandomBuildingOnSellPassive : BuildingPassive
    {
        [SerializeField] private BuildingRarity _rarity = BuildingRarity.Legendary;

        public override void OnSell(OwnedBuilding self)
        {
            var candidates = new System.Collections.Generic.List<BuildingDefinition>();
            foreach (BuildingDefinition b in RunState.Pool)
                if (b.Rarity == _rarity && b != self.Definition)
                    candidates.Add(b);
            if (candidates.Count > 0)
                RunState.Acquire(candidates[Random.Range(0, candidates.Count)]);
        }

        public override string Describe(int level) => $"「판매 시,」 이번 판에 등장하는 무작위 {_rarity.DisplayName()} 기물 1개를 획득합니다. (자기 자신 제외)";
    }
}

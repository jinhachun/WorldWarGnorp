using UnityEngine;

namespace GnorpWar
{
    // 「판매 시,」 이번 판에 등장하는 무작위 기물(등급 지정) 1개를 얻는다 — 자기 자신 제외 (판도라의 상자)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Random Building On Sell")]
    public class RandomBuildingOnSellPassive : BuildingPassive
    {
        [SerializeField] private BuildingRarity _rarity = BuildingRarity.Legendary;

        public override void OnSell(OwnedBuilding self, int floor)
        {
            var candidates = new System.Collections.Generic.List<BuildingDefinition>();
            foreach (BuildingDefinition b in RunState.Pool)
                if (b.Rarity == _rarity && b != self.Definition)
                    candidates.Add(b);
            if (candidates.Count == 0)
                return;
            // 판도라의 상자 신화 — 판 기물에 표시가 켜져 있었으면 얻는 기물이 신화 등급으로 온다(시작 등급에서 신화까지 올려서)
            bool mythic = false;
            foreach (BuildingPassive passive in self.Passives)
                mythic |= passive is AcquireAsMythicPassive;
            BuildingDefinition pick = candidates[Random.Range(0, candidates.Count)];
            RunState.Acquire(pick, mythic ? BuildingRarity.Mythic - pick.Rarity : 0);
        }

        public override string Describe(int level) => $"「판매 시,」 이번 판의 다른 {_rarity.DisplayName()} 기물 1개를 무작위로 얻습니다.";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 가진 기물 한 채 — 기물 종류와 시작 등급에서 몇 번 올랐나만 담는 순수 데이터(나중에 비동기 상대 보드로 저장·전송할 단위).
    // 같은 기물·같은 등급 둘이 모이면 위 등급 하나가 된다(RunState). 전설이 끝
    [System.Serializable]
    public class OwnedBuilding
    {
        [SerializeField] private BuildingDefinition _definition;
        [Tooltip("시작 등급에서 오른 횟수 — 0 = 시작 등급")]
        [SerializeField] private int _upgrades;

        public OwnedBuilding(BuildingDefinition definition)
        {
            _definition = definition;
        }

        public BuildingDefinition Definition => _definition;
        public int Upgrades => _upgrades;
        public BuildingRarity Rarity => (BuildingRarity)((int)_definition.Rarity + _upgrades);
        // 생산 수 배율 — 등급업 효과를 기물마다 정하기 전까지의 임시 규칙
        public int Level => _upgrades + 1;
        // 이 한 채에 합쳐진 시작 등급 기물 수
        public int Copies => 1 << _upgrades;
        public bool CanUpgrade => Rarity < BuildingRarity.Legendary;

        public void Upgrade() => _upgrades++;

        public static bool IsEmpty(OwnedBuilding building) => building == null || building._definition == null;
    }
}

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
        [Tooltip("이 기물이 모은 스택(스패너 등) — 팔면 같이 사라지고, 합쳐지면 더해진다")]
        [SerializeField] private int _stacks;

        public OwnedBuilding(BuildingDefinition definition)
        {
            _definition = definition;
        }

        public BuildingDefinition Definition => _definition;
        public int Upgrades => _upgrades;
        public BuildingRarity Rarity => (BuildingRarity)((int)_definition.Rarity + _upgrades);
        // 1 = 시작 등급. 생산 수는 × Level, 다른 수치는 Scaled로 등급마다 증가량을 더한다
        public int Level => _upgrades + 1;
        // 이 한 채에 합쳐진 시작 등급 기물 수
        public int Copies => 1 << _upgrades;
        public bool CanUpgrade => Rarity < BuildingRarity.Legendary;

        public int Stacks => _stacks;

        public void Upgrade() => _upgrades++;
        public void AddStacks(int count) => _stacks += count;

        // 등급이 오를 때마다 perTier씩 — level 1(시작 등급)이면 value 그대로
        public static float Scaled(float value, float perTier, int level) => value + perTier * (level - 1);
        public static int Scaled(int value, int perTier, int level) => value + perTier * (level - 1);

        public static bool IsEmpty(OwnedBuilding building) => building == null || building._definition == null;
    }
}

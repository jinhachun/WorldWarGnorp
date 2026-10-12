using UnityEngine;

namespace GnorpWar
{
    // 가진 기물 한 채 — 기물 종류와 시작 등급에서 몇 번 올랐나만 담는 순수 데이터(나중에 비동기 상대 보드로 저장·전송할 단위).
    // 같은 기물·같은 등급 둘이 모이면 위 등급 하나가 된다(RunState). 신화가 끝
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
        // 1 = 시작 등급. 생산 기물은 쿨다운이 줄고(BuildingDefinition.CooldownAt), 다른 수치는 Scaled로 등급마다 증가량을 더한다
        public int Level => _upgrades + 1;
        // 이 한 채에 합쳐진 시작 등급 기물 수
        public int Copies => 1 << _upgrades;
        public bool CanUpgrade => Rarity < BuildingRarity.Mythic;

        public int Stacks => _stacks;

        // 지금 등급에 켜진 것 — 신화면 기획서 §7-1의 신화 묶음이 §7 묶음 뒤에 더해진다(대체하지 않는다).
        // 발동·훅·툴팁이 모두 이 셋을 읽는다(기물 정의의 같은 이름 프로퍼티는 시작 등급의 것뿐이다).
        // 매 소환·매 프레임 돌아 합친 배열은 한 번만 만들고 등급이 오를 때 버린다 — 신화가 아니면 정의의 배열을 그대로 돌려줘 할당이 없다
        public BuildingAction[] Actions => _actions ?? (_actions = Plus(_definition.Actions, _definition.MythicActions));
        public UnitEffect[] Effects => _effects ?? (_effects = Plus(_definition.Effects, _definition.MythicEffects));
        public BuildingPassive[] Passives => _passives ?? (_passives = Plus(_definition.Passives, _definition.MythicPassives));

        private BuildingAction[] _actions;
        private UnitEffect[] _effects;
        private BuildingPassive[] _passives;

        private T[] Plus<T>(T[] start, T[] mythic)
        {
            if (Rarity != BuildingRarity.Mythic || mythic.Length == 0)
                return start;
            var merged = new T[start.Length + mythic.Length];
            start.CopyTo(merged, 0);
            mythic.CopyTo(merged, start.Length);
            return merged;
        }

        public void Upgrade()
        {
            _upgrades++;
            _actions = null;   // 등급이 바뀌면 위 세 배열을 다시 만든다(전설 → 신화에서 신화 묶음이 켜진다)
            _effects = null;
            _passives = null;
        }

        public void AddStacks(int count) => _stacks += count;

        // 등급이 오를 때마다 perTier씩 — level 1(시작 등급)이면 value 그대로
        public static float Scaled(float value, float perTier, int level) => value + perTier * (level - 1);
        public static int Scaled(int value, int perTier, int level) => value + perTier * (level - 1);

        // 효과가 터지거나 효과를 받은 순간(UI 칸 튐) — 「구매할 때마다,」·전투 시작 시·상점 훅(리롤·판매·골드).
        // 쿨다운 발동·가속은 BoardRunner.Triggered 쪽이다
        public static event System.Action<OwnedBuilding> EffectFired;
        public static void NotifyEffect(OwnedBuilding building) => EffectFired?.Invoke(building);

        public static bool IsEmpty(OwnedBuilding building) => building == null || building._definition == null;

        // 탑은 중간 층을 비울 수 없다 — 빈 칸을 빼고 아래로 당긴 새 배열(길이 = 층수 그대로). 치트 패널의 편집본처럼 손으로 고친 보드에
        public static OwnedBuilding[] Compacted(OwnedBuilding[] field)
        {
            var result = new OwnedBuilding[field.Length];
            int write = 0;
            foreach (OwnedBuilding building in field)
                if (!IsEmpty(building))
                    result[write++] = building;
            return result;
        }
    }
}

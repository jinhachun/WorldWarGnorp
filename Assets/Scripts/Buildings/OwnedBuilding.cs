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
        [Tooltip("이 한 채를 모으는 데 쓴 골드 — 판매가의 바탕(사용자 결정 10/13). 공짜로 얻은 기물은 0")]
        [SerializeField] private int _spentGold;

        public OwnedBuilding(BuildingDefinition definition)
        {
            _definition = definition;
        }

        // 저장해 둔 한 채를 다시 세운다(Run/TowerSnapshot) — 위 필드 전부를 그대로
        public OwnedBuilding(BuildingDefinition definition, int upgrades, int stacks, int spentGold, float cooldownScale)
        {
            _definition = definition;
            _upgrades = upgrades;
            _stacks = stacks;
            _spentGold = spentGold;
            _cooldownScale = cooldownScale;
        }

        public BuildingDefinition Definition => _definition;
        public int Upgrades => _upgrades;
        public BuildingRarity Rarity => (BuildingRarity)((int)_definition.Rarity + _upgrades);
        // 1 = 시작 등급. 생산 기물은 쿨다운이 줄고(BuildingDefinition.CooldownAt), 다른 수치는 Scaled로 등급마다 증가량을 더한다
        public int Level => LevelOf(_definition, _upgrades);

        // 오른 횟수를 수치용 레벨로 — 신화에서는 수치가 오르지 않으므로(기획서 §1) Rarity가 전설인 선에서 멈춘다.
        // 상한은 시작 등급마다 다르다(일반 시작이면 5 · 전설 시작이면 1). Copies(합친 개수)는 _upgrades를 직접 읽어 계속 늘어난다
        public static int LevelOf(BuildingDefinition definition, int upgrades)
            => Mathf.Min(upgrades, (int)BuildingRarity.Legendary - (int)definition.Rarity) + 1;
        // 쿨다운을 정하는 레벨 — 신화는 수치가 멈추지만 생산 기물의 등급업 쿨다운 배율만은 한 번 더 받는다(사용자 결정 10/13): 신화면 Level + 1.
        // 쿨다운 계산(BuildingDefinition.CooldownAt)에만 쓴다 — 다른 수치는 Level 그대로
        public int CooldownLevel => CooldownLevelOf(_definition, _upgrades);
        public static int CooldownLevelOf(BuildingDefinition definition, int upgrades)
            => LevelOf(definition, upgrades) + ((int)definition.Rarity + upgrades >= (int)BuildingRarity.Mythic ? 1 : 0);
        // 지금 등급의 원래 쿨다운(풍차 · 가속 · 맵 배율을 곱하기 전) — 발동(BoardRunner)과 툴팁이 같이 읽는다
        public float Cooldown => _definition.CooldownAt(CooldownLevel) * _cooldownScale;

        // 이 한 채에 영구히 붙은 쿨다운 배율(계몽주의 — 전투에서 질 때마다 무작위 기물 하나에 ×0.8) — 팔면 사라지고, 합쳐지면 곱해진다
        [SerializeField] private float _cooldownScale = 1f;
        public float CooldownScale => _cooldownScale;
        public void ScaleCooldown(float factor) => _cooldownScale *= factor;
        // 이 한 채에 합쳐진 시작 등급 기물 수
        public int Copies => 1 << _upgrades;
        public bool CanUpgrade => Rarity < BuildingRarity.Mythic;

        public int Stacks => _stacks;
        // 판매가는 여기의 절반이다(RunState.SellValue) — 살 때 낸 값을 더하고, 합쳐질 때 사라지는 쪽의 값을 받는다
        public int SpentGold => _spentGold;

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

        public void AddSpent(int gold) => _spentGold += gold;

        // 수를 세어 발동하는 효과(라이칸스로프 · 승천의 전당 · 미궁의 입구 · 스팀바론 · 맘모스 소환의식)의 남은 수 — 전투 중 탑 칸 표시(Bases/Tower · UI/SlotView).
        // 전투 밖이거나 그런 효과가 없으면 0(안 보인다). 미궁은 발동이 끝나면 0
        public int CountdownOf(Team team)
        {
            if (!BattleManager.Fighting)
                return 0;
            foreach (BuildingPassive passive in Passives)
                if (passive is DeathCountCooldownPassive killCount)
                    return killCount.RemainingOf(team);
                else if (passive is TowerKillBuyPassive towerKill)
                    return towerKill.RemainingOf(team);
                else if (passive is KillCountGoldPassive killGold)
                    return killGold.RemainingOf(team, Level);
                else if (passive is MammothRitualPassive ritual)
                    return ritual.RemainingOf(this);   // 런 내내 센다(이 한 채의 스택) — 채우면 0
            foreach (UnitEffect effect in Effects)
                if (effect is AscensionEffect ascension)
                    return ascension.RemainingOf(team);
                else if (effect is DeathCountSummonEffect deathCount)
                    return deathCount.RemainingOf(team);
                else if (effect is SummonCountRallyEffect rally)
                    return rally.RemainingOf(team);
            return 0;
        }

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

using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 직렬화되는 enum — 값은 맨 뒤에만 추가할 것. 번호가 WorldGraph.Upgrade의 값이다
    public enum RegionUpgradeKind
    {
        Weapon,       // 무기 — 내 유닛의 공격력 +N%
        Armor,        // 방어구 — 내 유닛의 체력 +N%
        Supply,       // 보급력 — 유닛을 소환하는 내 기물의 쿨다운 ×N
        Tech,         // 기술 연구 — 내 상점 등급 단계 +N
        Tax,          // 세금 증산 — 턴마다 얻는 골드 +N
        Nationalism,  // 민족주의 — 나와 다른 국가의 영주와 전투할 때, 내 유닛의 체력 +N%
    }

    public enum RegionUpgradeScope
    {
        Common,   // 공용
        Nation,   // 진영 특화
    }

    public enum RegionUpgradeUse
    {
        Battle,     // 전투에 도움
        Operation,  // 운영에 도움
    }

    // 지역 업그레이드 한 종 — 그 땅을 가진 동안 계속 걸린다(기획 §4). 어느 땅에 붙는지는 WorldMapDefinition.
    // 영주마다 자기 땅의 것만 받고 같은 종류는 겹친다 — 거는 곳은 World/WorldRun(「지역 업그레이드」)
    [CreateAssetMenu(menuName = "GnorpWar/Region Upgrade")]
    public class RegionUpgradeDefinition : ScriptableObject, IUnitFilter
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
        [Tooltip("아이콘(32×32) — 지도의 땅 표시 · 패널의 가진 업그레이드 줄")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _icon;
        [SerializeField] private RegionUpgradeKind _kind;
        [Tooltip("표기 그대로의 숫자 — 무기·방어구·민족주의는 +N%의 N(4 = +4%, 합연산) · 보급력은 쿨다운에 곱하는 값(0.97 = ×0.97) · 기술 연구는 등급 단계 수 · 세금 증산은 골드")]
        [SerializeField] private float _value = 1f;
        [Tooltip("공용 / 진영 특화")]
        [SerializeField] private RegionUpgradeScope _scope;
        [Tooltip("전투에 도움 / 운영에 도움")]
        [SerializeField] private RegionUpgradeUse _use;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public RegionUpgradeKind Kind => _kind;
        public float Value => _value;
        public RegionUpgradeScope Scope => _scope;
        public RegionUpgradeUse Use => _use;

        // 유닛 스탯에 거는 종류는 그 진영의 모든 유닛에게 걸린다(StatBook의 대상 고르기)
        public bool AppliesTo(UnitDefinition unit) => true;

        // count개를 가졌을 때의 합쳐진 값 — 표기가 곧 계산이다: +N%와 +N은 더하고(무기 3개 = +12%), ×N은 곱한다(보급력 3개 = ×0.97³)
        public float Total(int count) => _kind == RegionUpgradeKind.Supply ? Mathf.Pow(_value, count) : _value * count;

        // count개를 가졌을 때 유닛 스탯에 거는 값 — 무기(공격력 +%) · 방어구와 민족주의(체력 +%, 합연산). 그 밖의 종류는 false
        public bool UnitModifier(int count, out StatModifier modifier)
        {
            UnitStat stat = _kind == RegionUpgradeKind.Weapon ? UnitStat.Attack : UnitStat.MaxHp;
            modifier = new StatModifier(stat, StatOp.Percent, Total(count) / 100f);
            return _kind == RegionUpgradeKind.Weapon || _kind == RegionUpgradeKind.Armor || _kind == RegionUpgradeKind.Nationalism;
        }

        // 설명 글 — 값에서 만든다(값을 고치면 글이 따라간다, CLAUDE.md §5-2 6번). 한 개의 효과
        public string Description => Describe(1);

        // count개를 가졌을 때의 설명 — 숫자는 합쳐진 실제 값 하나
        public string Describe(int count)
        {
            float total = Total(count);
            switch (_kind)
            {
                case RegionUpgradeKind.Weapon: return $"내 유닛의 공격력 +{total:0.##}%";
                case RegionUpgradeKind.Armor: return $"내 유닛의 체력 +{total:0.##}%";
                case RegionUpgradeKind.Supply: return $"유닛을 소환하는 내 기물의 쿨다운 ×{total:0.###}";
                case RegionUpgradeKind.Tech: return $"내 상점 등급 단계 +{total:0.##}";
                case RegionUpgradeKind.Tax: return $"턴마다 얻는 골드 +{total:0.##}";
                default: return $"나와 다른 국가의 영주와 전투할 때, 내 유닛의 체력 +{total:0.##}%";
            }
        }
    }
}

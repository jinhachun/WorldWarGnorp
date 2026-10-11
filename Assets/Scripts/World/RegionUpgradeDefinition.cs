using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 직렬화되는 enum — 값은 맨 뒤에만 추가할 것. 번호가 WorldGraph.Upgrade의 값이다
    public enum RegionUpgradeKind
    {
        Weapon,       // 무기 강화 — 내 유닛의 공격력 +N%
        Armor,        // 방어구 강화 — 내 유닛의 체력 +N%
        Supply,       // 보급력 강화 — 유닛을 소환하는 내 기물의 쿨다운 ×N
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
    // 지금은 「누가 무엇을 몇 개 가졌나」만 센다(WorldState.UpgradeCount) — 효과를 거는 코드는 아직 없다
    [CreateAssetMenu(menuName = "GnorpWar/Region Upgrade")]
    public class RegionUpgradeDefinition : ScriptableObject
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
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
        public RegionUpgradeKind Kind => _kind;
        public float Value => _value;
        public RegionUpgradeScope Scope => _scope;
        public RegionUpgradeUse Use => _use;

        // 설명 글 — 값에서 만든다(값을 고치면 글이 따라간다, CLAUDE.md §5-2 6번)
        public string Description
        {
            get
            {
                switch (_kind)
                {
                    case RegionUpgradeKind.Weapon: return $"내 유닛의 공격력 +{_value:0.##}%";
                    case RegionUpgradeKind.Armor: return $"내 유닛의 체력 +{_value:0.##}%";
                    case RegionUpgradeKind.Supply: return $"유닛을 소환하는 내 기물의 쿨다운 ×{_value:0.##}";
                    case RegionUpgradeKind.Tech: return $"내 상점 등급 단계 +{_value:0.##}";
                    case RegionUpgradeKind.Tax: return $"턴마다 얻는 골드 +{_value:0.##}";
                    default: return $"나와 다른 국가의 영주와 전투할 때, 내 유닛의 체력 +{_value:0.##}%";
                }
            }
        }
    }
}

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 직렬화되는 enum — 값은 맨 뒤에만 추가할 것
    public enum UpgradeKind
    {
        UnitStat,    // 대상 병종 유닛의 스탯 +N%(합연산) — 전투 동안
        TowerStat,   // 내 타워들의 스탯 +N%(합연산) — 전투 동안
        UnitEffect,  // 유닛 효과를 켠다 — 합쳐진 값은 효과가 읽는다(UnitEffect.Value)
        Summon,      // 전투 시작 시 유닛 N기 소환
        SmallTower,  // 전투 시작 시 전장 가운데에 작은 타워 — 체력 = 내 본진의 N%
        TurnGold,    // 턴마다 골드 +N
        FreeReroll,  // 턴마다 무료 리롤 +N회
    }

    // 같은 업그레이드를 여러 번 얻었을 때 — 값을 더한다 / 가장 높은 등급의 값 하나만(사냥꾼의 활)
    public enum UpgradeStack { Sum, Strongest }

    // 얻은 업그레이드 한 번 — 종류와 그때의 등급(합친 재료의 등급)
    public struct UpgradeGain
    {
        public UpgradeDefinition Definition;
        public BuildingRarity Rarity;
    }

    // 한 탑이 가진 업그레이드 한 종 — 얻은 횟수 · 얻은 것 중 가장 높은 등급(아이콘 외곽선 색) · 합쳐진 값
    public struct UpgradeHolding
    {
        public UpgradeDefinition Definition;
        public int Count;
        public BuildingRarity Rarity;
        public float Value;
        public string Description => Definition.Describe(Value);
    }

    // 합치기 보상 업그레이드 한 종(아웃게임기획 §4) — 기물을 합칠 때마다 셋 중 하나를 고르고(RunState 「합치기 보상 업그레이드」), 런 동안 그 탑에 남는다.
    // 같은 업그레이드가 등급마다 수치만 다르다. 전투에 거는 곳은 Apply · Spawn(BattleManager.StartBattle), 운영(골드 · 무료 리롤)은 RunState가 값을 읽는다
    [CreateAssetMenu(menuName = "GnorpWar/Upgrade")]
    public class UpgradeDefinition : ScriptableObject, IUnitFilter
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
        [Tooltip("아이콘(32×32)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _icon;
        [Tooltip("효과 글 — {0}에 지금 값이 들어간다(표기 그대로: +N%의 N · ×N의 N · N기 · N초)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _description = "";
        [Tooltip("등급별 값 — 칸 순서: 일반 · 고급 · 희귀 · 영웅 · 전설. 글에 보이는 숫자 그대로(8 = +8% · 0.9 = ×0.9)")]
        [SerializeField] private float[] _values = new float[5];
        [SerializeField] private UpgradeStack _stack;
        [Tooltip("더한 값의 상한(연사 100) — 0 = 상한 없음")]
        [SerializeField] private float _max;
        [Tooltip("끄면 합치기 보상 후보에 나오지 않는다(조련장 — 중기병을 낳는 기물이 생길 때까지)")]
        [SerializeField] private bool _inPool = true;

        [Header("효과")]
        [SerializeField] private UpgradeKind _kind;
        [Tooltip("UnitStat — 이 병종의 유닛에게(비우면 전부)")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [Tooltip("UnitStat — 올릴 스탯")]
        [SerializeField] private UnitStat _unitStat;
        [Tooltip("TowerStat — 올릴 스탯")]
        [SerializeField] private TowerStat _towerStat;
        [Tooltip("UnitEffect — 켤 효과(대상 병종은 효과 에셋에서 고른다)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UnitEffect _effect;
        [Tooltip("Summon — 소환할 유닛")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UnitDefinition _unit;
        [Tooltip("SmallTower — 세우는 자리 · 프리팹을 이 능력(조립식 감시탑)에서 빌린다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private SmallTowerAtStartPassive _smallTower;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public UpgradeKind Kind => _kind;
        public bool InPool => _inPool;

        // 그 등급의 값 — 신화 등급 업그레이드는 없다(전설 둘을 합쳐도 전설 값)
        public float ValueAt(BuildingRarity rarity) => _values[Mathf.Min((int)rarity, _values.Length - 1)];

        // 설명 글 — 숫자는 지금 값 하나(CLAUDE.md §5-2 6번)
        public string Describe(float value) => string.Format(_description, value.ToString("0.##"));

        public bool AppliesTo(UnitDefinition unit) => _categories.Length == 0 || System.Array.IndexOf(_categories, unit.Category) >= 0;

        // 유닛 스탯에 거는 종류면 그 값 — 전투 시작(Apply)과 유닛 카드 미리 보기(RunStats.Preview)가 같이 읽는다
        public bool UnitModifier(float value, out StatModifier modifier)
        {
            modifier = new StatModifier(_unitStat, StatOp.Percent, value / 100f);
            return _kind == UpgradeKind.UnitStat;
        }

        // 전투 시작 ① — 양쪽 보드가 돌기 전에: 스탯과 유닛 효과를 건다(전투 동안)
        public void Apply(Team team, float value)
        {
            if (UnitModifier(value, out StatModifier modifier))
                StatBook.AddForBattle(team, this, modifier);
            else if (_kind == UpgradeKind.TowerStat)
                TowerBook.AddForBattle(team, new TowerModifier(_towerStat, StatOp.Percent, value / 100f));
            else if (_kind == UpgradeKind.UnitEffect)
                TeamEffects.Enable(team, _effect, 1).Value = value;
        }

        // 전투 시작 ② — 양쪽 보드가 돈 뒤에: 유닛 · 작은 타워를 전장에 낸다
        public void Spawn(Team team, float value)
        {
            if (_kind == UpgradeKind.Summon)
                BoardRunner.For(team).Summon(_unit, Mathf.RoundToInt(value), -1);
            else if (_kind == UpgradeKind.SmallTower)
            {
                Base tower = _smallTower.Build(team);
                if (tower != null)
                    tower.SetHpOfHome(value / 100f);
            }
        }

        // 얻은 목록을 종류별로 — 처음 얻은 순서. 값 = 더한 값(상한까지) 또는 가장 높은 등급의 값
        public static List<UpgradeHolding> Summarize(List<UpgradeGain> gains)
        {
            var holdings = new List<UpgradeHolding>();
            foreach (UpgradeGain gain in gains)
            {
                UpgradeDefinition definition = gain.Definition;
                int at = holdings.FindIndex(h => h.Definition == definition);
                if (at < 0)
                {
                    holdings.Add(new UpgradeHolding { Definition = definition, Count = 1, Rarity = gain.Rarity, Value = definition.ValueAt(gain.Rarity) });
                    continue;
                }
                UpgradeHolding holding = holdings[at];
                holding.Count++;
                if (definition._stack == UpgradeStack.Sum)
                    holding.Value += definition.ValueAt(gain.Rarity);
                else if (gain.Rarity > holding.Rarity)
                    holding.Value = definition.ValueAt(gain.Rarity);
                if (gain.Rarity > holding.Rarity)
                    holding.Rarity = gain.Rarity;
                holdings[at] = holding;
            }
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].Definition._max > 0f && holdings[i].Value > holdings[i].Definition._max)
                {
                    UpgradeHolding holding = holdings[i];
                    holding.Value = holding.Definition._max;
                    holdings[i] = holding;
                }
            return holdings;
        }

        // 얻은 목록에서 그 종류(TurnGold · FreeReroll)의 값을 전부 더한 것
        public static float TotalOf(List<UpgradeGain> gains, UpgradeKind kind)
        {
            float total = 0f;
            foreach (UpgradeHolding holding in Summarize(gains))
                if (holding.Definition._kind == kind)
                    total += holding.Value;
            return total;
        }
    }
}

using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 경보병)이 죽을 때마다, 전투 동안 다른 병종의 내 유닛 전부(나중에 소환되는 유닛도)의 스탯을 올린다
    // (거대 괴수의 무덤: 내 경보병이 죽을 때마다 전투 동안 내 중기병의 공격력 +5% — 트라이브 명세 §4 · §5). 진영 스탯(StatBook)에 건다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Category Stat")]
    public class DeathCategoryStatEffect : UnitEffect
    {
        // 받는 쪽 — 이 병종의 유닛(「~로도 취급」 포함)
        [System.Serializable]
        private class Recipients : IUnitFilter, ICategoryFilter
        {
            public UnitCategory[] Categories = new UnitCategory[0];

            public bool AppliesTo(UnitDefinition unit) => System.Array.IndexOf(Categories, unit.Category) >= 0;
            public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(Categories, category) >= 0;
        }

        [Tooltip("받는 병종")]
        [SerializeField] private Recipients _recipients = new Recipients();
        [SerializeField] private StatModifier _stat = new StatModifier(UnitStat.Attack, StatOp.Percent, 0.05f);
        [Tooltip("이만큼 죽을 때마다 한 번(영혼 수확 10 — 사용자 11/1). 1 = 죽을 때마다. 2 이상이면 죽은 수를 이 효과의 스택으로 센다(전투마다 0부터)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _every = 1;
        [Tooltip("그때마다 받는 병종의 사거리에 더하는 몫(합연산, 0.1 = +10%). 0이면 없음")]
        [SerializeField] private float _rangePercent;
        [Tooltip("이번 전투에 쌓이는 스탯 값의 상한(합연산 몫, 5 = +500% — 영혼 수확, 사용자 11/1). 사거리 몫도 같은 횟수에서 멈춘다. 0이면 없음. 「몇 기마다」가 2 이상일 때만 센다")]
        [SerializeField] private float _cap;

        // 상한에 닿기까지 발동하는 횟수(상한 없으면 int.MaxValue)
        private int MaxTimes => _cap > 0f && _stat.Value > 0f ? Mathf.FloorToInt(_cap / _stat.Value + 0.0001f) : int.MaxValue;

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            if (_every > 1)
            {
                TeamEffects.AddStack(unit.Team, this);
                if ((stacks + 1) % _every != 0 || (stacks + 1) / _every > MaxTimes)
                    return;
            }
            StatBook.AddForBattle(unit.Team, _recipients, _stat);
            if (_rangePercent != 0f)
                TowerBook.AddUnitRange(unit.Team, _recipients, _rangePercent);
            BuffLinks.EffectFromUnitToUnits(unit.Team, this, at, unit.Team, u => _recipients.AppliesFor(u.Team, u.Definition), BuffKind.Stat, BuffLabel.Of(_stat));
        }

        // 지금까지 쌓인 값(진영별 — 검증 · 표시용) = 발동 횟수 × 스탯 값
        public float StackedOf(Team team)
        {
            foreach (TeamEffects.Entry e in TeamEffects.For(team))
                if (e.Effect == this)
                    return Mathf.Min(e.Stacks / Mathf.Max(1, _every), MaxTimes) * _stat.Value;
            return 0f;
        }

        // 설명의 {0} = 스탯 값 · {1} = 몇 기마다 · {2} = 사거리 몫(+10%) · {3} = 상한(+500%)
        public override string Describe(int level) => string.Format(Description, _stat.ValueText, _every, $"+{_rangePercent * 100f:0.#}%", $"+{_cap * 100f:0.#}%");
    }
}

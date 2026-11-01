using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 번개 토템(사용자 11/1) — 이 진영 번개 유닛(_bouncer)의 번개가 늘 _bonus만큼 더 튀고, 적용 대상 유닛(경보병)이 _every기 죽을 때마다
    // 전투 동안 _perDeaths씩 더 튄다(최대 _maxFromDeaths). 죽은 수는 이 효과의 스택으로 센다 — 전투마다 0부터
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Chain Bounce")]
    public class ChainBounceEffect : UnitEffect
    {
        [Tooltip("번개가 더 튀는 유닛")]
        [SerializeField] private UnitDefinition _bouncer;
        [Tooltip("늘 더 튀는 수")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _bonus = 1;
        [Tooltip("적용 대상 유닛이 이만큼 죽을 때마다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _every = 30;
        [Tooltip("그때마다 전투 동안 더 튀는 수")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _perDeaths = 1;
        [Tooltip("죽음으로 더해지는 몫의 상한")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _maxFromDeaths = 3;

        private int FromDeaths(int deaths) => Mathf.Min(_maxFromDeaths, deaths / Mathf.Max(1, _every) * _perDeaths);

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            TeamEffects.AddStack(unit.Team, this);
            if (FromDeaths(stacks + 1) > FromDeaths(stacks) && _bouncer != null)
                BuffLinks.EffectFromUnitToUnits(unit.Team, this, at, unit.Team, u => u.Definition == _bouncer, BuffKind.Stat);
        }

        public override int ExtraChainBounces(Team team, UnitDefinition unit, int stacks)
            => unit == _bouncer ? _bonus + FromDeaths(stacks) : 0;

        // 설명의 {0} = 늘 더 튀는 수 · {1} = 몇 기마다 · {2} = 그때마다 · {3} = 상한
        public override string Describe(int level) => string.Format(Description, _bonus, _every, _perDeaths, _maxFromDeaths);
        public override string Headline(int level) => $"+{_bonus}";
    }
}

using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 유닛이 N기 소환될 때마다, 그 순간 살아 있는 내 유닛 전부의 이동속도 +P% (S초) (행진가 — 사용자 10/27, 영웅 +50% · 전설 +100%).
    // 모든 소환을 센다(기물 · 유닛 효과 · 연계 소환 — 소환 이벤트 OnSummoned). 다시 일어나기는 소환이 아니라 안 센다. 센 수는 이 효과의 스택(기물에 쿨다운이 없다), 전투마다 0부터.
    // 이 효과는 소환하지 않으므로 연쇄가 없다. 신화 표시(_mythicMark — 이 진영에 켜진 유닛 효과)가 있으면 공격속도도 같은 만큼
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Summon Count Rally")]
    public class SummonCountRallyEffect : UnitEffect
    {
        [SuffixLabel("기", true)]
        [SerializeField] private int _summons = 10;
        [Tooltip("합연산 %(시작 등급)")]
        [SuffixLabel("%", true)]
        [SerializeField] private float _percent = 50f;
        [SuffixLabel("%", true)]
        [SerializeField] private float _percentPerTier = 50f;
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 1f;
        [Tooltip("이 진영에 켜져 있으면(신화 줄) 공격속도도 같은 만큼")]
        [SerializeField] private UnitEffect _mythicMark;

        private float Percent(int level) => OwnedBuilding.Scaled(_percent, _percentPerTier, level);

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            Team team = unit.Team;
            TeamEffects.AddStack(team, this);
            if ((stacks + 1) % Mathf.Max(1, _summons) != 0)
                return;
            float ratio = Percent(Level(team)) / 100f;
            bool attackSpeed = false;
            if (_mythicMark != null)
                foreach (TeamEffects.Entry e in TeamEffects.For(team))
                    attackSpeed |= e.Effect == _mythicMark;
            foreach (Unit ally in FindObjectsByType<Unit>())
                if (ally.IsAlive && ally.Team == team)
                {
                    ally.Rally(UnitStat.MoveSpeed, ratio, _seconds);
                    if (attackSpeed)
                        ally.Rally(UnitStat.AttackSpeed, ratio, _seconds);
                }
            BuffLinks.EffectToUnits(team, this, u => u.IsAlive, BuffKind.Stat, BuffLabel.Of(new StatModifier(UnitStat.MoveSpeed, StatOp.Percent, ratio)));
        }

        // 다음 발동까지 남은 소환 수(설명 창의 회색 줄 · 탑 칸 숫자) — 전투 밖이면 처음 값
        public int RemainingOf(Team team)
        {
            if (!BattleManager.Fighting)
                return _summons;
            foreach (TeamEffects.Entry e in TeamEffects.For(team))
                if (e.Effect == this)
                    return _summons - e.Stacks % Mathf.Max(1, _summons);
            return _summons;
        }

        // 설명의 {0} = 소환 수 · {1} = 초 · {2} = 지금 등급의 %
        public override string Describe(int level) => string.Format(Description, _summons, _seconds, Percent(level));
        public override string Headline(int level) => $"{Percent(level):0.#}%";
    }
}

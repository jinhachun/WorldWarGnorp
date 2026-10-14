using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투가 시작된 뒤 일정 간격마다 그 진영의 적용 대상 유닛 전체에 스탯을 더한다 — 상한까지, 전투 동안
    // (교대근무계획서 신화: 톱니병사의 공격속도 1초마다 +1%, 최대 +200%).
    // 기물의 쿨다운이 아니라 전투 시각(BattleManager.Elapsed)으로 센다 — 가속·쿨감이 간격을 흔들지 않게
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Periodic Battle Stat")]
    public class PeriodicBattleStatEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _interval = 1f;
        [SerializeField] private StatModifier _step = new StatModifier(UnitStat.AttackSpeed, StatOp.Percent, 0.01f);
        [Tooltip("이만큼 쌓이면 멈춘다 — 200회 × +1% = +200%. 표기(최대)도 이 둘을 곱해 쓴다")]
        [SerializeField] private int _maxSteps = 200;

        // 이번 전투에 이미 걸어 준 횟수(진영마다). 전투가 다시 시작되면 Elapsed가 줄어드는 것으로 알고 0으로 돌린다
        // (StatBook의 「전투 동안」 목록도 그때 비워진다)
        private readonly int[] _applied = new int[2];

        public override void Tick(Unit unit, int stacks)
        {
            if (_interval <= 0f)
                return;
            int team = (int)unit.Team;
            int target = Mathf.Min((int)(BattleManager.Elapsed / _interval), _maxSteps);
            if (target < _applied[team])
                _applied[team] = 0;
            while (_applied[team] < target)
            {
                _applied[team]++;
                StatBook.AddForBattle(unit.Team, this, _step);
                BuffLinks.EffectToUnits(unit.Team, this, u => this.AppliesFor(u.Team, u.Definition), BuffKind.Stat);
            }
        }

        private string CapText => new StatModifier(_step.Stat, _step.Op, _step.Value * _maxSteps).ValueText;

        public override string Describe(int level) => string.Format(Description, $"{_interval:0.##}", _step.Describe(), CapText);
    }
}

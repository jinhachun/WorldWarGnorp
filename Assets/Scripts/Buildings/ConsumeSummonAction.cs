using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 전장의 내 유닛 N기를 소모해 다른 유닛을 소환한다 (조립 라인: 톱니병사 10기 → 톱니거인 1기).
    // 모자라면 아무것도 안 한다. 소모는 뒤쪽(내 성에 가까운) 유닛부터 — 앞줄을 비우지 않게
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Consume Summon")]
    public class ConsumeSummonAction : BuildingAction
    {
        [SerializeField] private UnitDefinition _consume;
        [SerializeField] private int _consumeCount = 10;
        [SerializeField] private UnitDefinition _summon;
        [SerializeField] private int _summonCount = 1;

        private readonly List<Unit> _found = new List<Unit>();

        public override void Execute(BoardRunner board, int slot, int level)
        {
            _found.Clear();
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team && unit.Definition == _consume)
                    _found.Add(unit);
            if (_found.Count < _consumeCount)
                return;

            _found.Sort((a, b) => (a.Position.x * a.Forward).CompareTo(b.Position.x * b.Forward));
            for (int i = 0; i < _consumeCount; i++)
                _found[i].Consume();
            board.Summon(_summon, _summonCount, slot);
        }

        public override string Describe(int level)
            => $"전장에 {_consume.DisplayName}가 {_consumeCount}기 이상이면 {_consumeCount}기를 소모하여 {_summon.DisplayName} {_summonCount}기를 소환합니다.";
    }
}

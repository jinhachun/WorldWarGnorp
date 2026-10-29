using Sirenix.OdinInspector;
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
        [SuffixLabel("기", true)]
        [SerializeField] private int _consumeCount = 10;
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _summonCount = 1;
        [Tooltip("등급이 오를 때마다 소환 수에 더한다(소모 수는 그대로)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _summonCountPerTier = 1;

        private int SummonCount(int level) => OwnedBuilding.Scaled(_summonCount, _summonCountPerTier, level);

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
            Vector2 consumed = Vector2.zero;
            for (int i = 0; i < _consumeCount; i++)
            {
                consumed += _found[i].Position;
                _found[i].Consume();
            }
            board.Summon(_summon, SummonCount(level), slot);
            // 사라진 무리 가운데 → 새로 나온 유닛(둘이 이어져 보이게)
            if (board.LastSpawned != null && _consumeCount > 0)
                BuffLinks.FromPoint(board.Team, slot, consumed / _consumeCount, board.LastSpawned.Position, BuffKind.Summon);
        }

        public override string Describe(int level)
            => $"전장의 {_consume.DisplayName} {_consumeCount}기로 {_summon.DisplayName} {SummonCount(level)}기를 만듭니다.";
        public override string Headline(int level) => $"{SummonCount(level)}기";
    }
}

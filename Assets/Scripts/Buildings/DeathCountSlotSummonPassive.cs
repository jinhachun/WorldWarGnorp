using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 내 유닛(고른 병종)이 N기 죽을 때마다 이 기물에서 유닛을 소환한다 (재활용 공장: 내 궁병 2기마다 톱니병사 1기 · 전설 2기 — 사용자 10/28).
    // 소환은 죽은 자리가 아니라 이 기물의 층에서(다른 생산 기물처럼 — 사용자 10/28). 「~로도 취급」되는 병종도 센다(UnitAliases.Is).
    // 연계 소환이다 — 낸 유닛은 chained(Unit.ChainBorn), 연계로 태어난 유닛의 죽음은 세지 않는다(사용자 10/26). 다시 일어나는 죽음도 안 센다.
    // 센 수는 칸마다(같은 기물 두 채가 서로의 수를 나누지 않게), 전투마다 0부터
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Death Count Slot Summon")]
    public class DeathCountSlotSummonPassive : BuildingPassive
    {
        [SerializeField] private UnitCategory _category;
        [SuffixLabel("기", true)]
        [SerializeField] private int _deaths = 2;
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("등급이 오를 때마다 소환 수에 더한다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _countPerTier = 1;

        [System.NonSerialized] private readonly Dictionary<BoardRunner.Slot, int> _counted = new Dictionary<BoardRunner.Slot, int>();

        public int CountAt(int level) => OwnedBuilding.Scaled(_count, _countPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            // 지난 전투의 칸(보드가 새로 뜨면 칸도 새것)은 지운다
            _stale.Clear();
            foreach (BoardRunner.Slot key in _counted.Keys)
                if (System.Array.IndexOf(board.Slots, key) < 0 && System.Array.IndexOf(Other(board).Slots, key) < 0)
                    _stale.Add(key);
            foreach (BoardRunner.Slot key in _stale)
                _counted.Remove(key);
            _counted[board.Slots[slot]] = 0;
        }

        private readonly List<BoardRunner.Slot> _stale = new List<BoardRunner.Slot>();

        private static BoardRunner Other(BoardRunner board)
            => BoardRunner.For(board.Team == Team.Ally ? Team.Enemy : Team.Ally) ?? board;

        public override void OnUnitDied(BoardRunner board, int slot, Unit victim)
        {
            if (victim.Team != board.Team || victim.ChainBorn || victim.Reviving || !UnitAliases.Is(victim.Team, victim.Definition, _category))
                return;
            BoardRunner.Slot self = board.Slots[slot];
            _counted.TryGetValue(self, out int n);
            if (++n < _deaths)
            {
                _counted[self] = n;
                return;
            }
            _counted[self] = 0;
            board.Summon(_summon, CountAt(self.Building.Level), slot, chained: true);
            OwnedBuilding.NotifyEffect(self.Building);
        }

        public override string Describe(int level) => $"{_category.DisplayName}이 {_deaths}기 죽을 때마다 {_summon.DisplayName} {CountAt(level)}기를 소환합니다.";
        public override string Headline(int level) => $"{CountAt(level)}기";
    }
}

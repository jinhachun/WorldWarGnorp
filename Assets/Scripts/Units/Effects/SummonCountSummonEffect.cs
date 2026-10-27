using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 N기 소환될 때마다, 이 효과를 켠 기물의 층에서 유닛을 소환한다 (기사 견습소: 내 병사 8기마다 기사 1기 — 사용자 10/28, 등급마다 필요한 수 -1: 8 · 7 · 6 · 5 · 4).
    // 실제로 나온 유닛만 센다 — 소환 이벤트(OnSummoned)는 변환이 끝난 유닛으로 불리므로 톱니병사 · 톱니로켓병으로 바뀌어 나온 병사는 세지 않는다.
    // 낸 유닛은 연계 소환(chained) — 그 유닛의 죽음 · 처치는 연계 소환을 다시 부르지 않는다. 이 효과가 낸 소환 안에서 나온 대상 유닛은 세지 않는다(_rolling).
    // 센 수는 이 효과의 스택(기물에 쿨다운이 없다), 전투마다 0부터
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Summon Count Summon")]
    public class SummonCountSummonEffect : UnitEffect
    {
        [Tooltip("이만큼 소환될 때마다(시작 등급)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _summons = 8;
        [Tooltip("등급이 오를 때마다 필요한 수에 더한다(음수 = 줄어든다)")]
        [SerializeField] private int _summonsPerTier = -1;
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;

        private static bool _rolling;

        private int Needed(int level) => Mathf.Max(1, OwnedBuilding.Scaled(_summons, _summonsPerTier, level));

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            if (_rolling)
                return;
            Team team = unit.Team;
            TeamEffects.AddStack(team, this);
            if ((stacks + 1) % Needed(Level(team)) != 0)
                return;
            BoardRunner board = BoardRunner.For(team);
            BuildingDefinition source = Source(team);
            _rolling = true;
            try
            {
                board.Summon(_summon, _count, SlotOf(board, source), null, source, chained: true);
            }
            finally
            {
                _rolling = false;
            }
        }

        // 이 효과를 켠 기물이 놓인 칸(못 찾으면 -1 — 그래도 성문에서 나온다)
        private static int SlotOf(BoardRunner board, BuildingDefinition source)
        {
            for (int i = 0; i < board.Slots.Length; i++)
                if (board.Slots[i].Building != null && board.Slots[i].Building.Definition == source)
                    return i;
            return -1;
        }

        // 다음 소환까지 남은 수(탑 칸 숫자) — 전투 밖이면 시작 등급 값
        public int RemainingOf(Team team)
        {
            if (!BattleManager.Fighting)
                return _summons;
            int needed = Needed(Level(team));
            foreach (TeamEffects.Entry e in TeamEffects.For(team))
                if (e.Effect == this)
                    return needed - e.Stacks % needed;
            return needed;
        }

        // 설명의 {0} = 지금 등급의 필요한 수 · {1} = 소환 수
        public override string Describe(int level) => string.Format(Description, Needed(level), _count);
        public override string Headline(int level) => $"{Needed(level)}기";
    }
}

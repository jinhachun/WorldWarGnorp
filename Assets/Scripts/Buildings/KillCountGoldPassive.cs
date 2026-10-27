using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적 유닛 N기를 처치할 때마다 골드 1 (전리품 마차 — 사용자 10/27, 등급마다 30 · 25 · 20기). 전투마다 0부터.
    // 처치 = 이 진영이 아닌 유닛의 죽음(누가 죽였든 — 아군을 죽여도 처치로 치는 규칙과 같은 폭). 골드는 주식시장과 같은 길(RunState.GainBattleGold) — 전투는 상점 뒤라 쓰는 건 다음 턴이다.
    // 신화 표시(_mythicMark)가 있으면 내 타워들의 처치는 2기로 센다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Kill Count Gold")]
    public class KillCountGoldPassive : BuildingPassive
    {
        [SuffixLabel("기", true)]
        [SerializeField] private int _kills = 30;
        [Tooltip("등급이 오를 때마다 필요한 수에 더한다(음수 = 줄어든다)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _killsPerTier = -5;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _gold = 1;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 내 타워들의 처치를 한 번 더 센다")]
        [SerializeField] private BuildingPassive _mythicMark;

        // 이번 전투에 센 처치 수(진영별 — 이 기물을 가진 쪽)
        [System.NonSerialized] private readonly int[] _count = new int[2];

        private int Kills(int level) => Mathf.Max(1, OwnedBuilding.Scaled(_kills, _killsPerTier, level));

        public override void OnBattleStart(BoardRunner board, int slot, int level) => _count[(int)board.Team] = 0;

        public override void OnUnitDied(BoardRunner board, int slot, Unit victim)
        {
            if (victim.Team != board.Team)
                Count(board, slot, victim);
        }

        public override void OnTowerKill(BoardRunner board, int slot, Unit victim)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            if (_mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0)
                Count(board, slot, victim);
        }

        private void Count(BoardRunner board, int slot, Unit victim)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            if (++_count[(int)board.Team] < Kills(self.Level))
                return;
            _count[(int)board.Team] = 0;
            RunState.GainBattleGold(board.Team, _gold);
            OwnedBuilding.NotifyEffect(self);
            GoldDrops.Raise(board.Team, victim.Position, _gold);
        }

        // 다음 골드까지 남은 처치 수(설명 창의 회색 줄 · 탑 칸 숫자)
        public int RemainingOf(Team team, int level) => BattleManager.Fighting ? Kills(level) - _count[(int)team] : Kills(level);

        public override string Describe(int level) => $"적 유닛 {Kills(level)}기를 처치할 때마다, 골드 {_gold}을 얻습니다.";
        public override string Headline(int level) => $"{Kills(level)}기";
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 전투당 한 번, 적용 대상 유닛(내 원시 졸병)이 N기 소환되면 그 N기를 바쳐, 쿨다운이 있는 무작위 내 기물 1개를 즉시 1회 발동시킨다
    // (국가 전용 업그레이드 「피의 공물」 — 트라이브). N은 얻은 것마다 따로(UnitEffect.Values) — 작은 N부터 차례로, 앞에서 바치고 남은 졸병부터 다시 센다.
    // 바치는 것은 소모다 — 죽음으로 치지 않는다(뼛가루 · 사망 효과 없음, 총괄 10/21). 이미 죽은 졸병은 빼고 살아 있는 것만 바친다.
    // 연출: 바친 졸병마다 그 자리 → 발동한 층으로 흰 선(BuffLinks.FromPointToSlot) + 소모 연기(Unit.Consume)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Sacrifice")]
    public class UpgradeSacrificeEffect : UnitEffect
    {
        // 진영별 — 이번 전투에 아직 안 바친 졸병(소환된 순서) · 끝낸 문턱 수. 전투가 바뀌면(시작 시각) 비운다
        private readonly List<Unit>[] _waiting = { new List<Unit>(), new List<Unit>() };
        private readonly List<int>[] _lives = { new List<int>(), new List<int>() };
        private readonly int[] _done = new int[2];
        private readonly float[] _battle = { -1f, -1f };

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            int t = (int)unit.Team;
            float battle = Time.time - BattleManager.Elapsed;
            if (!Mathf.Approximately(_battle[t], battle))
            {
                _battle[t] = battle;
                _waiting[t].Clear();
                _lives[t].Clear();
                _done[t] = 0;
            }
            List<float> values = Values(unit.Team);
            if (_done[t] >= values.Count)
                return;
            _waiting[t].Add(unit);
            _lives[t].Add(unit.Life);
            var thresholds = new List<float>(values);
            thresholds.Sort();
            int needed = Mathf.Max(1, Mathf.RoundToInt(thresholds[_done[t]]));
            if (_waiting[t].Count < needed)
                return;
            _done[t]++;
            Sacrifice(unit.Team);
        }

        private void Sacrifice(Team team)
        {
            int t = (int)team;
            BoardRunner board = BoardRunner.For(team);
            int slot = board != null && !BattleManager.BoardsStopped ? board.RandomCooling() : -1;
            for (int i = 0; i < _waiting[t].Count; i++)
            {
                Unit grunt = _waiting[t][i];
                if (grunt == null || !grunt.IsAlive || grunt.Life != _lives[t][i])
                    continue;
                if (slot >= 0)
                    BuffLinks.FromPointToSlot(team, grunt.Position, slot, BuffKind.Trigger);
                grunt.Consume();
            }
            _waiting[t].Clear();
            _lives[t].Clear();
            if (slot >= 0)
                board.FireNow(slot);
        }
    }
}

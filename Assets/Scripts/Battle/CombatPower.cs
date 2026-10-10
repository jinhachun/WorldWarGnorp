using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 전장 전투력 — 진영별 살아 있는 유닛의 전투력 합계 (체력바 밑 표시가 읽는다).
    // 유닛 전투력 = √(지금 체력 × 초당 피해) × 표시 배율. 초당 피해는 지금 공격력 × 공격력 가속 ÷ 지금 공격 간격,
    // 범위 공격(화염·휩쓸기)은 범위 배율을 곱하고, 회복 유닛은 초당 회복량 × 회복 계수를 쓴다. 밟기 피해는 넣지 않는다. 계수는 BattleConfig.
    // 무적·사망 유예 중인 유닛도 지금 값 그대로 센다. 전투 밖(상점)에선 0
    public static class CombatPower
    {
        private const float RefreshSeconds = 0.25f;

        private static readonly float[] Sums = new float[2];
        private static readonly int[] Counts = new int[2];
        private static float _computedAt = float.NegativeInfinity;

        public static float For(Team team)
        {
            Refresh();
            return Sums[(int)team];
        }

        public static int Count(Team team)
        {
            Refresh();
            return Counts[(int)team];
        }

        // 초당 유닛 생산 수 — 최근 SpawnRateWindowSeconds 동안 전장에 나온 유닛 수 ÷ 그 시간(체력바 밑 표시가 읽는다).
        // 전투 시작 뒤 창 길이가 안 됐으면 지난 시간으로 나눈다(첫 1초는 1초로 — 시작 직후 값이 튀지 않게). 전투 밖이면 0
        private static readonly Queue<float>[] SpawnTimes = { new Queue<float>(), new Queue<float>() };
        private static readonly float[] LastSpawnAt = new float[2];

        // 유닛이 전장에 나올 때마다(Unit.OnEnable) — 기물 소환 · 유닛이 낳는 소환 · 변환 소환 모두 여기로 온다
        public static void NoteSpawn(Team team)
        {
            int side = (int)team;
            float now = Time.time;
            // 씬 재로드로 시간이 되돌아갔으면 지난 기록을 버린다
            if (now < LastSpawnAt[side])
                SpawnTimes[side].Clear();
            LastSpawnAt[side] = now;
            SpawnTimes[side].Enqueue(now);
        }

        public static float SpawnPerSecond(Team team)
        {
            BattleConfig config = BattleManager.Config;
            if (config == null || !BattleManager.Fighting)
                return 0f;
            float now = Time.time;
            float elapsed = BattleManager.Elapsed;
            float window = Mathf.Min(config.SpawnRateWindowSeconds, elapsed);
            // 창 밖 기록과 이번 전투 전 기록을 버린다 — 새 전투는 빈 채로 시작한다
            Queue<float> times = SpawnTimes[(int)team];
            if (now < LastSpawnAt[(int)team])
                times.Clear();
            while (times.Count > 0 && times.Peek() < now - window)
                times.Dequeue();
            return times.Count / Mathf.Max(1f, window);
        }

        public static float Of(Unit unit)
        {
            BattleConfig config = BattleManager.Config;
            if (config == null || !unit.IsAlive)
                return 0f;
            UnitDefinition definition = unit.Definition;
            float perSecond;
            if (definition.AttackType == AttackType.Heal)
                perSecond = definition.HealAmount / unit.AttackInterval * config.PowerHealScale;
            else
            {
                perSecond = unit.AttackDamage * unit.DamageScale / unit.AttackInterval;
                if (definition.AttackType == AttackType.Flame || definition.AttackType == AttackType.Sweep)
                    perSecond *= config.PowerAreaScale;
            }
            return Mathf.Sqrt(unit.Hp * Mathf.Max(0f, perSecond)) * config.PowerDisplayScale;
        }

        // 0.25초마다 한 번만 다시 센다 — 씬 재로드로 시간이 되돌아가도 다시 센다
        private static void Refresh()
        {
            float now = Time.time;
            if (now >= _computedAt && now < _computedAt + RefreshSeconds)
                return;
            _computedAt = now;
            Sums[0] = Sums[1] = 0f;
            Counts[0] = Counts[1] = 0;
            if (!BattleManager.Fighting)
                return;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                if (!unit.IsAlive)
                    continue;
                int side = (int)unit.Team;
                Sums[side] += Of(unit);
                Counts[side]++;
            }
        }
    }
}

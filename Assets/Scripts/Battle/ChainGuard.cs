using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 끝없는 소환 · 발동 사슬 방어(사용자 10/26 — 짐승 몰이 100%가 한 프레임에 끝없이 소환해 스택이 넘쳤다).
    // 상한은 넷(BattleConfig 「안전 상한」): ① 소환 안에서 또 소환한 깊이 ② 물리 스텝 하나에 진영이 소환한 수 ③ 전장 유닛 수 ④ 발동 안에서 또 발동한 깊이.
    // 걸리면 그 소환(넘친 몫) · 발동을 버리고 콘솔 경고 한 줄 — 같은 원인은 전투당 한 번. 게임은 멈추지 않는다.
    // 모든 소환은 BoardRunner.Summon, 모든 발동은 BoardRunner.Fire를 지나므로 그 둘만 여기를 부른다
    public static class ChainGuard
    {
        private static int _summonDepth;
        private static int _fireDepth;
        // 지금 물리 스텝(Time.fixedTime)에 진영별로 소환한 수 — Update(기물 발동)의 소환은 직전 스텝에 센다
        private static float _step = float.NaN;
        private static readonly int[] StepSummons = new int[2];
        private static readonly HashSet<string> Warned = new HashSet<string>();

        // 살아 있는 유닛 수(진영별 · 시체는 빼고) — Unit이 켜질 때 세고 죽는 순간(또는 죽지 않고 꺼질 때) 뺀다(사용자 10/26).
        // 유닛 수 상한은 진영마다 절반씩 — 한쪽이 넘쳐도 다른 쪽 소환은 막지 않는다
        private static readonly int[] UnitsByTeam = new int[2];
        public static int Units => UnitsByTeam[0] + UnitsByTeam[1];
        public static int UnitsOf(Team team) => UnitsByTeam[(int)team];

        // 이번 전투에서 잰 최대값 · 걸린 경고 — 폭주 시험(Editor/RunawayBattleTest)이 읽는다
        public static int PeakSummonDepth { get; private set; }
        public static int PeakFireDepth { get; private set; }
        public static int PeakStepSummons { get; private set; }
        public static int PeakUnits { get; private set; }
        public static readonly List<string> Warnings = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            UnitsByTeam[0] = UnitsByTeam[1] = 0;
            Clear();
        }

        // 전투 시작(BattleManager.StartBattle)
        public static void Clear()
        {
            _summonDepth = _fireDepth = 0;
            _step = float.NaN;
            StepSummons[0] = StepSummons[1] = 0;
            Warned.Clear();
            Warnings.Clear();
            PeakSummonDepth = PeakFireDepth = PeakStepSummons = 0;
            PeakUnits = Units;
        }

        public static void UnitEnabled(Team team)
        {
            UnitsByTeam[(int)team]++;
            PeakUnits = Mathf.Max(PeakUnits, Units);
        }

        public static void UnitDisabled(Team team) => UnitsByTeam[(int)team] = Mathf.Max(0, UnitsByTeam[(int)team] - 1);

        private static int TeamCap(BattleConfig config) => config.MaxUnits / 2;

        public static bool UnitsFull(Team team) => BattleManager.Config != null && UnitsByTeam[(int)team] >= TeamCap(BattleManager.Config);

        // 소환 하나에 들어간다 — false면 깊이 상한이라 그 소환을 통째로 건너뛴다(ExitSummon도 부르지 않는다)
        public static bool EnterSummon(UnitDefinition unit, BuildingDefinition source)
        {
            BattleConfig config = BattleManager.Config;
            if (config != null && _summonDepth >= config.MaxSummonDepth)
            {
                Warn("소환 사슬 깊이", config.MaxSummonDepth, unit, source);
                return false;
            }
            _summonDepth++;
            PeakSummonDepth = Mathf.Max(PeakSummonDepth, _summonDepth);
            return true;
        }

        public static void ExitSummon() => _summonDepth--;

        // 이번 소환에서 실제로 낼 수 — 스텝 상한 · 유닛 수 상한에 남은 만큼으로 줄인다(0이면 하나도 안 낸다)
        public static int Allow(Team team, int count, UnitDefinition unit, BuildingDefinition source)
        {
            BattleConfig config = BattleManager.Config;
            if (config == null || count <= 0)
                return count;
            if (Time.fixedTime != _step)
            {
                _step = Time.fixedTime;
                StepSummons[0] = StepSummons[1] = 0;
            }
            int side = (int)team;
            int allowed = Mathf.Min(count, config.MaxStepSummons - StepSummons[side]);
            if (allowed < count)
                Warn($"{TeamName(team)} 한 스텝 소환 수", config.MaxStepSummons, unit, source);
            int room = TeamCap(config) - UnitsByTeam[side];
            if (room < allowed)
            {
                allowed = room;
                Warn($"{TeamName(team)} 유닛 수", TeamCap(config), unit, source);
            }
            allowed = Mathf.Max(0, allowed);
            StepSummons[side] += allowed;
            PeakStepSummons = Mathf.Max(PeakStepSummons, StepSummons[side]);
            return allowed;
        }

        // 발동 하나에 들어간다 — false면 깊이 상한이라 그 발동을 건너뛴다(ExitFire도 부르지 않는다)
        public static bool EnterFire(BuildingDefinition building)
        {
            BattleConfig config = BattleManager.Config;
            if (config != null && _fireDepth >= config.MaxFireDepth)
            {
                Warn("발동 사슬 깊이", config.MaxFireDepth, null, building);
                return false;
            }
            _fireDepth++;
            PeakFireDepth = Mathf.Max(PeakFireDepth, _fireDepth);
            return true;
        }

        public static void ExitFire() => _fireDepth--;

        private static string TeamName(Team team) => team == Team.Ally ? "아군" : "적";

        private static void Warn(string limit, int cap, UnitDefinition unit, BuildingDefinition source)
        {
            string cause = $"{limit} 상한({cap}) — 유닛 {(unit != null ? unit.name : "-")} · 기물 {(source != null ? source.name : "-")}";
            if (!Warned.Add(cause))
                return;
            Warnings.Add(cause);
            Debug.LogWarning($"[안전 상한] {cause}. 넘친 것은 건너뛰었다(이 전투에서 같은 원인은 다시 알리지 않는다)");
        }
    }
}

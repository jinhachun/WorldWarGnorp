using System.Collections.Generic;

namespace GnorpWar
{
    // 이번 전투의 준 피해 집계(결과 창 딜 미터기 — 사용자 10/21). 진영마다 유닛 종류별로 센다(사용자 11/1 — 기물이 아니라 유닛 기준).
    // 유닛 = 피해를 낸 유닛의 종류(어느 기물 · 효과 · 업그레이드가 냈든 같은 종류면 한 줄) · 그 유닛이 쏜 투사체는 쏜 유닛이 죽은 뒤에도 그 종류 몫.
    // 기물이 직접 준 피해(뼈 투척 = Core/SlotAttacker · 망루의 화살 = 그 칸을 단 투사체)는 그 칸 몫, 본진 · 작은 타워 = Tower, 처치자를 모르는 피해 = Other.
    // 실제로 깎인 체력만 센다(초과 피해 · 무적으로 막힌 피해 · 같은 편 피해는 빠진다) — 부르는 곳은 Unit.TakeDamage · Base.TakeDamage · Palisade.TakeDamage.
    // 반사 · 범위 · 관통 피해도 그 피해의 attacker 몫이다. 비우는 곳은 BattleManager.StartBattle. 결과 창(UI/BattleSummary)은 읽기만
    public static class DamageMeter
    {
        public const int Other = -1;
        public const int Tower = -2;

        private static readonly Dictionary<int, float>[] Dealt = { new Dictionary<int, float>(), new Dictionary<int, float>() };
        private static readonly Dictionary<UnitDefinition, float>[] ByUnit = { new Dictionary<UnitDefinition, float>(), new Dictionary<UnitDefinition, float>() };

        // 투사체가 맞히는 동안의 출처(Hit ~ EndHit) — attacker 대신 이것으로 센다. 그 투사체를 쏜 진영이 준 피해에만(맞은 쪽의 반사는 제 몫대로)
        private static bool _hitting;
        private static Team _hitTeam;
        private static UnitDefinition _hitUnit;
        private static int _hitSlot = -1;

        public static void Clear()
        {
            Dealt[0].Clear();
            Dealt[1].Clear();
            ByUnit[0].Clear();
            ByUnit[1].Clear();
        }

        // team의 투사체가 맞히기 시작 — unit = 쏜 유닛의 종류(없으면 null) · slot = 쏘게 한 기물의 칸(없으면 -1)
        public static void Hit(Team team, UnitDefinition unit, int slot)
        {
            _hitting = unit != null || slot >= 0;
            _hitTeam = team;
            _hitUnit = unit;
            _hitSlot = slot;
        }

        public static void EndHit() => _hitting = false;

        // victim 진영이 dealt만큼 체력을 잃었다 — 공격한 쪽의 유닛 종류 · 칸에 더한다
        public static void Credit(IDamageable attacker, Team victim, float dealt)
        {
            if (dealt <= 0f || attacker != null && attacker.Team == victim)
                return;
            int book = victim == Team.Ally ? (int)Team.Enemy : (int)Team.Ally;
            bool hit = _hitting && _hitTeam != victim;
            UnitDefinition unit = hit ? (_hitSlot >= 0 ? null : _hitUnit) : attacker is Unit u ? u.Definition : null;
            if (unit != null)
            {
                ByUnit[book][unit] = (ByUnit[book].TryGetValue(unit, out float total) ? total : 0f) + dealt;
                return;
            }
            int key = hit ? _hitSlot
                    : attacker is Base ? Tower
                    : attacker is SlotAttacker slot ? slot.Slot : Other;
            Dealt[book][key] = (Dealt[book].TryGetValue(key, out float sum) ? sum : 0f) + dealt;
        }

        // team이 이번 전투에 준 피해 — 기물이 직접 준 칸(또는 Other · Tower)별
        public static IReadOnlyDictionary<int, float> Of(Team team) => Dealt[(int)team];

        // team의 유닛이 이번 전투에 준 피해 — 유닛 종류별
        public static IReadOnlyDictionary<UnitDefinition, float> UnitsOf(Team team) => ByUnit[(int)team];
    }
}

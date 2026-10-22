using System.Collections.Generic;

namespace GnorpWar
{
    // 이번 전투의 준 피해 집계(결과 창 딜 미터기 — 사용자 10/21). 진영마다 「그 피해를 낸 칸」 몫으로 센다.
    // 칸 = 유닛을 낸 기물의 칸(Unit.SourceSlot) · 기물이 직접 준 피해의 칸(Core/SlotAttacker — 뼈 투척), 칸 없는 소환(유닛 효과 · 업그레이드 · 투사체)과 처치자를 모르는 피해 = Other, 본진 · 작은 타워 = Tower.
    // 실제로 깎인 체력만 센다(초과 피해 · 무적으로 막힌 피해 · 같은 편 피해는 빠진다) — 부르는 곳은 Unit.TakeDamage · Base.TakeDamage 둘.
    // 반사 · 범위 · 관통 피해도 그 피해의 attacker 몫이다. 비우는 곳은 BattleManager.StartBattle. 결과 창(UI/BattleSummary)은 읽기만
    public static class DamageMeter
    {
        public const int Other = -1;
        public const int Tower = -2;

        private static readonly Dictionary<int, float>[] Dealt = { new Dictionary<int, float>(), new Dictionary<int, float>() };

        public static void Clear()
        {
            Dealt[0].Clear();
            Dealt[1].Clear();
        }

        // victim 진영이 dealt만큼 체력을 잃었다 — 공격한 쪽의 칸에 더한다
        public static void Credit(IDamageable attacker, Team victim, float dealt)
        {
            if (dealt <= 0f || attacker != null && attacker.Team == victim)
                return;
            int key = attacker is Unit unit ? (unit.SourceSlot >= 0 ? unit.SourceSlot : Other)
                    : attacker is Base ? Tower
                    : attacker is SlotAttacker slot ? slot.Slot : Other;
            Dictionary<int, float> book = Dealt[victim == Team.Ally ? (int)Team.Enemy : (int)Team.Ally];
            book[key] = (book.TryGetValue(key, out float sum) ? sum : 0f) + dealt;
        }

        // team이 이번 전투에 준 피해 — 칸(또는 Other · Tower)별
        public static IReadOnlyDictionary<int, float> Of(Team team) => Dealt[(int)team];
    }
}

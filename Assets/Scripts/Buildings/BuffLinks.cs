using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 기물이 다른 대상에게 주는 것의 종류 — 선 색이 이걸로 갈린다(전장 Fx/BuffLinkFx · UI 탑)
    public enum BuffKind
    {
        Haste,          // 가속 · 쿨다운 감소 · 공격속도
        Attack,         // 공격력(타워 사거리·화살 수 포함)
        Vitality,       // 체력 · 크기
        Invulnerable,   // 무적
        Heal,           // 회복
        Trigger,        // 다른 기물을 발동시킴 · 다른 기물의 소환을 늘리거나 바꿈
    }

    public struct BuffLink
    {
        public Team Team;
        // 효과를 준 기물의 칸(0 = 1층) — 기물이 아니면(맵의 전체 발동) -1
        public int From;
        // 대상이 기물이면 그 칸, 아니면 -1
        public int To;
        // 대상이 유닛·타워면 그 월드 자리(유닛 무리면 가운데), 기물이면 null
        public Vector2? At;
        public BuffKind Kind;
        // 늘 켜진 효과(풍차)를 대상이 발동할 때마다 다시 알리는 것 — 약하게 그린다
        public bool Weak;
    }

    // "어떤 기물이 누구에게 무엇을 줬나" 알림 한 곳 — 효과를 거는 그 순간, 건 횟수만큼 부른다(그리는 쪽이 솎는다).
    // 게임 규칙은 건드리지 않는다 — 듣는 쪽이 없으면 아무 일도 없다
    public static class BuffLinks
    {
        public static event System.Action<BuffLink> Raised;

        // 늘 켜진 기물 → 기물 효과(진영별) — 전투 시작마다 비운다(BoardRunner.Begin)
        private static readonly List<BuffLink>[] StandingByTeam = { new List<BuffLink>(), new List<BuffLink>() };

        public static void ToSlot(Team team, int from, int to, BuffKind kind)
            => Raised?.Invoke(new BuffLink { Team = team, From = from, To = to, Kind = kind });

        public static void ToPoint(Team team, int from, Vector2 at, BuffKind kind)
            => Raised?.Invoke(new BuffLink { Team = team, From = from, To = -1, At = at, Kind = kind });

        // 내 타워 전부(본진 + 작은 타워)에게
        public static void ToTowers(Team team, int from, BuffKind kind)
        {
            if (Raised == null || from < 0)
                return;
            Base main = Base.For(team);
            if (main != null && main.IsAlive)
                ToPoint(team, from, main.Bounds.center, kind);
            foreach (Base small in Base.SmallTowers())
                if (small.Team == team)
                    ToPoint(team, from, small.Bounds.center, kind);
        }

        // 내 유닛 무리에게(진영 단위 스탯) — 선 하나를 무리 가운데로. filter를 비우면 전부
        public static void ToUnits(Team team, int from, BuffKind kind, IUnitFilter filter = null)
        {
            if (Raised == null)
                return;
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == team && (filter == null || filter.AppliesTo(unit.Definition)))
                {
                    sum += unit.Position;
                    count++;
                }
            if (count > 0)
                ToPoint(team, from, sum / count, kind);
        }

        // 유닛 효과(진영 단위)가 낸 것 — 그 효과를 켠 기물의 칸에서 나간다
        public static void EffectToPoint(Team team, UnitEffect effect, Vector2 at, BuffKind kind)
        {
            if (Raised == null)
                return;
            for (int slot = NextSlotOf(team, effect, 0); slot >= 0; slot = NextSlotOf(team, effect, slot + 1))
                ToPoint(team, slot, at, kind);
        }

        public static void EffectToSlot(Team team, UnitEffect effect, int to, BuffKind kind)
        {
            if (Raised == null)
                return;
            for (int slot = NextSlotOf(team, effect, 0); slot >= 0; slot = NextSlotOf(team, effect, slot + 1))
                if (slot != to)
                    ToSlot(team, slot, to, kind);
        }

        private static int NextSlotOf(Team team, UnitEffect effect, int start)
        {
            BoardRunner board = BoardRunner.For(team);
            if (board == null)
                return -1;
            for (int i = start; i < board.Slots.Length; i++)
                if (board.Slots[i].Building != null && System.Array.IndexOf(board.Slots[i].Building.Definition.Effects, effect) >= 0)
                    return i;
            return -1;
        }

        // 이 기물이 놓인 칸 — 필드에 없으면(보관함) -1. 「구매할 때마다,」처럼 칸을 안 받는 훅에서
        public static int SlotOf(Team team, OwnedBuilding building)
        {
            BoardRunner board = BoardRunner.For(team);
            return board != null && board.Field != null ? System.Array.IndexOf(board.Field, building) : -1;
        }

        // 늘 켜진 기물 → 기물 효과 — 지금 한 번 알리고, 대상이 발동할 때마다 약하게 다시 알린다
        public static void Standing(Team team, int from, int to, BuffKind kind)
        {
            StandingByTeam[(int)team].Add(new BuffLink { Team = team, From = from, To = to, Kind = kind, Weak = true });
            ToSlot(team, from, to, kind);
        }

        public static void ClearStanding(Team team) => StandingByTeam[(int)team].Clear();

        public static void SlotFired(Team team, int slot)
        {
            if (Raised == null)
                return;
            foreach (BuffLink link in StandingByTeam[(int)team])
                if (link.To == slot)
                    Raised.Invoke(link);
        }

        public static BuffKind KindOf(UnitStat stat)
        {
            switch (stat)
            {
                case UnitStat.Attack: return BuffKind.Attack;
                case UnitStat.MaxHp: return BuffKind.Vitality;
                default: return BuffKind.Haste;
            }
        }

        public static BuffKind KindOf(TowerStat stat)
        {
            switch (stat)
            {
                case TowerStat.MaxHp: return BuffKind.Vitality;
                case TowerStat.AttackSpeed: return BuffKind.Haste;
                default: return BuffKind.Attack;
            }
        }
    }
}

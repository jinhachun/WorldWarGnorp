using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 기물이 다른 대상에게 주는 것의 종류 — 「대상에게 무엇이 바뀌나」로 가른다(사용자 10/14). 선 색이 이걸로 갈린다(전장 Fx/BuffLinkFx · UI 탑)
    public enum BuffKind
    {
        Summon,         // 유닛이 더 나오거나 다른 유닛으로 바뀐다 — 추가 · 조건 소환 · 소환 수 늘림 · 변환 · 부활
        Stat,           // 내 유닛 · 타워의 능력치가 오른다 — 공격력 · 공격속도 · 체력 · 회복 · 크기 · 이동속도 · 사거리 · 화살 수
        Invulnerable,   // 무적
        Haste,          // 기물의 쿨다운이 빨리 돈다 — 가속 · 쿨다운 감소(유닛 공격속도는 Stat)
        Debuff,         // 적을 약하게 한다 — 적 능력치 감소 · 감속 · 체력을 잃게 함 · 불태움
        Trigger,        // 다른 기물을 즉시 발동시키거나 그 효과를 한 번 더 · 기물을 멈춤(제물의 의식)
    }

    // 버프 글자(대상 위에 뜨는 「[아이콘] +5%」 — Fx/BuffTexts)의 아이콘. 아이콘이 없는 스탯은 글에 이름을 쓴다
    public enum BuffIcon { None, Attack, Hp, Cooldown, AttackSpeed }

    // 버프 글자 한 줄 — 스탯 · 쿨다운 배율만(사용자 10/15). Text가 null이면 글자 없음
    public struct BuffLabel
    {
        public BuffIcon Icon;
        public string Text;

        public static BuffLabel Of(StatModifier m)
            => m.Stat == UnitStat.Attack ? New(BuffIcon.Attack, m.ValueText) : m.Stat == UnitStat.MaxHp ? New(BuffIcon.Hp, m.ValueText)
                : m.Stat == UnitStat.AttackSpeed ? New(BuffIcon.AttackSpeed, m.ValueText) : New(BuffIcon.None, m.Describe());

        public static BuffLabel Of(TowerModifier m)
            => m.Stat == TowerStat.Attack ? New(BuffIcon.Attack, m.ValueText) : m.Stat == TowerStat.MaxHp ? New(BuffIcon.Hp, m.ValueText)
                : m.Stat == TowerStat.AttackSpeed ? New(BuffIcon.AttackSpeed, m.ValueText) : New(BuffIcon.None, $"{m.Name} {m.ValueText}");

        // 쿨다운에 곱한 값 — 「×0.8」
        public static BuffLabel Cooldown(float factor) => New(BuffIcon.Cooldown, new StatModifier(UnitStat.Attack, StatOp.Multiply, factor).ValueText);

        private static BuffLabel New(BuffIcon icon, string text) => new BuffLabel { Icon = icon, Text = text };
    }

    public struct BuffLink
    {
        public Team Team;
        // 효과를 준 기물의 칸(0 = 1층) — 기물이 아니면(맵의 전체 발동) -1
        public int From;
        // 유닛이 준 것이면 그 유닛의 월드 자리(죽으며 준 것이면 죽은 자리) — 선은 층 대신 여기서 나간다. From은 그 효과를 켠 기물의 칸(솎기 열쇠)
        public Vector2? FromAt;
        // 대상이 기물이면 그 칸, 아니면 -1
        public int To;
        // 대상이 유닛·타워면 그 월드 자리(유닛 무리면 가운데), 기물이면 null
        public Vector2? At;
        // 대상이 타워면 그 진영 안에서의 타워 번호(본진 1, 작은 타워 2부터), 아니면 0 — 전장 선이 타워마다 하나씩 남게(BuffLinkFx의 솎기)
        public int Tower;
        public BuffKind Kind;
        // 늘 켜진 효과(풍차)를 대상이 발동할 때마다 다시 알리는 것 — 약하게 그린다
        public bool Weak;
        // 대상 위에 띄울 버프 글자(없으면 Text = null)
        public BuffLabel Label;
    }

    // "어떤 기물이 누구에게 무엇을 줬나" 알림 한 곳 — 효과를 거는 그 순간, 건 횟수만큼 부른다(그리는 쪽이 솎는다).
    // 게임 규칙은 건드리지 않는다 — 듣는 쪽이 없으면 아무 일도 없다
    public static class BuffLinks
    {
        public static event System.Action<BuffLink> Raised;

        // 늘 켜진 기물 → 기물 효과(진영별) — 전투 시작마다 비운다(BoardRunner.Begin)
        private static readonly List<BuffLink>[] StandingByTeam = { new List<BuffLink>(), new List<BuffLink>() };

        public static void ToSlot(Team team, int from, int to, BuffKind kind, BuffLabel label = default)
            => Raised?.Invoke(new BuffLink { Team = team, From = from, To = to, Kind = kind, Label = label });

        public static void ToPoint(Team team, int from, Vector2 at, BuffKind kind, BuffLabel label = default)
            => Raised?.Invoke(new BuffLink { Team = team, From = from, To = -1, At = at, Kind = kind, Label = label });

        // 내 타워 전부(본진 + 작은 타워)에게 — 선은 각 타워의 화살이 나가는 자리로
        public static void ToTowers(Team team, int from, BuffKind kind, BuffLabel label = default)
        {
            if (Raised == null || from < 0)
                return;
            Base main = Base.For(team);
            if (main != null && main.IsAlive)
                Raised.Invoke(new BuffLink { Team = team, From = from, To = -1, At = main.Muzzle, Tower = 1, Kind = kind, Label = label });
            int number = 1;
            foreach (Base small in Base.SmallTowers())
                if (small.Team == team)
                    Raised.Invoke(new BuffLink { Team = team, From = from, To = -1, At = small.Muzzle, Tower = ++number, Kind = kind, Label = label });
        }

        // 내 유닛 무리에게(진영 단위 스탯) — 선 하나를 무리 가운데로. filter를 비우면 전부
        public static void ToUnits(Team team, int from, BuffKind kind, IUnitFilter filter = null, BuffLabel label = default)
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
                ToPoint(team, from, sum / count, kind, label);
        }

        // 기물이 한 일이 전장의 한 자리에서 다른 자리로 이어진다(조립 라인 — 소모된 무리 가운데 → 거인). 솎기 열쇠는 그 기물의 칸
        public static void FromPoint(Team team, int slot, Vector2 from, Vector2 to, BuffKind kind)
            => Raised?.Invoke(new BuffLink { Team = team, From = slot, To = -1, FromAt = from, At = to, Kind = kind });

        // 업그레이드 · 지역 특수(켠 기물이 없다)가 낸 것 — 그 진영 본진의 화살 발사점에서 나간다(사용자 10/20 Q1). 본진이 없으면 긋지 않는다
        public static void FromBase(Team team, Vector2 at, BuffKind kind, BuffLabel label = default)
        {
            if (Raised != null && Base.For(team) is Base home && home.IsAlive)
                Raised.Invoke(new BuffLink { Team = team, From = -1, To = -1, FromAt = home.Muzzle, At = at, Kind = kind, Label = label });
        }

        public static void FromBaseToSlot(Team team, int to, BuffKind kind, BuffLabel label = default)
        {
            if (Raised != null && Base.For(team) is Base home && home.IsAlive)
                Raised.Invoke(new BuffLink { Team = team, From = -1, To = to, FromAt = home.Muzzle, Kind = kind, Label = label });
        }

        // 유닛 효과(진영 단위)가 낸 것 — 그 효과를 켠 기물의 칸에서 나간다
        public static void EffectToPoint(Team team, UnitEffect effect, Vector2 at, BuffKind kind, BuffLabel label = default)
        {
            if (Raised == null)
                return;
            for (int slot = NextSlotOf(team, effect, 0); slot >= 0; slot = NextSlotOf(team, effect, slot + 1))
                ToPoint(team, slot, at, kind, label);
        }

        public static void EffectToSlot(Team team, UnitEffect effect, int to, BuffKind kind)
        {
            if (Raised == null)
                return;
            for (int slot = NextSlotOf(team, effect, 0); slot >= 0; slot = NextSlotOf(team, effect, slot + 1))
                if (slot != to)
                    ToSlot(team, slot, to, kind);
        }

        // 유닛 효과를 유닛이 냈다(죽으며 · 처치하며 · 소환되며 등) — 선은 그 유닛의 자리(from)에서 대상(to)으로.
        // 그 효과를 켠 기물이 없으면(유닛의 늘 켜진 효과 · 합치기 보상 등 — 사용자 10/20) 기물 칸 없이 유닛에서만 긋는다(두 자리가 거의 같으면 긋지 않는다).
        // 기물이 있고 두 자리가 거의 같으면(그 자리에 소환 등) 선이 안 보이므로 켠 기물의 층에서 긋는다
        public static void EffectFromUnit(Team team, UnitEffect effect, Vector2 from, Vector2 to, BuffKind kind, BuffLabel label = default)
        {
            if (Raised == null)
                return;
            int slot = NextSlotOf(team, effect, 0);
            if (slot < 0)
            {
                if ((to - from).sqrMagnitude >= MinUnitLink * MinUnitLink)
                    Raised.Invoke(new BuffLink { Team = team, From = -1, To = -1, FromAt = from, At = to, Kind = kind, Label = label });
                return;
            }
            if ((to - from).sqrMagnitude < MinUnitLink * MinUnitLink)
                EffectToPoint(team, effect, to, kind, label);
            else
                Raised.Invoke(new BuffLink { Team = team, From = slot, To = -1, FromAt = from, At = to, Kind = kind, Label = label });
        }

        // 유닛에서 그 진영의 유닛 무리(조건에 맞는 것) 가운데로 — 무리가 없으면 긋지 않는다
        public static void EffectFromUnitToUnits(Team team, UnitEffect effect, Vector2 from, Team recipients, System.Func<Unit, bool> filter, BuffKind kind, BuffLabel label = default)
        {
            if (Raised == null || NextSlotOf(team, effect, 0) < 0 || Throttled(team, effect))
                return;
            if (Centroid(recipients, filter, out Vector2 at))
                EffectFromUnit(team, effect, from, at, kind, label);
        }

        // 무리 가운데를 찾는 건 유닛 전부를 훑는다 — 공격 · 회복마다 부르는 효과가 많으면 비싸므로, 같은 효과는 그리는 쪽의 솎기 시간(BuffLinkFx)만큼 건너뛴다
        private const float CentroidGap = 0.15f;
        private static readonly Dictionary<(Team, UnitEffect), float> LastCentroid = new Dictionary<(Team, UnitEffect), float>();

        private static bool Throttled(Team team, UnitEffect effect)
        {
            float now = Time.unscaledTime;
            if (LastCentroid.TryGetValue((team, effect), out float last) && now - last < CentroidGap && now >= last)
                return true;
            LastCentroid[(team, effect)] = now;
            return false;
        }

        // 유닛에서 기물로(마구간 신화 — 처치한 유닛 → 발동한 그 기물의 층). From = To라 UI 탑은 긋지 않는다(층 → 층 선만 그린다)
        public static void EffectFromUnitToSlot(Team team, UnitEffect effect, Vector2 from, int to, BuffKind kind)
        {
            if (Raised != null && NextSlotOf(team, effect, 0) >= 0)
                Raised.Invoke(new BuffLink { Team = team, From = to, To = to, FromAt = from, Kind = kind });
        }

        // 유닛 효과를 켠 기물의 층에서 그 진영의 유닛 무리(조건에 맞는 것) 가운데로 — 유닛이 아니라 시각 · 진영이 낸 것(교대근무계획서 신화)
        public static void EffectToUnits(Team team, UnitEffect effect, System.Func<Unit, bool> filter, BuffKind kind, BuffLabel label = default)
        {
            if (Raised != null && NextSlotOf(team, effect, 0) >= 0 && !Throttled(team, effect) && Centroid(team, filter, out Vector2 at))
                EffectToPoint(team, effect, at, kind, label);
        }

        // 기물의 층에서 어느 진영이든 유닛 무리(조건에 맞는 것) 가운데로 — 적에게 거는 것(디버프)도
        public static void ToUnitsOf(Team team, int from, Team recipients, System.Func<Unit, bool> filter, BuffKind kind, BuffLabel label = default)
        {
            if (Raised != null && Centroid(recipients, filter, out Vector2 at))
                ToPoint(team, from, at, kind, label);
        }

        private static bool Centroid(Team team, System.Func<Unit, bool> filter, out Vector2 at)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == team && (filter == null || filter(unit)))
                {
                    sum += unit.Position;
                    count++;
                }
            at = count > 0 ? sum / count : Vector2.zero;
            return count > 0;
        }

        // 유닛 → 대상 선이 이보다(칸) 짧으면 층에서 긋는다
        private const float MinUnitLink = 0.5f;

        private static int NextSlotOf(Team team, UnitEffect effect, int start)
        {
            BoardRunner board = BoardRunner.For(team);
            if (board == null)
                return -1;
            for (int i = start; i < board.Slots.Length; i++)
                if (board.Slots[i].Building != null && System.Array.IndexOf(board.Slots[i].Building.Effects, effect) >= 0)
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
        public static void Standing(Team team, int from, int to, BuffKind kind, BuffLabel label = default)
        {
            StandingByTeam[(int)team].Add(new BuffLink { Team = team, From = from, To = to, Kind = kind, Weak = true });
            ToSlot(team, from, to, kind, label);
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
    }
}

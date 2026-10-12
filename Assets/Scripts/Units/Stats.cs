using System.Collections.Generic;

namespace GnorpWar
{
    // 기획서 §6 스탯 표기. 직렬화되므로 맨 뒤에만 추가할 것
    public enum UnitStat { Attack, MaxHp, MoveSpeed, AttackSpeed }

    // 고정 +5 · 합연산 +5%(0.05) · 곱연산 ×1.1(1.1)
    public enum StatOp { Flat, Percent, Multiply }

    [System.Serializable]
    public struct StatModifier
    {
        public UnitStat Stat;
        public StatOp Op;
        public float Value;

        public StatModifier(UnitStat stat, StatOp op, float value)
        {
            Stat = stat;
            Op = op;
            Value = value;
        }

        private static readonly string[] Names = { "공격력", "체력", "이동속도", "공격속도" };

        // 기획서 §6 표기 그대로 — "공격력 +5" · "공격력 +5%" · "공격력 ×1.1"
        public string Describe() => $"{Name} {ValueText}";
        public string Name => Names[(int)Stat];
        public string ValueText => Op == StatOp.Flat ? $"+{Value:0.##}" : Op == StatOp.Percent ? $"+{Value * 100f:0.##}%" : $"×{Value:0.##}";

        // 등급 값 — Value에 등급마다 perTier를 더한 사본
        public StatModifier Scaled(float perTier, int level) => new StatModifier(Stat, Op, OwnedBuilding.Scaled(Value, perTier, level));
    }

    // 타워(본진) 스탯. 직렬화되므로 맨 뒤에만 추가할 것
    public enum TowerStat { Attack, AttackSpeed, Range, ArrowCount, MaxHp }

    [System.Serializable]
    public struct TowerModifier
    {
        public TowerStat Stat;
        public StatOp Op;
        public float Value;

        public TowerModifier(TowerStat stat, StatOp op, float value)
        {
            Stat = stat;
            Op = op;
            Value = value;
        }

        private static readonly string[] Names = { "공격력", "공격속도", "사거리", "한 번에 발사하는 화살 수", "체력" };

        public string Name => Names[(int)Stat];
        public string ValueText => new StatModifier(UnitStat.Attack, Op, Value).ValueText;

        // 등급 값 — Value에 등급마다 perTier를 더한 사본
        public TowerModifier Scaled(float perTier, int level) => new TowerModifier(Stat, Op, OwnedBuilding.Scaled(Value, perTier, level));
    }

    // 진영 단위 타워 스탯 변경 — StatBook과 같은 규칙(전투 동안 · 영구히), 대상 고르기는 없다(내 타워 전부)
    public static class TowerBook
    {
        private static readonly List<TowerModifier>[] Battle = { new List<TowerModifier>(), new List<TowerModifier>() };
        // 영구히 — 진영마다 「지금 쓰는 탑」의 목록(RunBook)
        private static readonly List<TowerModifier>[] Run = { RunBook.Of(Team.Ally).Towers, RunBook.Of(Team.Enemy).Towers };

        internal static void UseRun(Team team, List<TowerModifier> list)
        {
            Run[(int)team] = list;
            Version++;
        }

        public static int Version { get; private set; }

        public static void AddForBattle(Team team, TowerModifier modifier) => Add(Battle[(int)team], modifier);
        public static void AddForRun(Team team, TowerModifier modifier) => Add(Run[(int)team], modifier);

        // 같은 스탯·방식은 한 줄로 합친다(+는 더하고 ×는 곱한다 — 계산 결과는 같다). 처치마다 쌓는 기물로 목록이 길어지지 않게
        private static void Add(List<TowerModifier> list, TowerModifier modifier)
        {
            Version++;
            for (int i = 0; i < list.Count; i++)
            {
                TowerModifier m = list[i];
                if (m.Stat != modifier.Stat || m.Op != modifier.Op)
                    continue;
                m.Value = m.Op == StatOp.Multiply ? m.Value * modifier.Value : m.Value + modifier.Value;
                list[i] = m;
                return;
            }
            list.Add(modifier);
        }

        // 데스매치에서도 타워가 공격하는 진영(오버드라이브) — 전투 동안
        private static readonly bool[] Deathmatch = new bool[2];

        public static bool AttacksInDeathmatch(Team team) => Deathmatch[(int)team];
        public static void AttackInDeathmatch(Team team) => Deathmatch[(int)team] = true;

        // 작은 타워가 앞으로 걸어가는 진영(움직이는 성) — 전투 동안. Speed 0 = 걷지 않는다
        public struct FollowRule
        {
            public float Speed;
            public float Spacing;
        }

        private static readonly FollowRule[] Follows = new FollowRule[2];

        public static FollowRule FollowOf(Team team) => Follows[(int)team];
        public static void SetFollow(Team team, FollowRule rule) => Follows[(int)team] = rule;

        public static void ClearBattle()
        {
            foreach (List<TowerModifier> list in Battle)
                list.Clear();
            Deathmatch[0] = Deathmatch[1] = false;
            Follows[0] = Follows[1] = default;
            Version++;
        }

        public static void ClearRun()
        {
            foreach (List<TowerModifier> list in Run)
                list.Clear();
            Version++;
        }

        // ownPercent = 그 타워 한 채에만 걸린 합연산 %(조립식 감시탑의 등급 사거리) — 진영 %와 더해진다
        public static float Compute(Team team, TowerStat stat, float baseValue, float ownPercent = 0f)
        {
            StatSum sum = StatSum.Identity;
            sum.Add(StatOp.Percent, ownPercent);
            foreach (TowerModifier m in Battle[(int)team])
                if (m.Stat == stat)
                    sum.Add(m.Op, m.Value);
            foreach (TowerModifier m in Run[(int)team])
                if (m.Stat == stat)
                    sum.Add(m.Op, m.Value);
            return sum.Apply(baseValue);
        }
    }

    // 스탯 변경을 받을 유닛을 고른다 — 유닛 효과(UnitEffect)와 스탯을 거는 기물 능력이 쓴다
    public interface IUnitFilter
    {
        bool AppliesTo(UnitDefinition unit);
    }

    // "A는 B에게 적용되는 효과를 함께 받는다"(조립 라인: 톱니거인 → 톱니병사). 전투 시작마다 비우고(BattleManager) 패시브가 건다.
    // 🔴 효과·스탯이 유닛에게 적용되는지는 AppliesTo가 아니라 AppliesFor(진영 포함)로 본다
    public static class UnitAliases
    {
        private static readonly Dictionary<UnitDefinition, UnitDefinition>[] ByTeam =
            { new Dictionary<UnitDefinition, UnitDefinition>(), new Dictionary<UnitDefinition, UnitDefinition>() };

        public static void Clear()
        {
            foreach (Dictionary<UnitDefinition, UnitDefinition> aliases in ByTeam)
                aliases.Clear();
            StatBook.MarkChanged();
        }

        public static void Add(Team team, UnitDefinition unit, UnitDefinition alsoAs)
        {
            ByTeam[(int)team][unit] = alsoAs;
            StatBook.MarkChanged();
        }

        public static bool AppliesFor(this IUnitFilter filter, Team team, UnitDefinition unit)
            => filter.AppliesTo(unit) || (ByTeam[(int)team].TryGetValue(unit, out UnitDefinition alias) && filter.AppliesTo(alias));
    }

    // 스탯 하나에 걸린 값을 모아 기획서 §6 순서로 계산 — (기본 + 고정 합) × (1 + % 합) × 곱들
    public struct StatSum
    {
        public float Flat;
        public float Percent;
        public float Multiply;

        public static StatSum Identity => new StatSum { Multiply = 1f };

        public void Add(StatOp op, float value)
        {
            if (op == StatOp.Flat)
                Flat += value;
            else if (op == StatOp.Percent)
                Percent += value;
            else
                Multiply *= value;
        }

        public float Apply(float baseValue) => (baseValue + Flat) * (1f + Percent) * Multiply;
    }

    // 진영 단위로 걸린 스탯 변경. 전투 동안 = 전투 시작마다 비움 · 영구히 = 런 동안(진영별 — 적도 상점을 돈다. 기물을 팔아도 남는다).
    // 적용 대상은 걸어 준 쪽(효과·기물 능력)의 AppliesTo(병종 태그·유닛 이름). 바뀔 때마다 Version이 올라 유닛이 다시 계산한다
    public static class StatBook
    {
        internal struct Entry
        {
            public IUnitFilter Source;
            public StatModifier Modifier;
        }

        private static readonly List<Entry>[] Battle = { new List<Entry>(), new List<Entry>() };
        // 영구히 — 진영마다 「지금 쓰는 탑」의 목록(RunBook)
        private static readonly List<Entry>[] Run = { RunBook.Of(Team.Ally).Units, RunBook.Of(Team.Enemy).Units };

        internal static void UseRun(Team team, List<Entry> list)
        {
            Run[(int)team] = list;
            Version++;
        }

        public static int Version { get; private set; }

        public static void AddForBattle(Team team, IUnitFilter source, StatModifier modifier) => Add(Battle[(int)team], source, modifier);
        public static void AddForRun(Team team, IUnitFilter source, StatModifier modifier) => Add(Run[(int)team], source, modifier);

        public static void ClearBattle()
        {
            foreach (List<Entry> entries in Battle)
                entries.Clear();
            Version++;
        }

        public static void ClearRun()
        {
            foreach (List<Entry> entries in Run)
                entries.Clear();
            Version++;
        }

        // 스탯 계산에 쓰는 다른 것(별칭 등)이 바뀌었다 — 유닛이 다시 계산하게
        public static void MarkChanged() => Version++;

        // 같은 효과·스탯·방식은 한 줄로 합친다 — 발동마다 쌓는 효과(허수아비 등)로 목록이 길어지지 않게
        private static void Add(List<Entry> entries, IUnitFilter source, StatModifier modifier)
        {
            Version++;
            for (int i = 0; i < entries.Count; i++)
            {
                StatModifier m = entries[i].Modifier;
                if (entries[i].Source != source || m.Stat != modifier.Stat || m.Op != modifier.Op)
                    continue;
                m.Value = m.Op == StatOp.Multiply ? m.Value * modifier.Value : m.Value + modifier.Value;
                entries[i] = new Entry { Source = source, Modifier = m };
                return;
            }
            entries.Add(new Entry { Source = source, Modifier = modifier });
        }

        public static void Accumulate(Team team, UnitDefinition unit, UnitStat stat, ref StatSum sum)
        {
            Accumulate(Battle[(int)team], team, unit, stat, ref sum);
            Accumulate(Run[(int)team], team, unit, stat, ref sum);
        }

        // 영구히 걸린 것만(아군, 런 동안) — 전투의 유닛 계산과 RunStats.Preview가 같이 쓴다.
        // alias = 전투 시작 때 걸릴 별칭(상점에서는 아직 UnitAliases에 없다 — Preview가 필드에서 찾아 넘긴다)
        public static void AccumulateRun(UnitDefinition unit, UnitStat stat, ref StatSum sum, UnitDefinition alias = null)
            => Accumulate(Run[(int)Team.Ally], Team.Ally, unit, stat, ref sum, alias);

        // 탑 한 채가 영구히 쌓은 것만 — 지금 그 진영에 걸려 있지 않은 탑(지도에서 고른 영주)의 값을 볼 때(RunStats.Preview)
        public static void AccumulateBook(RunBook book, Team team, UnitDefinition unit, UnitStat stat, ref StatSum sum, UnitDefinition alias = null)
            => Accumulate(book.Units, team, unit, stat, ref sum, alias);

        private static void Accumulate(List<Entry> entries, Team team, UnitDefinition unit, UnitStat stat, ref StatSum sum, UnitDefinition alias = null)
        {
            foreach (Entry e in entries)
                if (e.Modifier.Stat == stat && (e.Source.AppliesFor(team, unit) || (alias != null && e.Source.AppliesTo(alias))))
                    sum.Add(e.Modifier.Op, e.Modifier.Value);
        }
    }

    // 탑 한 채가 「영구히」 쌓은 강화(유닛 · 타워) — 탑마다 하나(TowerState.Book). 진영마다 「지금 쓰는 탑」의 것이 StatBook · TowerBook의 영구히 목록이 된다.
    // 평소엔 플레이어 탑과 적 탑의 것이고, 다른 탑이 상점을 도는 동안(RunState.ShopAs)만 그 탑의 것으로 바뀐다
    public class RunBook
    {
        private static readonly RunBook[] Current = { new RunBook(), new RunBook() };

        internal readonly List<StatBook.Entry> Units = new List<StatBook.Entry>();
        internal readonly List<TowerModifier> Towers = new List<TowerModifier>();

        public static RunBook Of(Team team) => Current[(int)team];

        public static void Use(Team team, RunBook book)
        {
            Current[(int)team] = book;
            StatBook.UseRun(team, book.Units);
            TowerBook.UseRun(team, book.Towers);
        }
    }

    // 유닛 카드의 「공격력 11+1」용(전투 밖) — 유닛의 스탯 기본값과, 전투가 시작될 때 갖게 될 값. 내 유닛, 또는 지도에서 고른 영주의 유닛.
    // 넣는 것: 영구히 붙은 것 + 필드 기물이 런 동안 쌓아 전투 시작 때 거는 것(스패너 — BuildingPassive.BattleStartStat, 사용자 10/10) + 지역 업그레이드.
    // 조립 라인의 별칭(톱니거인 → 톱니병사)도 필드에 있으면 따른다. 전투 중에 쌓이는 것·버프·맵은 넣지 않는다. 계산은 전투의 유닛과 같은 길(StatSum)
    public static class RunStats
    {
        public struct Value
        {
            public float Base;
            public float Now;
            public float Bonus => Now - Base;
        }

        // 기본값은 유닛의 계산과 같은 곳에서(Unit.BaseStat)
        // enemy = 지도에서 고른 땅의 영주의 유닛(RunState.PreviewLord) — 그 영주의 탑 · 그 탑이 영구히 쌓은 강화 · 그 영주의 지역 업그레이드로 계산한다.
        // 지역 업그레이드는 전투가 시작될 때 걸리는 값 그대로다(상대가 정해져 있으면 민족주의까지). 맵 효과는 넣지 않는다 — 어느 땅에서 싸울지는 침략할 때 정해진다
        public static Value Preview(UnitDefinition unit, UnitStat stat, bool enemy = false)
        {
            float baseValue = Unit.BaseStat(unit, stat);
            if (!RunState.Started)
                return new Value { Base = baseValue, Now = baseValue };
            Lord player = RunState.World.State.Player, other = RunState.PreviewLord;
            Lord lord = enemy ? other : player;
            if (lord == null)
                return new Value { Base = baseValue, Now = baseValue };
            // 전투에서 효과를 내는 필드 기물만 — 보관함은 안 걸리고, 늪지에서는 쿨다운 없는 기물이 꺼진다(BoardRunner.IsActive와 같은 조건)
            var active = new List<OwnedBuilding>();
            foreach (OwnedBuilding owned in enemy ? RunState.EnemyField : RunState.Field)
                if (!OwnedBuilding.IsEmpty(owned) && !BoardRunner.StoppedByMap(owned))
                    active.Add(owned);

            UnitDefinition alias = null;
            foreach (OwnedBuilding owned in active)
                foreach (BuildingPassive passive in owned.Definition.Passives)
                    alias = passive.AliasOf(unit) ?? alias;

            StatSum sum = StatSum.Identity;
            if (enemy)
                StatBook.AccumulateBook(RunState.PreviewBook, Team.Enemy, unit, stat, ref sum, alias);
            else
                StatBook.AccumulateRun(unit, stat, ref sum, alias);
            foreach (OwnedBuilding owned in active)
                foreach (BuildingPassive passive in owned.Definition.Passives)
                    if (passive is IUnitFilter filter && (filter.AppliesTo(unit) || (alias != null && filter.AppliesTo(alias)))
                        && passive.BattleStartStat(owned, stat, out StatModifier modifier))
                        sum.Add(modifier.Op, modifier.Value);
            RunState.World.ForEachUnitUpgrade(lord, enemy ? player : other, (upgrade, modifier) =>
            {
                if (modifier.Stat == stat)
                    sum.Add(modifier.Op, modifier.Value);
            });
            return new Value { Base = baseValue, Now = sum.Apply(baseValue) };
        }
    }
}

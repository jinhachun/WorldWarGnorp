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
        public string ValueText => Op == StatOp.Flat ? $"{Value:+0.##;-0.##}" : Op == StatOp.Percent ? $"{Value * 100f:+0.##;-0.##}%" : $"×{Value:0.##}";   // 줄어든 값은 「-50%」(반군 막사의 이동속도)

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

        private static readonly string[] Names = { "공격력", "공격속도", "사거리", "화살 수", "체력" };

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
            if (Shared[0] != null || Shared[1] != null || ScaledShares[0].Count > 0 || ScaledShares[1].Count > 0)
                StatBook.MarkChanged();   // 타워 효과를 함께 받는 유닛이 다시 계산하게
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

        // 이 진영의 타워 스탯 변경(전투 동안 · 영구히 전부)을 함께 받는 유닛들(화살 강화기 신화: 궁병) — 전투 동안. 없으면 null.
        // 공격력 · 공격속도 · 체력은 유닛의 스탯 계산(Unit.TeamSum)에 같은 방식으로 얹히고, 사거리 · 화살 수는 스탯이 아니라 SharedCompute로 읽는다.
        // 스탯이 아닌 타워 효과(데스매치 공격 · 처치 시 추가 화살 · 피해 반사 · 걷기)는 넘어가지 않는다
        private static readonly IUnitFilter[] Shared = new IUnitFilter[2];

        public static void ShareWithUnits(Team team, IUnitFilter units)
        {
            Shared[(int)team] = units;
            StatBook.MarkChanged();
        }

        // 타워 스탯 변경을 비율만큼 함께 받는 유닛들(사격 통제소: 내 궁병 60 · 80 · 100%) — 전투 동안. 기물 한 채마다 한 줄, 같은 유닛에 여러 줄(화살 강화기 신화의 100% 포함)이면 비율을 더한다
        private static readonly List<(IUnitFilter units, float ratio)>[] ScaledShares = { new List<(IUnitFilter, float)>(), new List<(IUnitFilter, float)>() };

        public static void ShareWithUnits(Team team, IUnitFilter units, float ratio)
        {
            ScaledShares[(int)team].Add((units, ratio));
            StatBook.MarkChanged();
        }

        // unit이 타워 스탯 변경을 받는 비율(0 = 안 받는다)
        private static float ShareOf(Team team, UnitDefinition unit)
        {
            float ratio = Shared[(int)team] != null && Shared[(int)team].AppliesFor(team, unit) ? 1f : 0f;
            foreach ((IUnitFilter units, float r) in ScaledShares[(int)team])
                if (units.AppliesFor(team, unit))
                    ratio += r;
            return ratio;
        }

        // 변경 하나를 ratio만큼 — 고정 · 합연산은 값 × 비율, 곱연산은 늘어난 몫(×1.1의 0.1) × 비율
        private static void AddScaled(ref StatSum sum, TowerModifier m, float ratio)
            => sum.Add(m.Op, m.Op == StatOp.Multiply ? 1f + (m.Value - 1f) * ratio : m.Value * ratio);

        private static void AccumulateTower(Team team, TowerStat stat, float ratio, ref StatSum sum)
        {
            foreach (TowerModifier m in Battle[(int)team])
                if (m.Stat == stat)
                    AddScaled(ref sum, m, ratio);
            foreach (TowerModifier m in Run[(int)team])
                if (m.Stat == stat)
                    AddScaled(ref sum, m, ratio);
        }

        public static void AccumulateShared(Team team, UnitDefinition unit, UnitStat stat, ref StatSum sum)
        {
            if (stat == UnitStat.MoveSpeed)
                return;
            float ratio = ShareOf(team, unit);
            if (ratio <= 0f)
                return;
            TowerStat tower = stat == UnitStat.Attack ? TowerStat.Attack : stat == UnitStat.MaxHp ? TowerStat.MaxHp : TowerStat.AttackSpeed;
            AccumulateTower(team, tower, ratio, ref sum);
        }

        // 유닛의 사거리를 바꾸는 것(감시 망원경: 내 궁병 +10%) — 전투 동안, 합연산. 타워 몫을 함께 받는 유닛이면 그 몫과 한 합에 더한다(UnitRange)
        private static readonly List<(IUnitFilter units, float percent)>[] UnitRanges = { new List<(IUnitFilter, float)>(), new List<(IUnitFilter, float)>() };

        public static void AddUnitRange(Team team, IUnitFilter units, float percent)
        {
            UnitRanges[(int)team].Add((units, percent));
            Version++;
        }

        // 유닛의 지금 사거리 — 타워 사거리 몫(받는 비율만큼) + 유닛 사거리 변경
        public static float UnitRange(Team team, UnitDefinition unit, float baseValue)
        {
            StatSum sum = StatSum.Identity;
            float ratio = ShareOf(team, unit);
            if (ratio > 0f)
                AccumulateTower(team, TowerStat.Range, ratio, ref sum);
            foreach ((IUnitFilter units, float percent) in UnitRanges[(int)team])
                if (units.AppliesFor(team, unit))
                    sum.Add(StatOp.Percent, percent);
            return sum.Apply(baseValue);
        }

        // 유닛 스탯이 아닌 것(사거리 · 한 번에 쏘는 수) — 함께 받는 유닛이면 타워와 같은 계산(받는 비율만큼)을 그 유닛의 값에, 아니면 그대로
        public static float SharedCompute(Team team, UnitDefinition unit, TowerStat stat, float baseValue)
        {
            float ratio = ShareOf(team, unit);
            if (ratio <= 0f)
                return baseValue;
            StatSum sum = StatSum.Identity;
            AccumulateTower(team, stat, ratio, ref sum);
            return sum.Apply(baseValue);
        }

        // 내 유닛의 처치가 타워의 처치로도 치는 유닛들(사격 통제소 신화: 내 궁병) — 전투 동안. 처치 훅(Unit.Perish)이 본진의 처치 알림(Base.NotifyKill)을 한 번 더 부른다
        private static readonly List<IUnitFilter>[] KillsAsTower = { new List<IUnitFilter>(), new List<IUnitFilter>() };

        public static void CountKillsAsTower(Team team, IUnitFilter units) => KillsAsTower[(int)team].Add(units);

        public static bool KillCountsAsTower(Team team, UnitDefinition unit)
        {
            foreach (IUnitFilter units in KillsAsTower[(int)team])
                if (units.AppliesFor(team, unit))
                    return true;
            return false;
        }

        // 데스매치에서도 타워가 공격하는 진영(오버드라이브) — 전투 동안
        private static readonly bool[] Deathmatch = new bool[2];

        public static bool AttacksInDeathmatch(Team team) => Deathmatch[(int)team];
        public static void AttackInDeathmatch(Team team) => Deathmatch[(int)team] = true;

        // 타워의 화살이 적을 처치할 때마다 그 타워가 즉시 더 쏘는 화살 수(최신 엔진 부품·오버드라이브 신화) — 전투 동안. 쏘는 곳은 Base.NotifyKill
        private static readonly int[] ArrowsOnKill = new int[2];

        public static int ArrowsOnKillOf(Team team) => ArrowsOnKill[(int)team];
        public static void AddArrowsOnKill(Team team, int arrows) => ArrowsOnKill[(int)team] += arrows;

        // 타워가 받은 피해를 공격한 쪽에 그대로 돌려주는 진영(성채 신화) — 전투 동안. 돌려주는 곳은 Base.TakeDamage
        private static readonly bool[] Reflect = new bool[2];

        public static bool ReflectsDamage(Team team) => Reflect[(int)team];
        public static void ReflectDamage(Team team) => Reflect[(int)team] = true;

        // 작은 타워가 앞으로 걸어가는 진영(움직이는 성) — 전투 동안. Speed 0 = 걷지 않는다
        public struct FollowRule
        {
            public float Speed;
            public float Spacing;
        }

        private static readonly FollowRule[] Follows = new FollowRule[2];

        public static FollowRule FollowOf(Team team) => Follows[(int)team];
        public static void SetFollow(Team team, FollowRule rule) => Follows[(int)team] = rule;

        // 작은 타워만의 공격력 합연산 %(합치기 보상 「보조 포탑」 — 0.08 = +8%) — 전투 동안. 진영 %와 한 번에 더해진다(Base.RefreshStats)
        private static readonly float[] SmallAttack = new float[2];

        public static float SmallAttackOf(Team team) => SmallAttack[(int)team];

        public static void AddSmallAttack(Team team, float percent)
        {
            SmallAttack[(int)team] += percent;
            Version++;
        }

        public static void ClearBattle()
        {
            foreach (List<TowerModifier> list in Battle)
                list.Clear();
            SmallAttack[0] = SmallAttack[1] = 0f;
            Deathmatch[0] = Deathmatch[1] = false;
            ArrowsOnKill[0] = ArrowsOnKill[1] = 0;
            Reflect[0] = Reflect[1] = false;
            Shared[0] = Shared[1] = null;
            ScaledShares[0].Clear();
            ScaledShares[1].Clear();
            UnitRanges[0].Clear();
            UnitRanges[1].Clear();
            KillsAsTower[0].Clear();
            KillsAsTower[1].Clear();
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

    // 병종 태그로 거르는 유닛 필터 — 그 병종을 대상으로 적어 두었나. 「~로도 취급」(UnitAliases.AddCategory)이 이것으로 묻는다
    public interface ICategoryFilter
    {
        bool AppliesTo(UnitCategory category);
    }

    // "A는 B에게 적용되는 효과를 함께 받는다"(조립 라인: 톱니거인 → 톱니병사). 전투 시작마다 비우고(BattleManager) 패시브가 건다.
    // 🔴 효과·스탯이 유닛에게 적용되는지는 AppliesTo가 아니라 AppliesFor(진영 포함)로 본다
    public static class UnitAliases
    {
        private static readonly Dictionary<UnitDefinition, UnitDefinition>[] ByTeam =
            { new Dictionary<UnitDefinition, UnitDefinition>(), new Dictionary<UnitDefinition, UnitDefinition>() };

        // 병종 단위의 「~로도 취급」(지역 특수 업그레이드 「군제 개혁」 — 내 경보병은 중기병으로도 취급) — [진영] = (원래 병종, 함께 취급하는 병종)들
        private static readonly List<(UnitCategory from, UnitCategory alsoAs)>[] CategoriesByTeam =
            { new List<(UnitCategory, UnitCategory)>(), new List<(UnitCategory, UnitCategory)>() };

        public static void Clear()
        {
            foreach (Dictionary<UnitDefinition, UnitDefinition> aliases in ByTeam)
                aliases.Clear();
            foreach (List<(UnitCategory, UnitCategory)> aliases in CategoriesByTeam)
                aliases.Clear();
            StatBook.MarkChanged();
        }

        public static void AddCategory(Team team, UnitCategory from, UnitCategory alsoAs)
        {
            CategoriesByTeam[(int)team].Add((from, alsoAs));
            StatBook.MarkChanged();
        }

        // team의 unit이 category 병종인가 — 원래 병종이거나 그 진영에서 그 병종으로도 취급되거나. 병종을 직접 견주는 곳은 모두 이것으로 묻는다
        public static bool Is(Team team, UnitDefinition unit, UnitCategory category)
        {
            if (unit.Category == category)
                return true;
            foreach ((UnitCategory from, UnitCategory alsoAs) in CategoriesByTeam[(int)team])
                if (from == unit.Category && alsoAs == category)
                    return true;
            return false;
        }

        public static bool IsAny(Team team, UnitDefinition unit, UnitCategory[] categories)
        {
            foreach (UnitCategory category in categories)
                if (Is(team, unit, category))
                    return true;
            return false;
        }

        // 병종으로 거르는 것(ICategoryFilter)이 그 진영에서 unit이 함께 취급되는 병종을 받는가
        private static bool AppliesToAlias(IUnitFilter filter, Team team, UnitDefinition unit)
        {
            if (!(filter is ICategoryFilter byCategory))
                return false;
            foreach ((UnitCategory from, UnitCategory alsoAs) in CategoriesByTeam[(int)team])
                if (from == unit.Category && byCategory.AppliesTo(alsoAs))
                    return true;
            return false;
        }

        public static void Add(Team team, UnitDefinition unit, UnitDefinition alsoAs)
        {
            ByTeam[(int)team][unit] = alsoAs;
            StatBook.MarkChanged();
        }

        public static bool AppliesFor(this IUnitFilter filter, Team team, UnitDefinition unit)
            => filter.AppliesTo(unit) || (ByTeam[(int)team].TryGetValue(unit, out UnitDefinition alias) && filter.AppliesTo(alias)) || AppliesToAlias(filter, team, unit);
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
        // alsoAs = 전투 시작 때 걸릴 병종 취급(군제 개혁 — 마찬가지로 Preview가 그 탑의 업그레이드에서 찾아 넘긴다)
        public static void AccumulateRun(UnitDefinition unit, UnitStat stat, ref StatSum sum, UnitDefinition alias = null, List<UnitCategory> alsoAs = null)
            => Accumulate(Run[(int)Team.Ally], Team.Ally, unit, stat, ref sum, alias, alsoAs);

        // 탑 한 채가 영구히 쌓은 것만 — 지금 그 진영에 걸려 있지 않은 탑(지도에서 고른 영주)의 값을 볼 때(RunStats.Preview)
        public static void AccumulateBook(RunBook book, Team team, UnitDefinition unit, UnitStat stat, ref StatSum sum, UnitDefinition alias = null, List<UnitCategory> alsoAs = null)
            => Accumulate(book.Units, team, unit, stat, ref sum, alias, alsoAs);

        private static void Accumulate(List<Entry> entries, Team team, UnitDefinition unit, UnitStat stat, ref StatSum sum, UnitDefinition alias = null, List<UnitCategory> alsoAs = null)
        {
            foreach (Entry e in entries)
                if (e.Modifier.Stat == stat && (e.Source.AppliesFor(team, unit) || (alias != null && e.Source.AppliesTo(alias)) || RunStats.ByCategory(e.Source, alsoAs)))
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
    // 넣는 것: 영구히 붙은 것 + 필드 기물이 런 동안 쌓아 전투 시작 때 거는 것(스패너 — BuildingPassive.BattleStartStat, 사용자 10/10) + 합치기 보상 업그레이드의 스탯(철벽 · 조련장).
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
        // enemy = 지도에서 고른 땅의 영주의 유닛(RunState.PreviewLord) — 그 영주의 탑 · 그 탑이 영구히 쌓은 강화 · 그 탑의 업그레이드로 계산한다.
        // 맵 효과는 넣지 않는다 — 어느 땅에서 싸울지는 침략할 때 정해진다
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
                foreach (BuildingPassive passive in owned.Passives)
                    alias = passive.AliasOf(unit) ?? alias;

            // 군제 개혁의 「~로도 취급」 — 전투가 시작돼야 UnitAliases에 걸리므로 그 탑의 업그레이드에서 미리 읽는다(병종으로 거르는 것이 그 병종도 받게)
            List<UpgradeHolding> holdings = RunState.UpgradesOf(lord);
            var alsoAs = new List<UnitCategory>();
            foreach (UpgradeHolding holding in holdings)
                if (holding.Definition.AliasFor(unit) is UnitCategory category)
                    alsoAs.Add(category);

            StatSum sum = StatSum.Identity;
            if (enemy)
                StatBook.AccumulateBook(RunState.PreviewBook, Team.Enemy, unit, stat, ref sum, alias, alsoAs);
            else
                StatBook.AccumulateRun(unit, stat, ref sum, alias, alsoAs);
            foreach (OwnedBuilding owned in active)
                foreach (BuildingPassive passive in owned.Passives)
                    if (passive is IUnitFilter filter && (filter.AppliesTo(unit) || (alias != null && filter.AppliesTo(alias)) || ByCategory(filter, alsoAs))
                        && passive.BattleStartStat(owned, stat, out StatModifier modifier))
                        sum.Add(modifier.Op, modifier.Value);
            foreach (UpgradeHolding holding in holdings)
                if ((holding.Definition.AppliesTo(unit) || (alias != null && holding.Definition.AppliesTo(alias)) || ByCategory(holding.Definition, alsoAs))
                    && holding.Definition.UnitModifier(holding.Value, out StatModifier modifier) && modifier.Stat == stat)
                    sum.Add(modifier.Op, modifier.Value);
            return new Value { Base = baseValue, Now = sum.Apply(baseValue) };
        }

        // 병종으로 거르는 것이 alsoAs(함께 취급되는 병종들) 중 하나를 받는가
        internal static bool ByCategory(IUnitFilter filter, List<UnitCategory> alsoAs)
            => alsoAs != null && filter is ICategoryFilter byCategory && alsoAs.Exists(category => byCategory.AppliesTo(category));
    }
}

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
    }

    // 스탯 변경을 받을 유닛을 고른다 — 유닛 효과(UnitEffect)와 스탯을 거는 기물 능력이 쓴다
    public interface IUnitFilter
    {
        bool AppliesTo(UnitDefinition unit);
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

    // 진영 단위로 걸린 스탯 변경. 전투 동안 = 전투 시작마다 비움 · 영구히 = 런 동안(아군만, 기물을 팔아도 남는다).
    // 적용 대상은 걸어 준 쪽(효과·기물 능력)의 AppliesTo(병종 태그·유닛 이름). 바뀔 때마다 Version이 올라 유닛이 다시 계산한다
    public static class StatBook
    {
        private struct Entry
        {
            public IUnitFilter Source;
            public StatModifier Modifier;
        }

        private static readonly List<Entry>[] Battle = { new List<Entry>(), new List<Entry>() };
        private static readonly List<Entry> Run = new List<Entry>();

        public static int Version { get; private set; }

        public static void AddForBattle(Team team, IUnitFilter source, StatModifier modifier) => Add(Battle[(int)team], source, modifier);
        public static void AddForRun(IUnitFilter source, StatModifier modifier) => Add(Run, source, modifier);

        public static void ClearBattle()
        {
            foreach (List<Entry> entries in Battle)
                entries.Clear();
            Version++;
        }

        public static void ClearRun()
        {
            Run.Clear();
            Version++;
        }

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
            Accumulate(Battle[(int)team], unit, stat, ref sum);
            if (team == Team.Ally)
                Accumulate(Run, unit, stat, ref sum);
        }

        private static void Accumulate(List<Entry> entries, UnitDefinition unit, UnitStat stat, ref StatSum sum)
        {
            foreach (Entry e in entries)
                if (e.Modifier.Stat == stat && e.Source.AppliesTo(unit))
                    sum.Add(e.Modifier.Op, e.Modifier.Value);
        }
    }
}

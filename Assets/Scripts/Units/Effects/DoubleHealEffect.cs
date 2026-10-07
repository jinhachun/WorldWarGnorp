using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 회복할 때 다음으로 많이 다친 아군에게도 — 동시에 회복하는 수(첫 대상 포함)가 등급마다 는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Double Heal")]
    public class DoubleHealEffect : UnitEffect
    {
        [Tooltip("동시에 회복하는 아군 수(첫 대상 포함)")]
        [SerializeField] private int _targets = 2;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private int _targetsPerTier = 1;

        private readonly List<Unit> _healed = new List<Unit>();

        private int Targets(int level) => OwnedBuilding.Scaled(_targets, _targetsPerTier, level);

        public override void OnHealed(Unit healer, Unit patient, int stacks)
        {
            _healed.Clear();
            _healed.Add(patient);
            for (int i = 1; i < Targets(Level(healer.Team)); i++)
            {
                Unit next = healer.FindHealTarget(_healed);
                if (next == null)
                    return;
                _healed.Add(next);
                healer.ThrowHeal(next);
            }
        }

        public override string Describe(int level) => string.Format(Description, Targets(level));
        public override string Headline(int level) => $"{Targets(level)}기";
    }
}

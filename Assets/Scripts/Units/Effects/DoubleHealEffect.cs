using UnityEngine;

namespace GnorpWar
{
    // 회복할 때 두 번째로 많이 다친 아군에게도
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Double Heal")]
    public class DoubleHealEffect : UnitEffect
    {
        public override void OnHealed(Unit healer, Unit patient, int stacks)
        {
            Unit second = healer.FindHealTarget(patient);
            if (second != null)
                healer.ThrowHeal(second);
        }
    }
}

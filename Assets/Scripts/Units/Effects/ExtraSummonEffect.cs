using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛을 소환할 때마다 N기를 더 소환한다 (대량보급) — 소환 한 번에 N기, 소환 수와 상관없이
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Extra Summon")]
    public class ExtraSummonEffect : UnitEffect
    {
        [SerializeField] private int _extra = 1;

        public override int ExtraSummons(UnitDefinition unit, int count, int sourceSlot, int stacks) => _extra;
    }
}

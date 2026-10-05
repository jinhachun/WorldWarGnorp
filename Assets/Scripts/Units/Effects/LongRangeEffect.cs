using UnityEngine;

namespace GnorpWar
{
    // 사거리·포물선 높이 배율
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Long Range")]
    public class LongRangeEffect : UnitEffect
    {
        [SerializeField] private float _scale = 2f;

        public override float RangeScale(int stacks) => _scale;
    }
}

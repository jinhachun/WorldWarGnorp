using UnityEngine;

namespace GnorpWar
{
    // 이동 속도 증가
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Speed Boost")]
    public class SpeedBoostEffect : UnitEffect
    {
        [Tooltip("이동 속도 증가율 (0.3 = +30%)")]
        [SerializeField] private float _bonus = 0.3f;

        public override float MoveSpeedScale(int stacks) => 1f + _bonus;
    }
}

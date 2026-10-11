using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 작은 타워가 적 성 앞까지 앞으로 걸어간다 (티크톡의 움직이는 성). 움직임은 Base.Follow
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Small Tower Follow")]
    public class SmallTowerFollowPassive : BuildingPassive
    {
        [Tooltip("작은 타워가 움직이는 속도(초당 유닛)")]
        [SerializeField] private float _speed = 2f;
        [Tooltip("작은 타워가 여럿일 때 타워끼리 띄우는 거리")]
        [SerializeField] private float _spacing = 2.5f;
        [Tooltip("등급이 오를 때마다 속도에 더한다(합연산 — 0.5 = +50%). 시작 등급은 0")]
        [SerializeField] private float _speedPerTier;

        private StatModifier Speed(int level) => new StatModifier(UnitStat.MoveSpeed, StatOp.Percent, OwnedBuilding.Scaled(0f, _speedPerTier, level));

        // 등급이 다른 움직이는 성이 둘이면 빠른 쪽
        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            float speed = _speed * (1f + Speed(level).Value);
            if (speed > TowerBook.FollowOf(board.Team).Speed)
                TowerBook.SetFollow(board.Team, new TowerBook.FollowRule { Speed = speed, Spacing = _spacing });
        }

        public override string Describe(int level)
        {
            StatModifier speed = Speed(level);
            return speed.Value > 0f
                ? $"내 작은 타워가 {speed.Describe()}로 앞으로 걸어갑니다."
                : "내 작은 타워가 앞으로 걸어갑니다.";
        }
    }
}

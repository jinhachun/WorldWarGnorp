using UnityEngine;

namespace GnorpWar
{
    // 발동할 때마다 내 본진이 가장 가까운 적 유닛에게 화살 N발을 쏜다 (망루 — 사용자 10/27, 등급마다 +1발). 사거리와 상관없이 전장에서 가장 가까운 적 유닛.
    // 쏘는 길은 본진의 사격(Base.ShootNearestUnit → Base.Shoot) 그대로 — 타워 공격력 · 관통 · 처치 훅이 같이 걸린다.
    // 신화 표시(_mythicMark)가 이 기물의 능력 목록에 있으면 내 작은 타워들도 함께 쏜다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Base Volley")]
    public class BaseVolleyAction : BuildingAction
    {
        [SerializeField] private int _arrows = 3;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private int _arrowsPerTier = 1;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 내 작은 타워들도 함께 쏜다")]
        [SerializeField] private BuildingPassive _mythicMark;

        private int Arrows(int level) => OwnedBuilding.Scaled(_arrows, _arrowsPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Base home = Base.For(board.Team);
            if (home != null)
                home.ShootNearestUnit(Arrows(level));
            OwnedBuilding self = board.Slots[slot].Building;
            if (_mythicMark == null || System.Array.IndexOf(self.Passives, _mythicMark) < 0)
                return;
            foreach (Base small in Base.SmallTowers())
                if (small.Team == board.Team)
                    small.ShootNearestUnit(Arrows(level));
        }

        public override string Describe(int level) => $"내 본진이 가장 가까운 적 유닛에게 화살 {Arrows(level)}발을 쏩니다.";
        public override string Headline(int level) => $"{Arrows(level)}발";
    }
}

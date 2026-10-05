using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(궁병)과 이 진영 타워의 화살이 적을 관통한다 — 땅·벽에 닿을 때까지 경로상의 적을 한 번씩 (관통탄 특허국)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Pierce")]
    public class PierceEffect : UnitEffect
    {
        [SerializeField] private bool _towerArrows = true;

        public override bool ProjectilesPierce => true;
        public override bool TowerArrowsPierce => _towerArrows;
    }
}

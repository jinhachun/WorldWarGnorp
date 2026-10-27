using UnityEngine;

namespace GnorpWar
{
    // 내 타워들의 화살이 적을 처치하면 그 화살을 다시 발사한다 (최신 엔진 부품 신화 — 기획서 §7-1).
    // 「그 화살」이라 처치 하나에 한 발 — 쏘는 것은 처치한 그 타워다(Base.NotifyKill)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Refire Arrow On Kill")]
    public class RefireArrowOnKillPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level) => TowerBook.AddArrowsOnKill(board.Team, 1);

        public override string Describe(int level) => "타워 화살이 처치하면 다시 발사됩니다.";
    }
}

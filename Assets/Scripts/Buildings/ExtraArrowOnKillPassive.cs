using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 타워들이 적을 처치할 때마다 화살을 N발 즉시 더 발사한다 (영구기관 오버드라이브 신화 — 기획서 §7-1).
    // 쏘는 것은 처치한 그 타워다(Base.NotifyKill)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Extra Arrow On Kill")]
    public class ExtraArrowOnKillPassive : BuildingPassive
    {
        [SuffixLabel("발", true)]
        [SerializeField] private int _arrows = 1;

        public override void OnBattleStart(BoardRunner board, int slot, int level) => TowerBook.AddArrowsOnKill(board.Team, _arrows);

        public override string Describe(int level) => $"타워가 처치하면 화살 {_arrows}발을 더 쏩니다.";
    }
}

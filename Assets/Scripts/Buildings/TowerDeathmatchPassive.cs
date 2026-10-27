using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 타워(본진 + 작은 타워)가 데스매치 시각 뒤에도 화살을 쏜다 (영구기관 오버드라이브)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower Deathmatch")]
    public class TowerDeathmatchPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level) => TowerBook.AttackInDeathmatch(board.Team);

        public override string Describe(int level) => "타워가 데스매치에서도 공격합니다.";
    }
}

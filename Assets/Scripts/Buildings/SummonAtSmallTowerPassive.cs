using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 유닛이 성 대신 가장 앞에 있는 내 작은 타워에서 소환된다 (파견 사무소). 자리 고르기는 BoardRunner.Summon
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Summon At Small Tower")]
    public class SummonAtSmallTowerPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level) => board.SummonAtSmallTower();

        public override string Describe(int level) => "내 유닛이 내 성 대신, 가장 앞에 있는 내 작은 타워에서 소환됩니다.";
    }
}

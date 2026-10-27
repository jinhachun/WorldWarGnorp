using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 유닛이 내 모든 작은 타워에서 동시에 소환된다 (파견 사무소 신화 — 기획서 §7-1).
    // §7의 「가장 앞에 있는 작은 타워 하나」를 모든 작은 타워로 넓힌다 — 자리 고르기는 BoardRunner.Summon
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Summon At Every Small Tower")]
    public class SummonAtEverySmallTowerPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level) => board.SummonAtEverySmallTower();

        public override string Describe(int level) => "모든 작은 타워에서 동시에 소환됩니다.";
    }
}

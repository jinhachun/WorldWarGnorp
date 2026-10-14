using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면, 쿨다운이 멈췄던 내 기물은 다시 움직이는 순간 즉시 1회 발동한다 (제물의 의식 신화). 발동시키는 곳은 BoardRunner.Update
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Fire On Thaw")]
    public class FireOnThawPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level) => board.FireOnThawSlot = slot;

        public override string Describe(int level) => "쿨다운이 멈췄던 기물은 다시 움직이는 순간 즉시 1회 발동합니다.";
    }
}

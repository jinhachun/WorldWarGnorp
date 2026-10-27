using UnityEngine;

namespace GnorpWar
{
    // 거울 공방 신화 줄 — 효과는 BoardRunner.Reflect가 이 표시를 보고 낸다(변신한 기물을 전투 시작 때 즉시 1회 발동)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Mirror Mythic")]
    public class MirrorMythicPassive : BuildingPassive
    {
        public override string Describe(int level) => "변신한 기물이 바로 발동합니다.";
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 「판매 시,」 얻는 기물이 신화 등급으로 온다 (판도라의 상자 신화 — 기획서 §7-1).
    // 훅이 없는 표시다 — §7 능력(RandomBuildingOnSellPassive)이 판 기물에서 이것을 찾아 얻는 기물을 신화까지 올려서 준다.
    // 신화 등급의 기물 정의는 만들지 않는다 — 가진 기물(OwnedBuilding)의 오른 횟수로 신화가 된다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Acquire As Mythic")]
    public class AcquireAsMythicPassive : BuildingPassive
    {
        public override string Describe(int level) => "「판매 시,」 얻는 기물이 신화 등급입니다.";
    }
}

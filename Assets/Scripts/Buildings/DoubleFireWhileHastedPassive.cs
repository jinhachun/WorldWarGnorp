using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 가속 중인 내 기물이 발동할 때 효과를 2번 낸다 (물레방앗간 신화 — 기획서 §7-1).
    // 가속을 누가 걸었는지는 가리지 않는다(물레방앗간 · 수도원). 곱하는 곳은 BoardRunner.Fire — 쿨다운 발동도 즉시 발동도 지난다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Double Fire While Hasted")]
    public class DoubleFireWhileHastedPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level) => board.DoubleFireWhileHasted(slot);

        // 가속받을 수 있는 기물(쿨다운이 있는 기물) 전부 — 누가 가속하든
        public override bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated) => BuildingTargets.Cooling(field, other);
        public override string Describe(int level) => "가속 중인 기물은 효과를 2번 냅니다.";
    }
}

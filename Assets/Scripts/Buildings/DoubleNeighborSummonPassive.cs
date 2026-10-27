using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 위에 있는 모든 기물이 유닛을 N배로 소환한다 (군기 신화 — 기획서 §7-1). 칸 번호가 클수록 위층.
    // 늘린 몫은 소환된 한 마리마다 같은 유닛을 그 칸에서 더 소환해 낸다 — 그 소환이 다시 이 능력을 부르면 끝이 없으므로
    // 늘리는 동안은 쉰다(종료 조건: 늘려서 나온 소환은 더 늘리지 않는다)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Double Neighbor Summon")]
    public class DoubleNeighborSummonPassive : BuildingPassive
    {
        [SuffixLabel("배", true)]
        [SerializeField] private int _multiply = 2;

        // 지금 이 능력이 늘려 소환하는 중인가 — 에셋 하나를 양 진영이 같이 쓰지만 소환 한 번은 끝까지 한 번에 돈다
        [System.NonSerialized] private bool _doubling;

        public override void OnSummonedFrom(BoardRunner board, int slot, int sourceSlot, Unit unit)
        {
            if (_doubling || !board.Linked(slot, sourceSlot, sourceSlot > slot) || _multiply <= 1)
                return;
            _doubling = true;
            board.Summon(unit.Definition, _multiply - 1, sourceSlot);
            _doubling = false;
            BuffLinks.ToSlot(board.Team, slot, sourceSlot, BuffKind.Summon);
        }

        public override bool UsesAdjacency => true;
        // 위에 있는 소환 기물 — 상대 자리 고르기(Reaches)는 §7 줄(군기)이 이미 같은 자리를 세므로 대상 표시만
        public override bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated)
            => BuildingTargets.Linked(slot, other, other > slot, federated) && field[other].Definition.Unit != null;
        public override string Describe(int level) => $"위에 있는 모든 기물이 유닛을 {_multiply}배로 소환합니다.";
    }
}

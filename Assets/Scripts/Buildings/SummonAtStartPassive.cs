using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 때 이 기물의 층에서 유닛을 한 번 소환한다 (병영 신화 — 기획서 §7-1).
    // 신화는 등급이 하나뿐이라 수를 등급으로 올리지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Summon At Start")]
    public class SummonAtStartPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _unit;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            board.Summon(_unit, _count, slot);
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
        }

        public override string Describe(int level) => $"전투 시작 시, {_unit.DisplayName} {_count}기를 소환합니다.";
    }
}

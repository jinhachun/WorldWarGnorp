using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 기물의 원래 소환에 더해 unit을 N기 소환한다 — 여러 종류를 한 발동에 내는 생산 기물(왕국 징병소: 방패수(원래 소환) · 병사 · 궁수, 사용자 10/28).
    // 원래 소환 다음에 액션 순서대로 성문에서 나온다. 칸의 소환이라(sourceSlot) 추가 소환 · 변환 · 「소환할 때마다」를 모두 지난다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Summon Also")]
    public class SummonAlsoAction : BuildingAction
    {
        [SerializeField] private UnitDefinition _unit;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;

        public UnitDefinition Unit => _unit;
        public int Count => _count;

        public override void Execute(BoardRunner board, int slot, int level) => board.Summon(_unit, _count, slot);

        public override string Describe(int level) => $"{_unit.DisplayName} {_count}기도 함께 소환합니다.";
    }
}

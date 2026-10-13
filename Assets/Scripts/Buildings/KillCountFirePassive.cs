using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 유닛 · 내 타워들이 유닛 N기를 처치할 때마다, 쿨다운이 가장 긴 내 기물을 즉시 1회 발동시킨다 (약탈족장 라이칸스로프 — 트라이브 명세 §4 · §5).
    // 아군을 죽인 것도 처치로 센다(사용자 결정 10/14). 전투마다 0부터. 즉시 발동은 추가 발동이다(그 기물의 게이지는 그대로).
    // 신화 — 「즉시 발동시킨 기물은 그 발동의 효과를 2번 냅니다」: 이 기물이 신화 표시(_twiceMark)를 갖고 있으면 2번 발동시킨다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Kill Count Fire")]
    public class KillCountFirePassive : BuildingPassive
    {
        [SuffixLabel("기", true)]
        [SerializeField] private int _kills = 10;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 발동을 2번")]
        [SerializeField] private BuildingPassive _twiceMark;

        // 이번 전투에 센 처치 수(진영별)
        [System.NonSerialized] private readonly int[] _count = new int[2];

        public override void OnBattleStart(BoardRunner board, int slot, int level) => _count[(int)board.Team] = 0;

        public override void OnUnitKill(BoardRunner board, int slot, Unit killer, Unit victim) => Count(board, slot);
        public override void OnTowerKill(BoardRunner board, int slot, Unit victim) => Count(board, slot);

        private void Count(BoardRunner board, int slot)
        {
            if (++_count[(int)board.Team] < _kills)
                return;
            _count[(int)board.Team] = 0;
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
            board.FireLongest(System.Array.IndexOf(board.Slots[slot].Building.Passives, _twiceMark) >= 0 ? 2 : 1);
        }

        // 지금까지 센 처치 수(칸 숫자 · 확인용)
        public int CountOf(Team team) => _count[(int)team];

        public override string Describe(int level) => $"유닛 {_kills}기를 처치할 때마다, 쿨다운이 가장 긴 내 기물을 즉시 1회 발동시킵니다.";
    }
}

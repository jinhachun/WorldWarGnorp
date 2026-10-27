using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투 동안 유닛이 N기 죽을 때마다 이 기물의 쿨다운이 영구히 ×M (약탈족장 라이칸스로프 — 사용자 10/15).
    // 아군·적 구분 없이 센다(사용자 답 10/15). 전투마다 0부터. 영구히 = 이 한 채의 쿨다운 배율(OwnedBuilding.ScaleCooldown — 계몽주의와 같은 길)이고, 이번 전투의 게이지에도 바로 곱한다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Death Count Cooldown")]
    public class DeathCountCooldownPassive : BuildingPassive
    {
        [SuffixLabel("기", true)]
        [SerializeField] private int _deaths = 100;
        [Tooltip("쿨다운에 곱한다(영구히)")]
        [SerializeField] private float _factor = 0.96f;

        // 이번 전투에 센 죽음 수(진영별 — 이 기물을 가진 쪽)
        [System.NonSerialized] private readonly int[] _count = new int[2];

        public override void OnBattleStart(BoardRunner board, int slot, int level) => _count[(int)board.Team] = 0;

        public override void OnUnitDied(BoardRunner board, int slot, Unit victim)
        {
            if (++_count[(int)board.Team] < _deaths)
                return;
            _count[(int)board.Team] = 0;
            board.Slots[slot].Building.ScaleCooldown(_factor);
            board.ScaleCooldown(slot, _factor);
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
        }

        // 다음 쿨다운 감소까지 남은 죽음 수(설명 창의 회색 줄 · 탑 칸 숫자)
        public int RemainingOf(Team team) => BattleManager.Fighting ? _deaths - _count[(int)team] : _deaths;   // 전투 밖(상점)에서는 지난 전투의 값이 남아 있으므로 처음 값

        public override string Describe(int level) => $"유닛이 {_deaths}기 죽을 때마다 쿨다운 영구히 ×{_factor:0.##}";
    }
}

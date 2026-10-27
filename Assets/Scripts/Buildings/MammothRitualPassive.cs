using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 맘모스 소환의식(트라이브 고급 — 사용자 10/22): 내 유닛이 영구히 N기 죽기 전까지는 작동하지 않고, 채운 뒤에는 매 전투 데스매치 직전에 유닛을 소환한다.
    // 센 수는 이 기물 한 채의 스택(OwnedBuilding.Stacks — 「영구히」: 팔면 사라지고 합치면 이어진다). 필요한 수는 등급마다 준다.
    // 신화 표시(_mythicMark — 이 기물의 능력 목록에 있으면)가 있으면 그 수만큼 더 소환한다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Mammoth Ritual")]
    public class MammothRitualPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _unit;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("작동하기까지 죽어야 하는 내 유닛 수(시작 등급)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _deaths = 300;
        [Tooltip("등급이 오를 때마다 위 수에 더한다(-50 = 300 · 250 · 200 · 150)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _deathsPerTier = -50;
        [Tooltip("신화 줄의 표시 — 이 기물의 능력 목록에 있으면 그 표시의 수만큼 더 소환한다")]
        [SerializeField] private MammothRitualMythicPassive _mythicMark;

        private int Required(int level) => Mathf.Max(0, OwnedBuilding.Scaled(_deaths, _deathsPerTier, level));

        // 작동까지 남은 죽음 수(탑 칸 숫자 · 설명 창의 회색 줄) — 0이면 작동 중
        public int RemainingOf(OwnedBuilding self) => Mathf.Max(0, Required(self.Level) - self.Stacks);

        // 내 유닛이 죽을 때마다 이 한 채에 센다 — 채운 뒤에는 더 세지 않는다
        public override void OnUnitDied(BoardRunner board, int slot, Unit victim)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            if (victim.Team == board.Team && self.Stacks < Required(self.Level))
                self.AddStacks(1);
        }

        public override void OnBeforeDeathmatch(BoardRunner board, int slot, int level)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            if (RemainingOf(self) > 0)
                return;
            int count = _count + (_mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0 ? _mythicMark.Extra : 0);
            board.Summon(_unit, count, slot);
            // 소환 순간 — 나온 자리에 기둥(Fx/PillarFx) · 칸 튐
            Unit spawned = board.LastSpawned;
            if (spawned != null)
                PillarFx.Rise(new Vector2(spawned.Position.x, spawned.Feet), spawned.Top - spawned.Feet);
            OwnedBuilding.NotifyEffect(self);
        }

        public override string Describe(int level)
            => $"유닛이 영구히 {Required(level)}기 죽어야 작동합니다.\n데스매치 직전, {_unit.DisplayName} {_count}기를 소환합니다.";
    }
}

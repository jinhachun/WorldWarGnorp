using UnityEngine;

namespace GnorpWar
{
    // 내 타워(본진) 스탯을 올리는 늘 켜진 능력 — 언제(전투 시작 · 「구매할 때마다,」 · 타워가 적을 처치할 때마다) × 얼마나(전투 동안 · 영구히).
    // 성채(시작·전투 동안 체력 +50%) · 화살 강화기·최신 엔진 부품(구매·영구히) · 조준연산코그·오버드라이브(처치·전투 동안)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower Stat")]
    public class TowerStatPassive : BuildingPassive
    {
        public enum Trigger { BattleStart, Buy, TowerKill }

        [SerializeField] private Trigger _trigger;
        [Tooltip("켜면 영구히(런 동안, 팔아도 남는다) — 끄면 전투 동안")]
        [SerializeField] private bool _permanent;
        [SerializeField] private TowerModifier _modifier;
        [Tooltip("등급이 오를 때마다 _modifier 값에 더한다")]
        [SerializeField] private float _perTier;
        [Tooltip("×(곱연산) 전투 동안 효과의 상한 — 이 효과로 쌓인 배율이 이 값에서 멈춘다(오버드라이브 3). 0 = 상한 없음")]
        [SerializeField] private float _maxMultiplier;

        // 이 효과로 이번 전투에 쌓인 배율(진영별) — 상한 계산용. 전투 시작마다 1로
        [System.NonSerialized] private readonly float[] _stacked = { 1f, 1f };

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            _stacked[(int)board.Team] = 1f;
            if (_trigger == Trigger.BattleStart)
            {
                Apply(board.Team, level);
                OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
                BuffLinks.ToTowers(board.Team, slot, BuffLinks.KindOf(_modifier.Stat));
            }
        }

        public override bool HasBuyEffect => _trigger == Trigger.Buy;

        public override void OnBuy(OwnedBuilding self, Team team)
        {
            if (_trigger == Trigger.Buy)
            {
                Apply(team, self.Level);
                BuffLinks.ToTowers(team, BuffLinks.SlotOf(team, self), BuffLinks.KindOf(_modifier.Stat));
            }
        }

        public override void OnTowerKill(BoardRunner board, int slot, Unit victim)
        {
            if (_trigger == Trigger.TowerKill && Apply(board.Team, board.Slots[slot].Building.Level, out TowerModifier applied))
            {
                board.AddBattleGain(slot, applied.Op, applied.Value);
                BuffLinks.ToTowers(board.Team, slot, BuffLinks.KindOf(applied.Stat));
            }
        }

        // 처치 발동(전투 동안)은 칸이 값을 쌓는다 — 칸 숫자(BoardRunner.Slot.BattleGainText)
        public override bool BattleGain(out StatOp op)
        {
            op = _modifier.Op;
            return _trigger == Trigger.TowerKill && !_permanent;
        }

        private void Apply(Team team, int level) => Apply(team, level, out _);

        // 상한에 막혀 아무것도 안 걸었으면 false
        private bool Apply(Team team, int level, out TowerModifier modifier)
        {
            modifier = _modifier.Scaled(_perTier, level);
            if (Capped)
            {
                float next = Mathf.Min(_stacked[(int)team] * modifier.Value, _maxMultiplier);
                if (next <= _stacked[(int)team])
                    return false;
                modifier.Value = next / _stacked[(int)team];   // 상한에 정확히 닿게 마지막 한 번은 남은 만큼만
                _stacked[(int)team] = next;
            }
            if (_permanent)
                TowerBook.AddForRun(team, modifier);
            else
                TowerBook.AddForBattle(team, modifier);
            return true;
        }

        private bool Capped => _maxMultiplier > 0f && _modifier.Op == StatOp.Multiply && !_permanent;

        public override string Headline(int level) => _modifier.Scaled(_perTier, level).ValueText;

        public override string Describe(int level)
        {
            string when = _trigger == Trigger.Buy ? "「구매할 때마다,」 " : _trigger == Trigger.TowerKill ? "내 타워들이 적을 처치할 때마다, " : "";
            // 전투 시작에 거는 건 늘 켜진 것처럼 읽히므로 "전투 동안"을 붙이지 않는다(성채 "내 타워의 체력 +50%")
            string battle = !_permanent && _trigger != Trigger.BattleStart ? "전투 동안 " : "";
            string permanent = _permanent ? " 영구히" : "";
            string cap = Capped ? $" (최대 ×{_maxMultiplier:0.##})" : "";
            return $"{when}{battle}내 타워들의 {_modifier.Name}{permanent} {_modifier.Scaled(_perTier, level).ValueText}{cap}";
        }
    }
}

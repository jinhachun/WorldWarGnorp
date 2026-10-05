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

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            if (_trigger == Trigger.BattleStart)
                Apply(board.Team);
        }

        public override bool HasBuyEffect => _trigger == Trigger.Buy;

        public override void OnBuy(OwnedBuilding self, Team team)
        {
            if (_trigger == Trigger.Buy)
                Apply(team);
        }

        public override void OnTowerKill(BoardRunner board, int slot, Unit victim)
        {
            if (_trigger == Trigger.TowerKill)
                Apply(board.Team);
        }

        // 영구히는 아군만(런) — 적 보드는 그 전투 동안으로
        private void Apply(Team team)
        {
            if (_permanent && team == Team.Ally)
                TowerBook.AddForRun(_modifier);
            else
                TowerBook.AddForBattle(team, _modifier);
        }

        public override string Describe(int level)
        {
            string when = _trigger == Trigger.Buy ? "「구매할 때마다,」 " : _trigger == Trigger.TowerKill ? "내 타워가 적을 처치할 때마다, " : "";
            // 전투 시작에 거는 건 늘 켜진 것처럼 읽히므로 "전투 동안"을 붙이지 않는다(성채 "내 타워의 체력 +50%")
            string battle = !_permanent && _trigger != Trigger.BattleStart ? "전투 동안 " : "";
            string permanent = _permanent ? " 영구히" : "";
            return $"{when}{battle}내 타워의 {_modifier.Name}{permanent} {_modifier.ValueText}";
        }
    }
}

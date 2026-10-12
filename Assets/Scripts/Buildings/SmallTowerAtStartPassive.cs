using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 때 전장 가운데에 작은 타워 1채를 바로 세운다(건축가를 거치지 않는다) (조립식 감시탑).
    // 가운데 = 양 본진의 중간 x에서 내 쪽으로 _offset — 양 진영이 다 가지면 가운데 선을 사이에 두고 마주 선다.
    // 그 자리에 내 작은 타워가 이미 있으면 _spacing씩 내 쪽으로 물러난다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Small Tower At Start")]
    public class SmallTowerAtStartPassive : BuildingPassive
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Base _towerPrefab;
        [Tooltip("양 본진의 중간에서 내 쪽으로 물러난 거리")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _offset = 1.5f;
        [Tooltip("내 작은 타워끼리 띄우는 거리")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _spacing = 2.5f;
        [Tooltip("등급이 오를 때마다 이 작은 타워의 사거리에 더한다(합연산 — 0.05 = +5%). 시작 등급은 0")]
        [SerializeField] private float _rangePerTier;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        private TowerModifier Range(int level) => new TowerModifier(TowerStat.Range, StatOp.Percent, OwnedBuilding.Scaled(0f, _rangePerTier, level));

        public override void OnBattleStart(BoardRunner board, int slot, int level) => Build(board, slot, level);

        // 세운다 — 전투 시작 때, 그리고 신화면 이 타워가 파괴될 때마다 같은 규칙으로 다시(SmallTowerRebuildPassive)
        public void Build(BoardRunner board, int slot, int level)
        {
            Team team = board.Team;
            Base home = Base.For(team), enemy = Base.For(team == Team.Ally ? Team.Enemy : Team.Ally);
            if (home == null || enemy == null)
                return;
            float forward = team == Team.Ally ? 1f : -1f;
            float x = (home.transform.position.x + enemy.transform.position.x) * 0.5f - forward * _offset;
            while (IsTaken(team, x))
                x -= forward * _spacing;
            Base tower = Base.BuildOnGround(_towerPrefab, team, x);
            tower.SetRangeBonus(Range(level).Value);
            tower.BuiltBy = board.Slots[slot].Building;
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
        }

        private bool IsTaken(Team team, float x)
        {
            foreach (Base tower in Base.SmallTowers())
                if (tower.Team == team && Mathf.Abs(tower.transform.position.x - x) < _spacing)
                    return true;
            return false;
        }

        public override string Describe(int level)
        {
            TowerModifier range = Range(level);
            return range.Value > 0f ? $"전투 시작 시, 전장 가운데에 {range.Name} {range.ValueText}인 작은 타워를 세웁니다." : "전투 시작 시, 전장 가운데에 작은 타워를 세웁니다.";
        }
    }
}

using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다 본진에서 유닛을 소환한다 (왕족친위대: 기사 → 친위기사 — 사용자 10/28, 친위기사는 기사가 아니라 발동하지 않는다).
    // 「연계는 한 번만」(사용자 10/26) — 연계로 태어난 유닛(Unit.ChainBorn)의 처치는 다시 부르지 않는다. 낸 유닛은 연계로 태어남
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Kill Summon")]
    public class KillSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _countPerTier = 1;

        private int Count(int level) => OwnedBuilding.Scaled(_count, _countPerTier, level);

        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            if (!killer.ChainBorn)
                SummonFor(killer);
        }

        protected void SummonFor(Unit killer)
        {
            BoardRunner board = BoardRunner.For(killer.Team);
            board.Summon(_summon, Count(Level(killer.Team)), -1, source: Source(killer.Team), chained: true);
            // 처치한 유닛에서 성에서 나온 유닛으로
            if (board.LastSpawned != null)
                BuffLinks.EffectFromUnit(killer.Team, this, killer.Position, board.LastSpawned.Position, BuffKind.Summon);
        }

        public override string Describe(int level) => string.Format(Description, Count(level));
        public override string Headline(int level) => $"{Count(level)}기";
    }
}

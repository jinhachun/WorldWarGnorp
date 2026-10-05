using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다 본진에서 유닛을 소환한다 (왕족친위대: 경기병 → 친위기사, 친위기사도 경기병이라 연쇄)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Kill Summon")]
    public class KillSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SerializeField] private int _count = 1;

        public override void OnKill(Unit killer, Unit victim, int stacks) => BoardRunner.For(killer.Team).Summon(_summon, _count, -1);
    }
}

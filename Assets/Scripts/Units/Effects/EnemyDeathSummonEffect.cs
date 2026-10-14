using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적 유닛이 죽으면 그 자리에 내 유닛을 소환한다 (까마귀의사 신화: 적 전부 → 병사 · 꼭두각시 왕 신화: 적 지원병 → 친위기사 — 기획서 §7-1).
    // 누가 죽였는지는 보지 않는다. 신화는 등급이 하나뿐이라 수를 등급으로 올리지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Enemy Death Summon")]
    public class EnemyDeathSummonEffect : UnitEffect
    {
        [Tooltip("이 병종의 적이 죽을 때만 — 비우면 적 전부")]
        [SerializeField] private UnitCategory[] _victims = new UnitCategory[0];
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;

        public override void OnEnemyDied(Team team, Unit victim, Vector2 at, int stacks)
        {
            if (_victims.Length != 0 && !UnitAliases.IsAny(victim.Team, victim.Definition, _victims))
                return;
            Vector2 ground = new Vector2(at.x, Ground.Instance.HeightAt(at.x));
            BoardRunner.For(team).Summon(_summon, _count, -1, ground, Source(team));
            BuffLinks.EffectToPoint(team, this, ground, BuffKind.Summon);
        }

        public override string Describe(int level) => string.Format(Description, _count);
    }
}

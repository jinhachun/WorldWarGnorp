using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 이 효과를 가진 기물의 무적 발동이 체력이 가장 낮은 아군 N기에게 간다 (공주의 축복 신화: 3기 — 기획서 §7-1, 사용자 10/13. §7의 1기를 포함한 합계다).
    // 진영에 켜지는 효과로 돌지 않는다 — 무적을 주는 능력(InvulnerableLowestAction)이 자기 기물에서 이 수를 읽는다. 신화는 등급이 하나뿐이라 수를 등급으로 올리지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable Targets")]
    public class InvulnerableTargetsEffect : UnitEffect
    {
        [SuffixLabel("기", true)]
        [SerializeField] private int _targets = 3;

        public int Targets => _targets;

        public override string Describe(int level) => string.Format(Description, _targets);
    }
}

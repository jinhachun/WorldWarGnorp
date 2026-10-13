using UnityEngine;

namespace GnorpWar
{
    // 공격력이 (rank + 1)번째로 높은 내 유닛이 피해를 줄 때마다, 준 피해의 N%만큼 체력을 회복한다 (원시 의식 제단 — 트라이브 명세 §4 · §5).
    // 누가 가장 높은지는 타격마다 그때의 공격력으로 본다. 범위 공격은 맞힌 대상마다 준 피해가 다 들어간다(아군에게 준 것도). 신화는 rank 1짜리 에셋을 더 켠다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Lifesteal Strongest")]
    public class LifestealStrongestEffect : UnitEffect
    {
        [Tooltip("0 = 공격력이 가장 높은 유닛 · 1 = 두 번째")]
        [SerializeField] private int _rank;
        [Tooltip("준 피해 대비 — 0.5 = 50%")]
        [SerializeField] private float _ratio = 0.5f;

        public override void OnDealtDamage(Unit dealer, Unit victim, float amount)
        {
            if (Strongest.Of(dealer.Team, _rank) == dealer)
                dealer.Heal(amount * _ratio);
        }

        public override string Describe(int level) => string.Format(Description, $"{_ratio * 100f:0.#}");
    }
}

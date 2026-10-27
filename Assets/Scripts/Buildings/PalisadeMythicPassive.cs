using UnityEngine;

namespace GnorpWar
{
    // 목책 신화 줄 — 효과는 PalisadeAtStartPassive가 이 표시를 보고 낸다(목책이 받은 피해의 N%를 공격한 쪽에)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Palisade Mythic")]
    public class PalisadeMythicPassive : BuildingPassive
    {
        [Tooltip("받은 피해 중 돌려주는 비율(0.5 = 50%)")]
        [SerializeField] private float _reflect = 0.5f;

        public float Reflect => _reflect;

        public override string Describe(int level) => $"목책이 받은 피해의 {_reflect * 100f:0}%를 돌려줍니다.";
    }
}

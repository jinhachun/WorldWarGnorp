using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 처음 죽을 때 대신 N초간 무적 상태가 된 뒤 쓰러진다 (충정의 맹세)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/First Death Delay")]
    public class FirstDeathDelayEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 3f;

        public override float FirstDeathDelay => _seconds;
        public override string Headline(int level) => $"{_seconds:0.#}초";
    }
}

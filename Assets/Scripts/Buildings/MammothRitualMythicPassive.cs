using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 맘모스 소환의식 신화 줄 — 효과는 MammothRitualPassive가 이 표시를 보고 낸다(데스매치 직전 소환 수 + N)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Mammoth Ritual Mythic")]
    public class MammothRitualMythicPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _unit;
        [SuffixLabel("기", true)]
        [SerializeField] private int _extra = 1;

        public int Extra => _extra;

        public override string Describe(int level) => $"{_unit.DisplayName} {_extra}기를 더 소환합니다.";
    }
}

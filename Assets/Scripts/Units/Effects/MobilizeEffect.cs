using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 모든 기물이 유닛을 소환할 때 대신 다른 유닛을 소환한다 — 큰 유닛도(총동원 개조령: 톱니로켓병, 사용자 10/28).
    // 기물의 소환만(sourceSlot ≥ 0 — 교황청의 「내 기물이」와 같은 선) — 유닛이 낳는 소환(톱니거인의 톱니병사 · 죽은 자리 소환)은 그대로.
    // 다른 변환(노동의 기계화 · 교황청)이 끝난 뒤에 바꾼다(UnitEffect.ReplaceSummonLast). 수는 그대로 — 1기는 1기
    // 신화(표시 _mythicMark가 켜져 있으면): 바꾸기 전 유닛이 그 병종(중보병)이면 1기마다 _mythicCount기
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Mobilize")]
    public class MobilizeEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _into;
        [Tooltip("신화 표시 — 이 진영에 켜져 있으면 아래 병종을 바꿀 때 수를 늘린다")]
        [SerializeField] private UnitEffect _mythicMark;
        [SerializeField] private UnitCategory _mythicCategory;
        [Tooltip("신화 — 그 병종 1기 대신 나오는 수")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _mythicCount = 5;

        public override UnitDefinition ReplaceSummonLast(Team team, UnitDefinition unit, int sourceSlot, ref int count)
        {
            if (_into == null || sourceSlot < 0 || unit == _into)
                return unit;
            if (_mythicMark != null && _mythicCategory != null && UnitAliases.Is(team, unit, _mythicCategory))
                foreach (TeamEffects.Entry e in TeamEffects.For(team))
                    if (e.Effect == _mythicMark)
                    {
                        count *= _mythicCount;
                        break;
                    }
            return _into;
        }
    }
}

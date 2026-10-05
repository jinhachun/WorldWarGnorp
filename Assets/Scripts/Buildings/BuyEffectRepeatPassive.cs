using UnityEngine;

namespace GnorpWar
{
    // 내 「구매할 때마다,」 효과가 N번 발동한다 (대지주 스팀바론) — 여러 개여도 가장 큰 값 한 번(BuildingPassive.BuyRepeats)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Buy Effect Repeat")]
    public class BuyEffectRepeatPassive : BuildingPassive
    {
        [SerializeField] private int _repeat = 2;

        public override int BuyEffectRepeat => _repeat;

        public override string Describe(int level) => $"내 「구매할 때마다,」 효과가 {_repeat}번 발동합니다.";
    }
}

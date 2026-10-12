using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 「구매할 때마다,」 효과가 있는 기물을 사면 같은 기물을 N개 더 얻는다 (주식시장 신화 — 기획서 §7-1).
    // 얻는 것은 사는 게 아니다 — 「구매할 때마다,」도 합치기도 없이 빈 칸에 들어간다(RunState.Acquire)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Copy On Buy Effect")]
    public class CopyOnBuyEffectPassive : BuildingPassive
    {
        [SuffixLabel("개", true)]
        [SerializeField] private int _count = 1;

        public override void OnAnyBought(OwnedBuilding self, OwnedBuilding bought)
        {
            if (!HasBuyEffects(bought.Definition))
                return;
            for (int i = 0; i < _count; i++)
                RunState.Acquire(bought.Definition);
            OwnedBuilding.NotifyEffect(self);
        }

        public override string Describe(int level) => $"「구매할 때마다,」 효과가 있는 기물을 구매하면, 같은 기물 {_count}개를 더 얻습니다.";
    }
}

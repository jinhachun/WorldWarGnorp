using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 턴마다 골드 N, 「판매 시,」 지금까지 준 골드만큼 더 (전쟁 금고 — 사용자 10/27, 영웅 2 · 전설 3).
    // 준 골드는 이 한 채의 스택에 쌓는다(합쳐지면 더해지고 팔면 사라진다). 턴 훅(OnTurnStart)은 내 턴 · AI 영주의 턴 상점 모두에서 불린다(RunState).
    // 신화 표시(_mythicMark)가 있으면 판매 때 그만큼을 한 번 더 받는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/War Chest")]
    public class WarChestPassive : BuildingPassive
    {
        [SuffixLabel("골드", true)]
        [SerializeField] private int _gold = 2;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _goldPerTier = 1;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 판매 때 준 골드를 한 번 더 받는다")]
        [SerializeField] private BuildingPassive _mythicMark;

        private int Gold(int level) => OwnedBuilding.Scaled(_gold, _goldPerTier, level);

        public override void OnTurnStart(OwnedBuilding self)
        {
            int gold = Gold(self.Level);
            self.AddStacks(gold);
            RunState.GainGold(gold);
            OwnedBuilding.NotifyEffect(self);
        }

        public override void OnSell(OwnedBuilding self, int floor)
        {
            int times = _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0 ? 2 : 1;
            if (self.Stacks > 0)
                RunState.GainGold(self.Stacks * times);
        }

        public override string Describe(int level)
            => $"턴마다 골드 +{Gold(level)}\n「판매 시,」 지금까지 준 골드만큼 더 받습니다.";
        public override string Headline(int level) => $"{Gold(level)}골드";
    }
}

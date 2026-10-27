using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투 중 N초마다 이 기물의 판매가가 영구히 +G골드 (동전 주조기 — 사용자 10/28, 등급마다 +1골드: 일반 1 · 고급 2 · …).
    // 오른 몫은 이 한 채의 스택에 쌓는다(합쳐지면 더해지고 팔면 사라진다 — 전쟁 금고와 같다) — 판매가(RunState.SellValue)가 SellBonus로 더한다.
    // 기물이 멈춘 뒤(BoardsStopped)에는 오르지 않는다. 신화 표시(_mythicMark)가 있으면 오를 때마다 그 진영이 골드 1도 바로 얻는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Coin Mint")]
    public class CoinMintPassive : BuildingPassive
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 30f;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _gold = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _goldPerTier = 1;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 오를 때마다 골드를 바로 얻는다")]
        [SerializeField] private BuildingPassive _mythicMark;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _mythicGold = 1;

        private int Gold(int level) => OwnedBuilding.Scaled(_gold, _goldPerTier, level);

        public override int SellBonus(OwnedBuilding self) => self.Stacks;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            board.StartCoroutine(Mint(board, self));
        }

        private IEnumerator Mint(BoardRunner board, OwnedBuilding self)
        {
            // 전투 시작 훅(Begin)은 전투 중으로 바뀌기 전에 불린다 — 바뀔 때까지 기다린다. 전투가 끝나면 씬과 함께 사라진다
            while (!BattleManager.Fighting)
                yield return null;
            float at = _seconds;
            bool mythic = _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0;
            while (BattleManager.Fighting && !BattleManager.BoardsStopped)
            {
                if (BattleManager.Elapsed >= at)
                {
                    self.AddStacks(Gold(self.Level));
                    OwnedBuilding.NotifyEffect(self);
                    if (mythic)
                        RunState.GainBattleGold(board.Team, _mythicGold);
                    at += _seconds;
                }
                yield return null;
            }
        }

        public override string Describe(int level) => $"전투 중 {_seconds:0}초마다 이 기물의 판매가 영구히 +{Gold(level)}골드";
        public override string Headline(int level) => $"{Gold(level)}골드";
    }
}

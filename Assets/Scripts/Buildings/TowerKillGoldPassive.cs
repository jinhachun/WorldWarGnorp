using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 타워들이 적을 처치할 때마다 확률로 골드를 얻는다 (주식시장 — 사용자 10/20). 전투 중에 바로 그 진영의 런 골드로 들어간다(「골드를 얻을 때마다」 기물도 반응).
    // 신화 표시(_mythicMark)가 있으면 얻는 골드가 _mythicGold
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower Kill Gold")]
    public class TowerKillGoldPassive : BuildingPassive
    {
        [SuffixLabel("%", true)]
        [SerializeField] private float _chancePercent = 10f;
        [Tooltip("등급이 오를 때마다 확률에 더한다(영웅 10% · 전설 15% — 사용자 10/20)")]
        [SuffixLabel("%", true)]
        [SerializeField] private float _chancePerTier = 5f;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _gold = 1;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 얻는 골드가 아래 값")]
        [SerializeField] private BuildingPassive _mythicMark;
        [SuffixLabel("골드", true)]
        [SerializeField] private int _mythicGold = 3;

        public override void OnTowerKill(BoardRunner board, int slot, Unit victim)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            if (Random.value * 100f >= Chance(self.Level))
                return;
            int gold = _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0 ? _mythicGold : _gold;
            RunState.GainBattleGold(board.Team, gold);
            OwnedBuilding.NotifyEffect(self);
            GoldDrops.Raise(board.Team, victim.Position, gold);
        }

        private float Chance(int level) => OwnedBuilding.Scaled(_chancePercent, _chancePerTier, level);

        public override string Describe(int level) => $"타워가 처치하면 {Chance(level):0.##}% 확률로 골드 {_gold}을 얻습니다.";
    }
}

using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 내 작은 타워가 파괴될 때마다 내 성 앞면에 작은 타워를 세운다 (티크톡의 움직이는 성 신화 — 기획서 §7-1).
    // 걸어 나오는 것은 §7 능력(SmallTowerFollowPassive)이 한다 — 신화는 §7에 더해지므로 늘 같이 켜져 있다.
    // 누가 세운 타워든(건축가 · 감시탑 · 이 능력) 센다. 신화는 등급이 하나뿐이라 수를 등급으로 올리지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Small Tower From Castle")]
    public class SmallTowerFromCastlePassive : BuildingPassive
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Base _towerPrefab;
        [SuffixLabel("개", true)]
        [SerializeField] private int _count = 1;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public override void OnSmallTowerDestroyed(BoardRunner board, int slot, Base tower)
        {
            Base home = Base.For(board.Team);
            if (home == null || !home.IsAlive)
                return;
            float x = board.Team == Team.Ally ? home.Bounds.max.x : home.Bounds.min.x;
            for (int i = 0; i < _count; i++)
                Base.BuildOnGround(_towerPrefab, board.Team, x);
            OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
        }

        public override string Describe(int level) => $"내 작은 타워가 파괴되면, 내 성에서 작은 타워 {_count}개가 걸어 나옵니다.";
    }
}

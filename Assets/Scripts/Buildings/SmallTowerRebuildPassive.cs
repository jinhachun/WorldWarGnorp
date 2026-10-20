using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 세운 작은 타워가 파괴되면 같은 규칙으로 다시 세운다 (조립식 감시탑 신화 — 기획서 §7-1).
    // 「이 작은 타워」 = Base.BuiltBy가 이 칸의 기물인 것 — 건축가가 지은 타워 · 다른 감시탑의 타워는 대상이 아니다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Small Tower Rebuild")]
    public class SmallTowerRebuildPassive : BuildingPassive
    {
        [Tooltip("다시 세울 때 쓰는 §7 능력 — 자리 · 프리팹 · 사거리를 거기서 읽는다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private SmallTowerAtStartPassive _build;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public override void OnSmallTowerDestroyed(BoardRunner board, int slot, Base tower)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            if (tower.BuiltBy != self)
                return;
            Base rebuilt = _build.Build(board, slot, self.Level);
            // 다시 세워지는 자리에 기둥
            if (rebuilt != null)
                PillarFx.Rise(new Vector2(rebuilt.transform.position.x, rebuilt.Bounds.min.y), rebuilt.Bounds.size.y);
        }

        public override string Describe(int level) => "이 작은 타워가 파괴되면, 다시 세워집니다.";
    }
}

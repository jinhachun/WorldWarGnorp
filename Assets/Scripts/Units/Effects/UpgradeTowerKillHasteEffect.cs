using UnityEngine;

namespace GnorpWar
{
    // 내 타워들(본진 · 작은 타워)이 적을 처치할 때마다, 쿨다운이 있는 무작위 내 기물 1개를 N초간 가속 (국가 전용 업그레이드 「증기 압력」 — 티크톡) — N은 업그레이드의 합쳐진 값.
    // 연출: 본진 → 가속한 층(가속 색)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Tower Kill Haste")]
    public class UpgradeTowerKillHasteEffect : UnitEffect
    {
        public override void OnTowerKill(Team team, Base tower, Unit victim)
        {
            BoardRunner board = BoardRunner.For(team);
            if (board == null || BattleManager.BoardsStopped)
                return;
            int slot = board.HasteRandom(Value(team));
            if (slot >= 0)
                BuffLinks.FromBaseToSlot(team, slot, BuffKind.Haste);
        }
    }
}

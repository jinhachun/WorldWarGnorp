using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 남은 체력(수치)이 가장 낮은 아군 1기에게 N초간 무적 (공주의 축복 — 사용자 결정: 비율이 아니라 수치)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Invulnerable Lowest")]
    public class InvulnerableLowestAction : BuildingAction
    {
        [SerializeField] private float _seconds = 1f;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Unit lowest = null;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team && (lowest == null || unit.Hp < lowest.Hp))
                    lowest = unit;
            if (lowest != null)
                lowest.GrantInvulnerable(_seconds);
        }

        public override string Describe(int level) => $"체력이 가장 낮은 아군 1기에게 {_seconds:0.#}초간 무적을 부여합니다.";
    }
}

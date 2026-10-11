using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 남은 체력(수치)이 가장 낮은 아군 1기에게 N초간 무적 (공주의 축복 — 사용자 결정: 비율이 아니라 수치)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Invulnerable Lowest")]
    public class InvulnerableLowestAction : BuildingAction
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 1f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _secondsPerTier = 1f;

        private float Seconds(int level) => OwnedBuilding.Scaled(_seconds, _secondsPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Unit lowest = null;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team && (lowest == null || unit.Hp < lowest.Hp))
                    lowest = unit;
            if (lowest != null)
            {
                lowest.GrantInvulnerable(Seconds(level));
                BuffLinks.ToPoint(board.Team, slot, lowest.Position, BuffKind.Invulnerable);
            }
        }

        public override string Describe(int level) => $"체력이 가장 낮은 아군 1기에게 {Seconds(level):0.#}초간 무적을 부여합니다.";
        public override string Headline(int level) => $"{Seconds(level):0.#}초";
    }
}

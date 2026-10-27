using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 그 순간 전장의 내 유닛 전부에게 N초간 무적 (수호의 종 — 사용자 10/28, 등급마다 +0.5초: 2 · 2.5 · 3 · 3.5 · 4).
    // 무적은 Unit.GrantInvulnerable 한 곳만 지난다 — 무적 시간 · 무적 끝 효과가 이 무적에도 걸리게
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Invulnerable All")]
    public class InvulnerableAllAction : BuildingAction
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 2f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _secondsPerTier = 0.5f;

        public float SecondsAt(int level) => OwnedBuilding.Scaled(_seconds, _secondsPerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            float seconds = SecondsAt(level);
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team)
                    unit.GrantInvulnerable(seconds);
            BuffLinks.ToUnits(board.Team, slot, BuffKind.Invulnerable);
        }

        public override string Describe(int level) => $"모든 아군이 {SecondsAt(level):0.#}초간 무적이 됩니다.";
        public override string Headline(int level) => $"{SecondsAt(level):0.#}초";
    }
}

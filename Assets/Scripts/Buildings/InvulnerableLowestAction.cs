using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 남은 체력(수치)이 가장 낮은 아군 1기에게 N초간 무적 (공주의 축복 — 사용자 결정: 비율이 아니라 수치). 신화면 3기(InvulnerableTargetsEffect)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Invulnerable Lowest")]
    public class InvulnerableLowestAction : BuildingAction
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 1f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _secondsPerTier = 1f;

        private float Seconds(int level) => OwnedBuilding.Scaled(_seconds, _secondsPerTier, level);

        private static readonly System.Collections.Generic.List<Unit> Lowest = new System.Collections.Generic.List<Unit>();

        public override void Execute(BoardRunner board, int slot, int level)
        {
            // 몇 기에게 — 기본 1기, 이 기물(이 칸의 한 채)에 켜진 효과가 늘린다(신화 3기). 신화 한 채 + 전설 한 채면 각자 자기 수만큼
            int targets = 1;
            foreach (UnitEffect effect in board.Slots[slot].Building.Effects)
                if (effect is InvulnerableTargetsEffect more)
                    targets = Mathf.Max(targets, more.Targets);
            // 남은 체력이 낮은 순 — 이미 무적인 아군도 가리지 않고 고른다(§7의 1기 규칙 그대로). 아군이 모자라면 있는 만큼
            Lowest.Clear();
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team)
                    Lowest.Add(unit);
            Lowest.Sort((a, b) => a.Hp.CompareTo(b.Hp));
            for (int i = 0; i < targets && i < Lowest.Count; i++)
            {
                Lowest[i].GrantInvulnerable(Seconds(level));
                BuffLinks.ToPoint(board.Team, slot, Lowest[i].Position, BuffKind.Invulnerable);
            }
        }

        public override string Describe(int level) => $"체력이 가장 낮은 아군 1기에게 {Seconds(level):0.#}초간 무적을 부여합니다.";
        public override string Headline(int level) => $"{Seconds(level):0.#}초";
    }
}

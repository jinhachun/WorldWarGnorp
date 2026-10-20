using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때마다, 쿨다운이 있는 무작위 내 기물 1개(이 기물도 될 수 있다)의 쿨다운을 N초간 멈추고, 이 기물의 바로 위 · 아래 기물들을 즉시 1회 발동시킨다
    // (제물의 의식 — 트라이브 명세 §4 · §5 · 결정 기록: 「인접」 = 바로 위 · 아래). 순서는 멈추기 → 발동. 멈춘 기물은 바로 위 · 아래여도 발동하지 않는다(사용자 10/15)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Sacrifice")]
    public class SacrificeAction : BuildingAction
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _freezeSeconds = 10f;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            int frozen = board.RandomCooling();
            board.Freeze(frozen, _freezeSeconds);
            // 멈추는 것도 「발동」 색(사용자 10/14 기준) — 자기 자신을 멈추면 선이 없다
            if (frozen >= 0 && frozen != slot)
                BuffLinks.ToSlot(board.Team, slot, frozen, BuffKind.Trigger);
            for (int other = 0; other < board.Slots.Length; other++)
                if (board.Linked(slot, other, Mathf.Abs(other - slot) == 1) && board.FireNow(other))
                    BuffLinks.ToSlot(board.Team, slot, other, BuffKind.Trigger);
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => Mathf.Abs(other - slot) == 1 && target.Cooldown > 0f;

        public override bool UsesAdjacency => true;
        public override string Describe(int level) => $"무작위 내 기물 1개의 쿨다운을 {_freezeSeconds:0.#}초간 멈추고, 바로 위·아래 기물들을 즉시 1회 발동시킵니다.";
    }
}

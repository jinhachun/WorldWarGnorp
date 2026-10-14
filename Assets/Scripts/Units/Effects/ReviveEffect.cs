using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽으면 N초 뒤 그 자리에서 다시 일어난다 (노동의 기계화 신화: 톱니병사 5초 — 기획서 §7-1). 죽을 때마다다.
    // 일어나는 것은 새 유닛이다(가득 찬 체력 · 그 유닛에만 걸려 있던 스탯은 없다) — 일으키는 곳은 BoardRunner.ReviveLater
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Revive")]
    public class ReviveEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 5f;

        public override void OnDied(Unit unit, Vector2 at, int stacks) => BoardRunner.For(unit.Team).ReviveLater(unit.Definition, at, _seconds, this);

        public override string Describe(int level) => string.Format(Description, $"{_seconds:0.#}");
    }
}

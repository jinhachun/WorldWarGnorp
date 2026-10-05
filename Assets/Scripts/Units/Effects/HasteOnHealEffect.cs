using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(사제)이 회복할 때마다 무작위 기물 하나를 가속한다 (수도원 — 쿨다운 있는 기물만)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Haste On Heal")]
    public class HasteOnHealEffect : UnitEffect
    {
        [SerializeField] private float _seconds = 0.5f;

        public override void OnHealed(Unit healer, Unit patient, int stacks) => BoardRunner.For(healer.Team).HasteRandom(_seconds);
    }
}

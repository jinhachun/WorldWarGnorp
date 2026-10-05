using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(사제)이 회복과 별개로, 회복 간격마다 사거리 안 가장 가까운 적 주변을 회복량만큼 친다 (교황청).
    // 치료할 아군이 없어도 친다 — 사용자 결정: "회복과 별개로"
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Smite")]
    public class SmiteEffect : UnitEffect
    {
        [SerializeField] private float _radius = 2f;
        // 사거리 안에 적이 없을 때 다시 보는 간격
        private const float Recheck = 0.2f;

        public override void Tick(Unit unit, int stacks)
        {
            if (!unit.EffectReady(this, unit.AttackInterval))
                return;
            bool hit = unit.AreaAttackNearest(_radius, unit.Definition.HealAmount);
            unit.SetEffectCooldown(this, hit ? unit.AttackInterval : Recheck);
        }
    }
}

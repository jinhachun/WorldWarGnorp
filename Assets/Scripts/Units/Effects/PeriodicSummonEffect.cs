using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 N초마다 유닛을 소환한다 — 자기 바로 뒤에 (철인공장 콜로서스: 톱니거인이 3초마다 톱니병사)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Periodic Summon")]
    public class PeriodicSummonEffect : UnitEffect
    {
        [SerializeField] private float _interval = 3f;
        [SerializeField] private UnitDefinition _summon;
        [SerializeField] private int _count = 1;
        [Tooltip("소환 자리 — 내 뒤로 이만큼")]
        [SerializeField] private float _behind = 1.5f;

        public override void Tick(Unit unit, int stacks)
        {
            if (!unit.EffectReady(this, _interval))
                return;
            unit.SetEffectCooldown(this, _interval);
            float x = unit.Position.x - unit.Forward * _behind;
            BoardRunner.For(unit.Team).Summon(_summon, _count, -1, new Vector2(x, Ground.Instance.HeightAt(x)));
        }
    }
}

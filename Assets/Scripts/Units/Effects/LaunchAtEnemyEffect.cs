using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 N초마다 가까운 적 유닛에게 투사체를 발사해 자기 공격력만큼 피해를 준다 (철인공장 콜로서스 신화: 톱니거인이 톱니병사를 발사 — 기획서 §7-1).
    // 유닛마다 따로 도는 타이머다(Unit.EffectReady) — 기물의 쿨다운과 상관없다. 사거리 안에 적 유닛이 없으면 쏘지 않고 기다린다. 날아간 유닛은 적을 맞히든 땅에 떨어지든 그 자리에 내 유닛으로 남는다(Projectile.Carry)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Launch At Enemy")]
    public class LaunchAtEnemyEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _interval = 5f;
        [Tooltip("이 거리 안의 가장 가까운 적 유닛에게 쏜다")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _range = 10f;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Projectile _projectile;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _arcHeight = 2.5f;
        [Tooltip("탄으로 날아가는 유닛 — 닿은 자리에 내 유닛으로 남는다(사용자 10/13). 비우면 피해만 주는 탄")]
        [SerializeField] private UnitDefinition _carried;

        public override void Tick(Unit unit, int stacks)
        {
            if (!unit.EffectReady(this, _interval))
                return;
            Unit target = null;
            float best = float.MaxValue;
            int enemyLayer = LayerMask.GetMask(unit.Team == Team.Ally ? "EnemyUnit" : "AllyUnit");
            foreach (Collider2D col in Physics2D.OverlapCircleAll(unit.Position, _range, enemyLayer))
            {
                if (!col.TryGetComponent(out Unit enemy) || !enemy.IsAlive)
                    continue;
                float distance = (enemy.Position - unit.Position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = enemy;
                }
            }
            if (target == null)
                return;
            unit.SetEffectCooldown(this, _interval);
            Projectile shot = Pooled.Get(_projectile, unit.Position, Quaternion.identity);
            shot.Launch(unit.Team, unit.AttackDamage * unit.DamageScale, 0f, target.Position, _arcHeight, 0f, unit);
            if (_carried != null)
                shot.Carry(_carried, Source(unit.Team));
        }

        public override string Describe(int level) => string.Format(Description, $"{_interval:0.#}");
    }
}

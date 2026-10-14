using UnityEngine;

namespace GnorpWar
{
    // 단두대 혁명의 도끼 — 본진에서 대상 유닛까지 돌며 포물선으로 날아가, 닿는 순간 그 유닛에게 즉사 피해(체력만큼)를 준다(사용자 10/14 "진짜 커다란 도끼가 날라갔으면").
    // 대상이 움직이면 끝점이 따라간다. 날아가는 동안 대상이 죽거나 다른 유닛으로 다시 쓰이면 마지막 자리에 떨어지고 아무도 맞히지 않는다(다른 대상을 고르지 않는다).
    // 즉사 판정은 기존 피해 길 그대로(Unit.TakeDamage — 무적이면 막히고 · 신정 · 충정의 맹세가 먼저). 피해를 준 쪽은 던진 본진이다(처치 효과 · 처치 수)
    public class AxeThrow : MonoBehaviour
    {
        private const int SortingOrder = 160;   // 유닛 · 버프 선(150) 위, 데미지 숫자(200) 아래
        private const float HitShake = 0.3f;

        private Vector2 _from;
        private Vector2 _last;
        private Unit _victim;
        private int _victimLife;
        private Base _thrower;
        private float _flight;
        private float _arc;
        private float _spin;
        private float _age;

        public static void Throw(Base thrower, Unit victim)
        {
            BattleConfig config = BattleManager.Config;
            if (config == null || config.AxeSprite == null)
            {
                // 그림이 없으면 연출 없이 바로(옛 동작)
                victim.TakeDamage(victim.MaxHp, Direction(thrower.Muzzle, victim.Position), 0f, thrower);
                return;
            }
            var go = new GameObject("GuillotineAxe", typeof(SpriteRenderer));
            var sprite = go.GetComponent<SpriteRenderer>();
            sprite.sprite = config.AxeSprite;
            sprite.sortingOrder = SortingOrder;
            AxeThrow axe = go.AddComponent<AxeThrow>();
            axe._from = thrower.Muzzle;
            axe._last = victim.Position;
            axe._victim = victim;
            axe._victimLife = victim.Life;
            axe._thrower = thrower;
            axe._flight = Mathf.Max(0.05f, config.AxeFlightSeconds);
            axe._arc = config.AxeArcHeight;
            // 날아가는 쪽으로 앞으로 구르게 — 오른쪽으로 가면 시계 방향
            axe._spin = config.AxeSpin * (victim.Position.x >= axe._from.x ? -1f : 1f);
            go.transform.position = axe._from;
        }

        private bool VictimHere => _victim != null && _victim.IsAlive && _victim.Life == _victimLife;

        private void Update()
        {
            if (VictimHere)
                _last = _victim.Position;
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _flight);
            // 꼭짓점 높이를 출발 · 끝 중 높은 쪽 + arc에 두는 포물선 — 직선 보간 위에 4t(1-t)만큼 얹는다
            float lift = Mathf.Max(_from.y, _last.y) + _arc - (_from.y + _last.y) * 0.5f;
            Vector2 at = Vector2.Lerp(_from, _last, t) + Vector2.up * (4f * t * (1f - t) * lift);
            transform.position = at;
            transform.rotation = Quaternion.Euler(0f, 0f, _spin * _age);
            if (t < 1f)
                return;

            Vector2 direction = Direction(_from, _last);
            if (VictimHere)
                _victim.TakeDamage(_victim.MaxHp, direction, 0f, _thrower);
            if (FxDirector.Instance != null)
            {
                FxDirector.Instance.HitSpark(_last, direction, Color.white);
                FxDirector.Instance.Shake(HitShake);
            }
            Destroy(gameObject);
        }

        private static Vector2 Direction(Vector2 from, Vector2 to)
            => new Vector2(to.x >= from.x ? 1f : -1f, 0f);
    }
}

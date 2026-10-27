using UnityEngine;

namespace GnorpWar
{
    // 투사체(화살·돌) — 중력으로 포물선을 그리며 날아가 처음 닿은 적에게 피해. 아군은 통과.
    // 착탄 범위가 있으면(돌) 닿은 자리 주변 적 전부에게 피해를 주고 바깥·위로 날린다
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        private const float Lifetime = 4f;
        // 범위 착탄 시 날아가는 방향에 섞는 위쪽 성분 — 옆으로만 밀면 산에 막혀 날아가 보이지 않는다
        private const float SplashLiftBias = 1f;
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };

        [Tooltip("로켓 — 날아가는 동안 불꽃 꼬리, 범위 착탄이면 터지는 자리에 불씨가 사방으로 튀고 범위만큼 고리(톱니로켓병)")]
        [SerializeField] private bool _explodes;

        private Rigidbody2D _rb;
        private Team _team;
        private float _damage;
        private float _push;
        private float _splashRadius;
        // 0보다 크면 회복 투사체 — 적·던진 본인·체력이 가득 찬 아군은 통과하고, 처음 닿은 다친 아군을 회복
        private float _heal;
        private Unit _owner;
        // 던진 유닛의 종류 — 구슬이 나는 동안 던진 유닛이 죽어 풀에서 다른 유닛이 돼도 착지 효과(OnHealLanded)의 적용 분류는 던질 때 기준
        private UnitDefinition _ownerDefinition;
        // 쏜 쪽(처치자 판정) — 유닛은 풀에서 다시 쓰이므로 쏠 때의 생애 번호가 맞을 때만 그 유닛으로 친다
        private IDamageable _shooter;
        private int _shooterLife;
        // 쏜 유닛의 종류(타워의 화살이면 null) — 화살이 맞았을 때의 효과(OnArrowHit)의 적용 분류는 쏠 때 기준
        private UnitDefinition _shooterDefinition;
        private bool _spent;
        // 관통 — 적을 맞혀도 멈추지 않고 땅·벽에 닿을 때까지. 한 적은 한 번만
        private bool _pierce;
        // 관통한 화살이 적을 맞힐 때마다 피해에 더하는 몫(관통탄 특허국 신화 1 = +100%, 합연산 — 1 · 2 · 3 · 4배) — 0이면 그대로. 기준은 쏠 때의 피해
        private float _pierceHitBonus;
        private float _pierceBaseDamage;
        // 높은 고도에서 쏜 화살인가 — 맞은 적 유닛을 켜진 효과에 알린다(기름투하대 신화)
        private bool _highGround;
        private readonly System.Collections.Generic.HashSet<IDamageable> _pierced = new System.Collections.Generic.HashSet<IDamageable>();
        private float _lifeTimer;
        private readonly System.Collections.Generic.List<Collider2D> _splashHits = new System.Collections.Generic.List<Collider2D>();

        // 실려 날아가는 유닛(콜로서스 신화 — 톱니거인이 발사한 톱니병사) — 날아가는 동안 그 유닛 그림으로 보이고, 닿은 자리에 이 진영의 유닛으로 선다. 없으면 null
        private UnitDefinition _carried;
        private BuildingDefinition _carrySource;
        private SpriteRenderer _renderer;
        private Sprite _baseSprite;

        // 이번 비행 번호 — 풀에서 다시 꺼낼 때마다 오른다(연출이 같은 비행인지 가린다)
        public int Flight { get; private set; }
        // 그림 칸 — 물리 몸과 따로 움직일 수 있는 자식(꼬리 Fx/ArrowTrails가 읽는다)
        public Transform Body => _renderer.transform;

        // 처치해서 더 쏜 화살 · 관통 화살의 꼬리 색(Fx/ArrowTrails) · 관통 신화 섬광
        private static readonly Color ExtraTrail = new Color(1f, 0.9f, 0.25f);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _renderer = GetComponentInChildren<SpriteRenderer>();
            // 그림이 몸에 바로 붙어 있으면 자식으로 옮긴다 — 그림만 옆으로 벌릴 수 있게(물리 몸 · 판정은 그대로)
            if (_renderer.transform == transform)
                _renderer = MoveToBody(_renderer);
            _baseSprite = _renderer.sprite;
        }

        private SpriteRenderer MoveToBody(SpriteRenderer source)
        {
            var body = new GameObject("Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            body.transform.SetParent(transform, false);
            body.sprite = source.sprite;
            body.color = source.color;
            body.flipX = source.flipX;
            body.flipY = source.flipY;
            body.sharedMaterial = source.sharedMaterial;
            body.sortingLayerID = source.sortingLayerID;
            body.sortingOrder = source.sortingOrder;
            body.maskInteraction = source.maskInteraction;
            source.enabled = false;
            return body;
        }

        // Launch 뒤에 — 처치해서 더 쏜 화살이다(노란 꼬리)
        public void MarkExtra() => ArrowTrails.Follow(this, ExtraTrail);

        // Launch 뒤에 — 이 탄은 unit 한 기다. 적을 맞히든 땅에 떨어지든 그 자리에 선다(소환 — source = 낳은 기물)
        public void Carry(UnitDefinition unit, BuildingDefinition source)
        {
            _carried = unit;
            _carrySource = source;
            _renderer.sprite = unit.PrefabFor(_team).GetComponentInChildren<SpriteRenderer>().sprite;
        }

        // 실린 유닛을 지금 자리의 땅에 세운다 — 게임 규칙상의 소환이라 BoardRunner.Summon을 지난다(빈 칸 찾기 · 소환 효과)
        private void Land()
        {
            if (_carried == null)
                return;
            UnitDefinition unit = _carried;
            _carried = null;
            float x = _rb.position.x;
            BoardRunner.For(_team).Summon(unit, 1, -1, new Vector2(x, Ground.Instance.HeightAt(x)), _carrySource);
        }

        // 풀에서 다시 꺼낼 때마다 — 지난번 비행의 흔적(회복 종류)을 지운다
        private void OnEnable()
        {
            Flight++;
            _renderer.transform.localPosition = Vector3.zero;
            _spent = false;
            _heal = 0f;
            _owner = null;
            _shooter = null;
            _pierce = false;
            _highGround = false;
            _highGroundBoost = false;
            _slowSeconds = 0f;
            _carried = null;
            _renderer.sprite = _baseSprite;
            _pierced.Clear();
            _splashRadius = 0f;
            _lifeTimer = Lifetime;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        // 꼭짓점 높이를 고정(발사 지점·목표 중 높은 쪽 + arcHeight)하고 거기서 속도를 역산한다.
        // 수평 속도를 고정하면 가깝거나 같은 높이의 적에게는 거의 직선이 되므로 쓰지 않는다
        public void Launch(Team team, float damage, float push, Vector2 targetPoint, float arcHeight, float splashRadius, IDamageable shooter)
        {
            _team = team;
            _damage = damage;
            _push = push;
            _splashRadius = splashRadius;
            _shooter = shooter;
            _shooterLife = shooter is Unit unit ? unit.Life : 0;
            _shooterDefinition = shooter is Unit archer ? archer.Definition : null;

            Vector2 origin = _rb.position;
            float gravity = -Physics2D.gravity.y * _rb.gravityScale;
            float apex = Mathf.Max(origin.y, targetPoint.y) + arcHeight;
            float riseSpeed = Mathf.Sqrt(2f * gravity * (apex - origin.y));
            _rb.linearVelocity = new Vector2((targetPoint.x - origin.x) / FlightTime(targetPoint, arcHeight), riseSpeed);
            _lifeTimer = Lifetime;
            if (_explodes)
                ArrowTrails.FollowFire(this);
        }

        // Launch 뒤에 — 이번 비행은 적을 관통한다(관통탄 특허국)
        public void Pierce(float hitBonus = 0f)
        {
            _pierce = true;
            _pierceHitBonus = hitBonus;
            _pierceBaseDamage = _damage;
            ArrowTrails.FollowFire(this);   // 관통 = 불꽃 꼬리(사용자 10/27 — 10/20 Q5의 흰 꼬리를 바꿈)
        }

        // Launch 뒤에 — 높은 고도에서 쏜 화살이다(쏘는 순간 기준 · 타워는 적 유닛에게 늘)
        // boosted = 고지 보너스로 피해가 올라 있다(기름투하대) — 맞는 순간 주황 섬광
        public void HighGround(bool boosted = false)
        {
            _highGround = true;
            _highGroundBoost = boosted;
        }

        private bool _highGroundBoost;

        // Launch 뒤에 — 맞은 적 유닛이 seconds 동안 이동속도 ×factor(뼈 투척 신화)
        public void SlowOnHit(float factor, float seconds)
        {
            _slowFactor = factor;
            _slowSeconds = seconds;
        }

        private float _slowFactor = 1f;
        private float _slowSeconds;

        // 적에게 피해를 준다 — 높은 고도에서 쏜 화살이 적 유닛을 맞혔으면 켜진 효과에 알린다
        private void Deal(IDamageable target, Vector2 direction)
        {
            // 타워(본진 · 작은 타워)의 화살이 건물(본진 · 작은 타워)을 맞히면 피해 ×배율(BattleConfig — 사용자 10/22). 유닛에게는 그대로
            float damage = _shooter is Base && target is Base ? _damage * BattleManager.TowerVsBuildingDamage : _damage;
            target.TakeDamage(damage, direction, _push, Shooter);
            if (_slowSeconds > 0f && target is Unit slowed && slowed.IsAlive)
                slowed.Slow(_slowFactor, _slowSeconds);
            if (_shooterDefinition != null && target is Unit hit && hit.IsAlive)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _shooterDefinition))
                        e.Effect.OnArrowHit(_team, hit);
            if (_highGroundBoost && target is Unit && FxDirector.Instance != null)
                FxDirector.Instance.HitSpark(_rb.position, direction, FxDirector.FireColor);
            if (_highGround && target is Unit victim)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    e.Effect.OnHighGroundHit(victim, _damage, Shooter);
        }

        // 쏜 유닛이 그사이 죽어 풀에서 다른 유닛으로 다시 쓰였으면 처치자 없음
        private IDamageable Shooter => _shooter is Unit unit && unit.Life != _shooterLife ? null : _shooter;

        private float FlightTime(Vector2 targetPoint, float arcHeight)
        {
            float gravity = -Physics2D.gravity.y * _rb.gravityScale;
            float apex = Mathf.Max(_rb.position.y, targetPoint.y) + arcHeight;
            return Mathf.Sqrt(2f * (apex - _rb.position.y) / gravity) + Mathf.Sqrt(2f * (apex - targetPoint.y) / gravity);
        }

        public void LaunchHeal(Unit owner, float amount, Vector2 targetPoint, float arcHeight)
        {
            Team team = owner.Team;
            _owner = owner;
            _ownerDefinition = owner.Definition;
            _heal = amount;
            Launch(team, 0f, 0f, targetPoint, arcHeight, 0f, null);
        }

        private void FixedUpdate()
        {
            _lifeTimer -= Time.fixedDeltaTime;
            if (_lifeTimer <= 0f)
            {
                Land();
                gameObject.SetActive(false);
                return;
            }

            // 그림은 오른쪽을 향해 그려져 있다 — 날아가는 방향으로 머리를 돌린다
            Vector2 v = _rb.linearVelocity;
            if (v.sqrMagnitude > 0.0001f)
                _rb.MoveRotation(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent || other.isTrigger || other.usedByEffector)   // 탑의 층 바닥(이펙터)은 지나간다
                return;

            if (_heal > 0f)
            {
                if (other.TryGetComponent(out IDamageable anyone) && anyone.Team != _team)
                    return;   // 적은 통과
                if (other.TryGetComponent(out Unit ally))
                {
                    if (ally == _owner || !ally.IsDamaged)
                        return;   // 던진 본인·멀쩡한 아군은 통과
                    float healed = ally.Heal(_heal);
                    if (healed > 0f)
                        foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                            if (e.Effect.AppliesFor(_team, _ownerDefinition))
                                e.Effect.OnHealLanded(_team, ally, healed, _heal - healed, e.Stacks);
                }
                _spent = true;
                gameObject.SetActive(false);
                return;
            }

            if (other.TryGetComponent(out IDamageable damageable))
            {
                if (damageable.Team == _team || !damageable.IsAlive)
                    return;
                if (_pierce)
                {
                    if (_pierced.Add(damageable))
                    {
                        Deal(damageable, _rb.linearVelocity.normalized);
                        _damage = _pierceBaseDamage * (1f + _pierced.Count * _pierceHitBonus);
                        // 뚫는 순간 흰 섬광 — 관통 신화(타격마다 피해 +)면 노랑 + 꼬리가 한 점 는다
                        bool mythic = _pierceHitBonus > 0f;
                        if (FxDirector.Instance != null)
                            FxDirector.Instance.HitSpark(_rb.position, _rb.linearVelocity.normalized, mythic ? ExtraTrail : Color.white);
                        if (mythic)
                            ArrowTrails.Grow(this);
                    }
                    return;
                }
                if (_splashRadius <= 0f)
                    Deal(damageable, _rb.linearVelocity.normalized);
            }

            if (_splashRadius > 0f)
                Splash(_rb.position);

            // 적이든 바닥·벽이든 닿으면 끝 (같은 스텝에 여러 번 불려도 한 번만)
            _spent = true;
            Land();
            gameObject.SetActive(false);
        }

        // 로켓 폭발 연출 — 불씨(Fx/FireTrails)가 범위 끝까지 사방으로 튀고 범위만큼 주황 고리(Fx/RingFx). 판정은 아래 Splash 그대로
        private const int ExplosionEmbers = 14;
        private const float ExplosionEmberLife = 0.45f;

        private void Explode(Vector2 center)
        {
            for (int i = 0; i < ExplosionEmbers; i++)
            {
                float angle = (i + Random.value * 0.5f) / ExplosionEmbers * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) * 0.8f + 0.2f);
                FireTrails.Emit(center, dir * (_splashRadius / ExplosionEmberLife * Random.Range(0.6f, 1f)), Random.Range(2, 4), ExplosionEmberLife, false);
            }
            RingFx.Pulse(center, _splashRadius, FxDirector.FireColor, null);
        }

        private void Splash(Vector2 center)
        {
            if (_explodes)
                Explode(center);
            Physics2D.OverlapCircle(center, _splashRadius, SolidOnly, _splashHits);
            foreach (Collider2D col in _splashHits)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive)
                    continue;

                Vector2 away = (Vector2)col.bounds.center - center;
                Vector2 direction = (away.normalized + Vector2.up * SplashLiftBias).normalized;
                damageable.TakeDamage(_damage, direction, _push, Shooter);
            }
        }
    }
}

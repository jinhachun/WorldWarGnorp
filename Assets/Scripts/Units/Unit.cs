using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Unit : MonoBehaviour, IDamageable
    {
        // 죽은 유닛은 그림만 숨긴 채 이만큼 뒤에 풀로 돌아간다 — 보이는 건 날아가는 시체(CorpseFx)
        private const float DeathDestroyDelay = 3f;
        // 피격 연출 — 밀려나는 동안은 이동·공격을 쉰다(안 그러면 다음 스텝 속도 덮어쓰기에 지워진다)
        private const float KnockbackDuration = 0.15f;
        private const float SquashDuration = 0.3f;
        private const float SquashAmount = 0.3f;
        private const float FlashDuration = 0.08f;
        // 큰 넉백 — 뒤로 튕겨 오르며 날아가는 동안은 경직
        private const float BigKnockbackDuration = 0.6f;
        private const float BigKnockbackHopRatio = 0.6f;
        // 타격감 연출 세기 (FxDirector)
        private static readonly Color SparkColor = new Color(1f, 0.9f, 0.25f);
        private const float SparkSurfaceOffset = 0.4f;
        private const float DeathShake = 0.08f;
        private const float BigKnockbackShake = 0.35f;
        private const float BigKnockbackHitStop = 0.06f;
        // 칼 찌르기 — 몸 중심 근처에서 타겟 방향으로 뻗었다가 돌아온다
        private const float ThrustDuration = 0.2f;
        private const float ThrustDistance = 0.3f;
        private const float WeaponReach = 0.45f;
        // 칸 검사는 몸(고체)만 본다 — 날아가는 화살(트리거) 때문에 칸이 막힌 걸로 보이면 안 된다
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };
        private static readonly List<Collider2D> CellProbe = new List<Collider2D>();
        // 등반 — 앞 기둥은 이만큼까지만 위로 훑는다. "앞 아군이 내 속도의 몇 배보다 느리면 넘나"는 BattleConfig
        private const int ClimbMaxColumn = 4;
        // 기둥 꼭대기가 점프 높이에 딱 걸리면 꼭짓점에서 못 넘어가 떨어진다 — 이만큼 여유가 있어야 노린다
        private const float ClimbReachMargin = 0.2f;
        // 넘쳐흐르기 — 근접은 머리가 내 발 높이(+이만큼) 아래인 적을 치지 않고 밟고 넘어간다
        private const float StandOnMargin = 0.1f;

        [SerializeField] private UnitDefinition _definition;
        [SerializeField] private Team _team;
        [Tooltip("그림(몸통·무기)만 담은 자식. 찌그러짐은 여기에만 건다 — 충돌 박스가 찌그러지면 산이 흔들린다")]
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _weapon;

        private Rigidbody2D _rb;
        private BoxCollider2D _body;
        // 검색 필터 — 적 쪽(내 진영 유닛 레이어 제외: 적 유닛·기지·탑)과 아군 쪽(내 진영 유닛 레이어만)
        private ContactFilter2D _enemyFilter;
        private ContactFilter2D _allyFilter;
        private int[] _bodySortingOrders;
        private int _weaponSortingOrder;
        // 죽은 뒤 풀로 돌아가기까지 남은 시간
        private float _despawnTimer;
        // 적 찾기 캐시(FindTargetCached) — 다음 새 검색까지 남은 시간과 그때 찾은 적
        private float _targetScanTimer;
        private IDamageable _cachedTarget;
        private Collider2D _cachedTargetCollider;
        // 찾은 적을 이 시각(Time.fixedTime)까지는 다시 고르지 않는다(BattleManager.TargetHold) — 앞뒤 적이 번갈아 가까워질 때 대상이 매 검색마다 바뀌지 않게
        private float _targetHoldUntil;
        private Collider2D _foundCollider;
        // 프리스트의 회복 대상 캐시 — 같은 간격으로만 새로 찾는다
        private float _supportScanTimer;
        private Unit _cachedPatient;
        // 기지 위 유닛 검사 캐시(걷기) — 같은 간격으로만 새로 본다
        private float _walkScanTimer;
        private bool _aheadOnBaseUnit;
        // 뒤돌아 싸움 검사 캐시(걷기, 같은 간격) — 등 뒤 가장 가까운 적 유닛의 x(없으면 NaN)
        private float _turnBackScanTimer;
        private float _turnBackX = float.NaN;
        // 뒤돌기를 그만둔 뒤 이 시각까지는 다시 돌아서지 않는다(BattleManager.TargetHold)
        private float _turnBackRetryAt;
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        // 콜라이더 → 유닛 — 접촉 루프가 접촉마다 TryGetComponent(네이티브 호출)를 부르지 않게. 풀 재사용이라 Awake에 한 번 넣고 파괴될 때 뺀다
        private static readonly Dictionary<Collider2D, Unit> ByCollider = new Dictionary<Collider2D, Unit>();
        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private float _lastX;
        private float _advanceSpeed;
        private float _hp;
        private float _attackCooldown;
        private bool _onBase;
        // 몸통 그림들(무기 제외) — 보통 1장, 기사는 말 + 탄 gnorp 2장
        private SpriteRenderer[] _bodyRenderers;
        private Color[] _bodyColors;
        private SpriteRenderer _weaponRenderer;
        // 키의 절반 — 충돌 박스 높이에서 읽는다(보통 0.5, 기사 0.75). 칸 검사·발 고정이 이 값을 쓴다
        private float _halfHeight;
        private Vector3 _weaponRestPosition;
        // 그림 크기 — 프리팹의 Visual 스케일(큰 유닛은 2). 찌그러짐이 매 프레임 덮으므로 거기에 곱한다
        private Vector3 _visualScale = Vector3.one;
        // 크기(순례지·크루세이더) — 1 = 프리팹 그대로. 충돌 박스·그림·키가 같이 커진다(발은 그 자리)
        private Vector2 _baseBodySize;
        private Vector3 _baseVisualScale = Vector3.one;
        private float _knockbackTimer;
        private float _squashTime = SquashDuration;
        private bool _squashVertical = true;
        private float _flashTimer;
        private float _thrustTime = ThrustDuration;
        private Vector2 _thrustDirection;
        private bool _bigKnockbackUsed;
        // 돌격 충전 — 적 없이 실제로 달린 시간. 막혀 서 있으면 0으로
        private float _runTime;
        // 밟기 피해 — 적 위에 서 있은 시간(간격마다 피해를 주고 덜어낸다)
        private const float StompTickInterval = 0.5f;
        private float _stompTimer;
        // 등반 점프로 뛰어올라 아직 내려앉지 않았다(행군·싸움 모두) — 그동안 가로 속도는 착지 자리에 맞춘다(ClimbVx)
        private bool _climbing;
        // 다음 등반 점프(행군·싸움 모두)까지 남은 시간(BattleConfig)
        private float _climbRestTimer;
        // 점프 착지 충격(코끼리) — 다음 점프까지 남은 시간 · 뛰어오른 뒤 착지 전인가 · 그 사이 발이 땅에서 떨어진 적이 있나
        private float _slamTimer;
        private bool _slamJumping;
        private bool _slamLeftGround;
        // 이 유닛에게만 걸린 스탯 변경(꼭두각시 왕·군기 등) · 계산해 둔 스탯(StatBook.Version이나 내 변경이 바뀔 때만 다시)
        private readonly List<StatModifier> _ownModifiers = new List<StatModifier>();
        private readonly float[] _stats = new float[4];
        private int _statsVersion = -1;
        private bool _ownModifiersChanged;
        // 건축가(AttackType.Build) — 자리 찾기 → 걸어가기 → 짓기 → 성으로 돌아가기. 짓는 자리를 잡은 건축가들(같은 자리를 두 명이 고르지 않게)
        private enum BuildPhase { Find, Going, Building, Returning }
        private const float BuildArriveDistance = 0.1f;
        private const float BuildSearchStep = 0.25f;
        private const float HammerInterval = 0.5f;
        // 걷는데 이 시간 동안 못 나아가면(속도가 이동 속도의 이 비율 아래) 이 높이로 뛴다
        private const float BuildStallSeconds = 0.3f;
        private const float BuildStallSpeedRatio = 0.1f;
        private const float BuildStallHopHeight = 0.25f;
        private static readonly List<Unit> Builders = new List<Unit>();
        private BuildPhase _buildPhase;
        private float _buildX;
        private float _buildTimer;
        private float _hammerTimer;
        private float _buildStallTimer;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public bool IsDamaged => IsAlive && _hp < MaxHp;
        public UnitDefinition Definition => _definition;
        public float MaxHp => Stat(UnitStat.MaxHp);
        public float AttackDamage => Stat(UnitStat.Attack);
        // 생애 번호 — 풀에서 다시 꺼낼 때마다 오른다. 날아가는 투사체가 쏜 유닛이 아직 그 생애인지 가린다
        public int Life { get; private set; }
        // 이 유닛을 낳은 기물(BoardRunner.Summon이 적는다) — 「이 기물이 소환한 ~」을 가리는 효과가 읽는다. 기물이 낳지 않았으면 null
        public BuildingDefinition SummonedBy { get; private set; }
        // 이 유닛을 낸 칸(BoardRunner.Summon의 sourceSlot) — 칸 없는 소환이면 -1. 준 피해를 그 칸 몫으로 센다(Battle/DamageMeter)
        public int SourceSlot { get; private set; } = -1;

        // 효과별 이 유닛의 다음 사용 시각(톱니거인의 주기 소환 등) — 생애마다 비운다
        private readonly Dictionary<UnitEffect, float> _effectReadyAt = new Dictionary<UnitEffect, float>();
        // 무적 남은 시간 · 사망 유예(충정의 맹세) 남은 시간과 그때의 처치자 · 생애에 한 번만 쓰는 효과
        private const float DoomedHp = 0.001f;
        private float _invulnerableTimer;
        private float _doomTimer;
        private IDamageable _doomKiller;
        private readonly HashSet<UnitEffect> _usedOnce = new HashSet<UnitEffect>();

        // 처음 묻는 순간부터 firstDelay 뒤에 준비된다
        public bool EffectReady(UnitEffect effect, float firstDelay)
        {
            if (!_effectReadyAt.TryGetValue(effect, out float at))
            {
                _effectReadyAt[effect] = Time.fixedTime + firstDelay;
                return false;
            }
            return Time.fixedTime >= at;
        }

        public void SetEffectCooldown(UnitEffect effect, float seconds) => _effectReadyAt[effect] = Time.fixedTime + seconds;

        // 무적(공주의 축복·루니카·충정의 맹세) — 이미 무적이면 더 긴 쪽. 무적 중 공격력 보너스가 있어 스탯을 다시 계산한다
        public bool IsInvulnerable => _invulnerableTimer > 0f;
        public float Hp => _hp;

        // 무적을 주는 모든 길이 여기를 지난다 — 지속시간 보정(충정의 맹세 신화)을 한 자리에서 건다. 무적은 받은 유닛 한 기에게만 걸린다.
        // 돌려주는 값 = 보정이 붙은 실제 무적 시간(충정의 맹세의 사망 유예가 무적과 같은 길이로 가게)
        public float GrantInvulnerable(float seconds)
        {
            if (!IsAlive)
                return 0f;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    seconds += e.Effect.InvulnerableBonusSeconds;
            if (!IsInvulnerable)
                _ownModifiersChanged = true;
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, seconds);
            if (_invulnerableRing == null)
                _invulnerableRing = MakeHalo("InvulnerableRing", RingSprite(), InvulnerableRingColor, _weaponSortingOrder + 1);
            return seconds;
        }

        // 무적 표시 — 몸을 감싸는 노란 원(사용자 10/7). 처음 무적이 될 때 만들고 무적 동안만 켠다(Update)
        private static readonly Color InvulnerableRingColor = new Color(1f, 0.85f, 0.1f);
        // 공격력 버프 — 받는 순간 몸 뒤로 빨간 원판이 잠깐 비쳤다가 흐려진다(사용자 10/7). 처음 버프를 받을 때 만들고 재사용
        private static readonly Color AttackAuraColor = new Color(1f, 0.15f, 0.1f, 0.6f);
        private const float AttackAuraDuration = 0.4f;
        private const float HaloDiameterScale = 1.5f;   // 몸(충돌 박스) 긴 변의 몇 배
        private static Sprite _ringSprite;
        private static Sprite _discSprite;
        private SpriteRenderer _invulnerableRing;
        private SpriteRenderer _attackAura;
        private float _attackAuraTimer;

        private void FlashAttackAura()
        {
            if (_attackAura == null)
                _attackAura = MakeHalo("AttackAura", DiscSprite(), AttackAuraColor, Mathf.Min(_bodySortingOrders) - 1);
            _attackAuraTimer = AttackAuraDuration;
        }

        // 16px 원 — PPU 16이라 지름 1유닛, 몸 크기에 맞춰 늘린다. 테두리 1px(링) 또는 꽉 찬 원판
        private static Sprite RingSprite() => _ringSprite != null ? _ringSprite : _ringSprite = CircleSprite(true);
        private static Sprite DiscSprite() => _discSprite != null ? _discSprite : _discSprite = CircleSprite(false);

        private static Sprite CircleSprite(bool ringOnly)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Vector2 center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    bool on = d <= size * 0.5f - 0.5f && (!ringOnly || d > size * 0.5f - 1.5f);
                    texture.SetPixel(x, y, on ? Color.white : Color.clear);
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private SpriteRenderer MakeHalo(string name, Sprite sprite, Color color, int sortingOrder)
        {
            var halo = new GameObject(name).AddComponent<SpriteRenderer>();
            halo.transform.SetParent(_visual, false);
            float diameter = Mathf.Max(_baseBodySize.x, _baseBodySize.y) * HaloDiameterScale;
            halo.transform.localScale = Vector3.one * (diameter / Mathf.Abs(_baseVisualScale.y));
            halo.transform.localPosition = _body.offset / Mathf.Abs(_baseVisualScale.y);
            halo.sprite = sprite;
            halo.color = color;
            halo.sharedMaterial = _weaponRenderer.sharedMaterial;
            halo.sortingLayerID = _weaponRenderer.sortingLayerID;
            halo.sortingOrder = sortingOrder;
            return halo;
        }

        // 이 생애에 이 효과를 처음 쓰나 — 쓰면 표시한다(루니카 첫 피격 · 충정의 맹세 첫 죽음)
        public bool UseOnce(UnitEffect effect) => _usedOnce.Add(effect);

        // 이 생애에 이 효과가 센 수에 1을 더하고 그 수를 돌려준다(기사 서임 — 이 유닛의 처치 수). 생애마다 0부터
        private readonly Dictionary<UnitEffect, int> _effectCounts = new Dictionary<UnitEffect, int>();

        public int AddCount(UnitEffect effect)
        {
            _effectCounts.TryGetValue(effect, out int count);
            _effectCounts[effect] = ++count;
            return count;
        }

        // 지금 체력을 최대 체력의 비율로(기사 서임 — 바뀌기 전 유닛의 남은 비율을 이어 간다). 피해 · 회복이 아니다
        public void SetHpRatio(float ratio)
        {
            if (IsAlive)
                _hp = Mathf.Max(DoomedHp, MaxHp * Mathf.Clamp01(ratio));
        }

        // 적과 맞붙어 있나(앞이 적 몸에 막혔다 — 지난 물리 스텝) · 앞에 맞닿은 아군(그중 가장 느린 쪽, 없으면 null) — 방패 대열이 「바로 뒤 아군」을 가린다
        public bool Engaged { get; private set; }
        public Unit AllyInFront { get; private set; }
        // 지금 죽는 중인 이 유닛이 그 자리에서 다시 일어나는가(죽지 않는 전사) — 사망 시 효과 안에서만 뜻이 있다(영혼의 메아리는 다시 일어나는 죽음을 세지 않는다)
        public bool Reviving { get; private set; }

        // 죽는다 — 처치 효과(처치자 진영) → 사망 효과(내 진영, 하마 교련장 등)
        private void Perish(IDamageable attacker)
        {
            Vector2 at = _rb.position;
            BattleManager.NoteProgress();
            // 죽지 않는 전사 — 죽을 때마다 확률로 그 자리에서 다시 일어난다. 사망 시 효과 · 처치 효과는 그대로 먼저 돌고(그동안은 죽은 유닛이다),
            // 몸은 사라지지 않아 받은 강화가 그대로 남는다. 소환이 아니다
            float reviveChance = 0f;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    reviveChance += e.Effect.ReviveChance;
            bool revives = reviveChance > 0f && Random.value < reviveChance;
            Reviving = revives;
            _hp = Mathf.Min(_hp, 0f);
            if (!revives)
                Die();
            if (attacker is Unit killer && killer.IsAlive)
            {
                foreach (TeamEffects.Entry e in TeamEffects.For(killer._team))
                    if (e.Effect.AppliesFor(killer._team, killer._definition))
                        e.Effect.OnKill(killer, this, e.Stacks);
            }
            else if (attacker is Base tower)
                tower.NotifyKill(this);
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    e.Effect.OnDied(this, at, e.Stacks);
            Team foe = _team == Team.Ally ? Team.Enemy : Team.Ally;
            foreach (TeamEffects.Entry e in TeamEffects.For(foe))
                e.Effect.OnEnemyDied(foe, this, at, e.Stacks);
            // 양 진영의 기물이 죽은 수를 센다(약탈족장 라이칸스로프 — 아군·적 구분 없이)
            BoardRunner.For(Team.Ally)?.NotifyUnitDied(this);
            BoardRunner.For(Team.Enemy)?.NotifyUnitDied(this);
            Reviving = false;
            if (!revives)
                return;
            _hp = MaxHp;
            PillarFx.Rise(new Vector2(at.x, Feet), Top - Feet);   // 다시 일어나는 순간 발밑에서 기둥
            _doomTimer = 0f;
            _doomKiller = null;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    e.Effect.OnRevived(this);
        }

        // 소모 — 죽음이 아니다(사망 효과 없음). 연기만 남기고 사라진다(조립 라인)
        public void Consume()
        {
            if (!IsAlive)
                return;
            _hp = 0f;
            if (FxDirector.Instance != null)
                FxDirector.Instance.DeathPuff(_rb.position);
            gameObject.SetActive(false);
        }

        private void OnDisable() => Builders.Remove(this);

        // 건축가 한 스텝 — 이번 스텝의 가로 속도. 싸우지 않는다. 자리가 없으면 바로 돌아가고, 성(앞쪽 끝)에 닿으면 조용히 사라진다(죽음 아님)
        private float BuildStep()
        {
            float x = _rb.position.x;
            if (_buildPhase == BuildPhase.Find)
            {
                _buildX = FindBuildSite();
                _buildPhase = float.IsNaN(_buildX) ? BuildPhase.Returning : BuildPhase.Going;
                if (_buildPhase == BuildPhase.Going)
                    Builders.Add(this);
            }
            if (_buildPhase == BuildPhase.Going)
            {
                float dx = _buildX - x;
                if (Mathf.Abs(dx) > BuildArriveDistance)
                    return Mathf.Sign(dx) * MoveSpeed;
                _buildPhase = BuildPhase.Building;
                _buildTimer = _definition.BuildSeconds;
            }
            if (_buildPhase == BuildPhase.Building)
            {
                _buildTimer -= Time.fixedDeltaTime;
                _hammerTimer -= Time.fixedDeltaTime;
                if (_hammerTimer <= 0f)
                {
                    StartThrust(new Vector2(Forward, -1f).normalized);   // 망치질
                    _hammerTimer = HammerInterval;
                }
                if (_buildTimer > 0f)
                    return 0f;
                Base.BuildOnGround(_definition.BuildPrefab, _team, _buildX);
                Builders.Remove(this);
                // 건축설계조합 신화 — 건축가 한 기가 정해진 수까지 짓는다. 아직 남았으면 돌아가지 않고 다음 자리를 찾는다(다음 스텝부터 · 자리가 없으면 §7처럼 돌아간다)
                _buildPhase = ++_builtCount < BuildLimit() ? BuildPhase.Find : BuildPhase.Returning;
                if (_buildPhase == BuildPhase.Find)
                    return 0f;
            }
            Base home = Base.For(_team);
            float gate = home == null ? x : Forward > 0f ? home.Bounds.max.x : home.Bounds.min.x;
            if ((x - gate) * Forward <= 0f)
            {
                _hp = 0f;
                gameObject.SetActive(false);
                return 0f;
            }
            return -Forward * MoveSpeed;
        }

        // 이 생애에 지은 작은 타워 수
        private int _builtCount;

        // 건축가 한 기가 짓는 작은 타워 수 — 기본 1, 켜진 효과 중 가장 큰 값(건축설계조합 신화 3)
        private int BuildLimit()
        {
            int limit = 1;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    limit = Mathf.Max(limit, e.Effect.BuilderTowerLimit);
            return limit;
        }

        private float BuildHalfWidth => Base.HalfWidthOf(_definition.BuildPrefab);

        // 내 성문 앞에서 적 성 바로 앞까지, 내 성에 가장 가까운 빈 평지의 x — 내 성문 앞(BuildGateClearance)은 비우고, 타워 폭 안의 땅 높이 차가 BuildFlatness 이하,
        // 작은 타워(양 진영)·다른 건축가가 잡은 자리와 BuildSpacing 이상 떨어진 곳. 없으면 NaN
        private float FindBuildSite()
        {
            Base home = Base.For(_team);
            Base enemy = Base.For(_team == Team.Ally ? Team.Enemy : Team.Ally);
            if (home == null || enemy == null || _definition.BuildPrefab == null)
                return float.NaN;
            float half = BuildHalfWidth;
            float front = Forward > 0f ? home.Bounds.max.x : home.Bounds.min.x;
            float end = Forward > 0f ? enemy.Bounds.min.x : enemy.Bounds.max.x;
            for (float x = front + Forward * (_definition.BuildGateClearance + half); (end - x) * Forward >= half; x += Forward * BuildSearchStep)
                if (IsFlat(x, half) && !IsSiteTaken(x))
                    return x;
            return float.NaN;
        }

        private bool IsFlat(float x, float half)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float s = x - half; s <= x + half + 0.001f; s += BuildSearchStep)
            {
                float h = Ground.Instance.HeightAt(s);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            return max - min <= _definition.BuildFlatness;
        }

        private bool IsSiteTaken(float x)
        {
            float spacing = _definition.BuildSpacing;
            foreach (Base tower in Base.SmallTowers())
                if (Mathf.Abs(tower.transform.position.x - x) < spacing)
                    return true;
            foreach (Unit builder in Builders)
                if (builder != this && builder.IsAlive && Mathf.Abs(builder._buildX - x) < spacing)
                    return true;
            return false;
        }

        public Vector2 Position => _rb.position;

        public void AddModifier(StatModifier modifier)
        {
            _ownModifiers.Add(modifier);
            _ownModifiersChanged = true;
        }

        private float Stat(UnitStat stat)
        {
            if (_statsVersion != StatBook.Version || _ownModifiersChanged)
                RefreshStats();
            // 고도로 스탯이 바뀌는 유닛(미노타우로스)은 고도가 한 눈금 넘게 달라지면 다시 센다 — 지금 고도의 값이다
            else if (_usesAltitude && Mathf.Abs(Altitude - _statAltitude) >= AltitudeStep)
                RefreshStats(false);
            return _stats[(int)stat];
        }

        private static readonly UnitStat[] AltitudeStats = { UnitStat.Attack, UnitStat.MaxHp, UnitStat.MoveSpeed, UnitStat.AttackSpeed };
        // 고도로 바뀌는 스탯을 다시 세는 눈금(칸) — 0.1이면 고도 1당 +10%가 1%씩 움직인다
        private const float AltitudeStep = 0.1f;
        // 스탯에 들어가 있는 고도(눈금에 맞춘 값) · 고도를 읽는 효과가 이 유닛에 걸려 있나(걸려 있을 때만 고도를 본다)
        private float _statAltitude;
        private bool _usesAltitude;

        // 지금 고도로 오른 공격력 배율(1 = 고도 보너스 없음) — 연출(Fx/AltitudeMark · 붉은 기운 · 그림 크기)이 읽는다
        public float AltitudeAttackScale { get; private set; } = 1f;

        // 고도로 커 보이는 그림 배율 — 고도 1마다 +N%(상한) · 고도로 강해지지 않는 유닛은 1. 충돌 박스 · 키는 그대로(그림만)
        private float AltitudeLook => AltitudeAttackScale > 1f
            ? 1f + Mathf.Min(_statAltitude * BattleManager.AltitudeLookGrowth, BattleManager.AltitudeLookGrowthMax)
            : 1f;

        // 최대 체력이 바뀌면 지금 체력도 같은 비율로 — 가득 찬 유닛은 계속 가득. flash = 공격력이 오르면 오라를 반짝이나(고도로 조금씩 오르는 것은 반짝이지 않는다)
        private void RefreshStats(bool flash = true)
        {
            _usesAltitude = false;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    foreach (UnitStat stat in AltitudeStats)
                        _usesAltitude |= e.Effect.AltitudeBonus(_team, stat) != 0f;
            _statAltitude = _usesAltitude ? Mathf.Round(Altitude / AltitudeStep) * AltitudeStep : 0f;
            // 고도로 오른 공격력 배율(머리 위 「×1.3」 · 붉은 기운 · 커 보이는 그림 — 사용자 10/22)
            float altitudeAttack = 0f;
            if (_usesAltitude)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition))
                        altitudeAttack += e.Effect.AltitudeBonus(_team, UnitStat.Attack) * _statAltitude;
            AltitudeAttackScale = 1f + altitudeAttack;
            float oldMaxHp = _stats[(int)UnitStat.MaxHp];
            float oldAttack = _stats[(int)UnitStat.Attack];
            _stats[(int)UnitStat.Attack] = Compute(UnitStat.Attack);
            // 공격력 버프를 받은 순간 — 빨간 오라(사용자 10/7: 받는 순간 반짝). 생애 첫 계산(oldAttack 0)은 버프가 아니다
            if (flash && oldAttack > 0f && _stats[(int)UnitStat.Attack] > oldAttack + 0.0001f && IsAlive)
                FlashAttackAura();
            _stats[(int)UnitStat.MaxHp] = Compute(UnitStat.MaxHp);
            _stats[(int)UnitStat.MoveSpeed] = Compute(UnitStat.MoveSpeed);
            _stats[(int)UnitStat.AttackSpeed] = Compute(UnitStat.AttackSpeed);
            _statsVersion = StatBook.Version;
            _ownModifiersChanged = false;
            if (oldMaxHp > 0f && IsAlive)
                _hp *= _stats[(int)UnitStat.MaxHp] / oldMaxHp;
        }

        // 스탯의 기본값 — 공격속도는 기본 1의 배율이다(공격 간격 = UnitDefinition.AttackInterval ÷ 값). 회복 유닛은 공격력이 회복량
        public static float BaseStat(UnitDefinition definition, UnitStat stat)
            => stat == UnitStat.Attack ? definition.AttackDamage
            : stat == UnitStat.MaxHp ? definition.MaxHp
            : stat == UnitStat.MoveSpeed ? definition.MoveSpeed
            : 1f;

        // 진영에 걸린 것(전투 동안 · 영구히 · 맵)만 모은 합 — 유닛의 스탯 계산이 여기서 시작한다
        private static StatSum TeamSum(Team team, UnitDefinition definition, UnitStat stat)
        {
            StatSum sum = StatSum.Identity;
            StatBook.Accumulate(team, definition, stat, ref sum);
            TowerBook.AccumulateShared(team, definition, stat, ref sum);   // 화살 강화기 신화 — 타워 효과를 함께 받는 유닛
            return sum;
        }

        // 지금 이 진영에서 막 소환된 그 유닛의 스탯 — 유닛 하나하나에 따로 붙는 것(소환한 기물 옆의 강화 · 성장 · 무적 중 보너스)은 아직 없다.
        // 유닛 카드(UI/BuildingTooltip)가 전투 중에 읽는다 — 유닛의 계산(Compute)과 같은 길이다
        public static float SpawnStat(Team team, UnitDefinition definition, UnitStat stat)
            => TeamSum(team, definition, stat).Apply(BaseStat(definition, stat));

        // extraPercent = 이번 계산에만 얹는 합연산 %(고지 점령 — 높은 고도에서 한 공격 뒤의 공격속도)
        private float Compute(UnitStat stat, float extraPercent = 0f)
        {
            StatSum sum = TeamSum(_team, _definition, stat);
            sum.Add(StatOp.Percent, extraPercent);
            foreach (StatModifier m in _ownModifiers)
                if (m.Stat == stat)
                    sum.Add(m.Op, m.Value);
            // 미노타우로스 — 고도 1당 +%(합연산), 지금 고도로
            if (_usesAltitude)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition))
                        sum.Add(StatOp.Percent, e.Effect.AltitudeBonus(_team, stat) * _statAltitude);
            // 공주 루니카 — 무적 상태인 내 아군의 공격력 +%(합연산)
            if (stat == UnitStat.Attack && IsInvulnerable)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition))
                        sum.Add(StatOp.Percent, e.Effect.InvulnerableAttackBonus);
            return sum.Apply(BaseStat(_definition, stat));
        }

        // 소환 — 소환 지점 칸이 (적이든 아군이든) 차 있으면 한 층씩 올라가 비어 있는 가장 낮은 층에 만든다.
        // 그냥 겹쳐 만들면 1층에 끼인 채 쌓인다
        private const int SpawnMaxFloors = 30;
        private static readonly Vector2 SpawnCellProbe = new Vector2(0.9f, 0.9f);

        // 한꺼번에 여러 마리가 나올 때 — 소환 지점부터 앞쪽으로 이만큼의 칸을 먼저 채우고, 다 차면 위층으로
        private const int SpawnSpreadSlots = 5;
        // 주변 검색(적 찾기·사냥·칼 던질 띠) 간격 — 매 물리 스텝(0.02초)마다 하면 유닛이 수백일 때 프레임이 무너진다
        private const float TargetScanInterval = 0.1f;
        // 프로파일러 구간
        private static readonly Unity.Profiling.ProfilerMarker ContactsMarker = new Unity.Profiling.ProfilerMarker("Unit.Contacts");
        private static readonly Unity.Profiling.ProfilerMarker ScanMarker = new Unity.Profiling.ProfilerMarker("Unit.Scan");
        private static readonly Unity.Profiling.ProfilerMarker SupportMarker = new Unity.Profiling.ProfilerMarker("Unit.Support");
        private static readonly Unity.Profiling.ProfilerMarker WalkMarker = new Unity.Profiling.ProfilerMarker("Unit.Walk");
        private static readonly Unity.Profiling.ProfilerMarker ClimbMarker = new Unity.Profiling.ProfilerMarker("Unit.Climb");
        // 땅에 박혀 나오지 않게 발을 땅보다 살짝 위에
        private const float SpawnGroundGap = 0.02f;

        // 발을 feet에 맞춰 그대로 낸다(탑의 층 바닥 — 빈칸을 찾지 않는다. 겹쳐 나와도 층을 걸어 나가며 풀린다)
        public static Unit SpawnStanding(Unit prefab, Vector2 feet)
        {
            float height = prefab.GetComponent<BoxCollider2D>().size.y;
            return Pooled.Get(prefab, feet + Vector2.up * (height * 0.5f + SpawnGroundGap), Quaternion.identity);
        }

        public static Unit Spawn(Unit prefab, Vector2 groundPoint)
        {
            // groundPoint는 1층 칸의 중심. 키가 큰 유닛은 발을 같은 높이에 맞추도록 중심을 올리고, 키만큼 빈 공간을 찾는다
            Vector2 box = prefab.GetComponent<BoxCollider2D>().size;
            float height = box.y;
            // 폭이 큰 유닛(공룡)도 몸 전체가 들어갈 공간을 찾는다
            Vector2 probe = new Vector2(box.x - (1f - SpawnCellProbe.x), height - (1f - SpawnCellProbe.y));
            float forward = prefab._team == Team.Ally ? 1f : -1f;
            float slotWidth = Mathf.Max(1f, box.x);
            // 칸마다 땅 높이가 다를 수 있다 — groundPoint는 가로 위치만 쓰고, 각 칸의 땅 위에 발이 닿게 세운다
            float lift = height * 0.5f + SpawnGroundGap;
            // 층 정렬을 지키는 칸을 먼저 찾고(위층은 나보다 아래·같은 역할 위에만), 없으면 그냥 첫 빈칸
            Vector2 position = groundPoint;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int floor = 0; floor < SpawnMaxFloors; floor++)
                {
                    for (int slot = 0; slot < SpawnSpreadSlots; slot++)
                    {
                        float x = groundPoint.x + forward * slot * slotWidth;
                        position = new Vector2(x, Ground.Instance.HeightAt(x) + lift + floor);
                        if (IsSpawnCellFree(position, probe, prefab._team)
                            && (pass == 1 || floor == 0 || CanStackOn(position, height, prefab)))
                            return Pooled.Get(prefab, position, Quaternion.identity);
                    }
                }
            }
            return Pooled.Get(prefab, position, Quaternion.identity);
        }

        // 층 정렬: 소환 칸 바로 아래가 나보다 아래·같은 역할의 아군으로 차 있어야 그 위에 낸다.
        // 비어 있으면 떨어져서 결국 더 아래 아무 유닛 위에나 앉으므로 쓰지 않는다
        private static bool CanStackOn(Vector2 position, float height, Unit prefab)
        {
            Vector2 below = position + Vector2.down * (height * 0.5f + 0.5f);
            Physics2D.OverlapBox(below, SpawnCellProbe, 0f, SolidOnly, CellProbe);
            bool supported = false;
            foreach (Collider2D col in CellProbe)
            {
                if (!col.TryGetComponent(out Unit unit) || unit._team != prefab._team)
                    continue;
                if (prefab._definition.StackRank < unit._definition.StackRank)
                    return false;
                supported = true;
            }
            return supported;
        }

        // 같은 진영 기지는 몸이 통과하므로 빈칸 검사에서 뺀다 — 안 빼면 성문 안 소환 칸이 늘 막힌 걸로 보인다.
        // 땅도 뺀다 — 굽은 땅에서는 1층 칸 아래쪽이 경사에 걸려 늘 막힌 걸로 보인다
        private static bool IsSpawnCellFree(Vector2 position, Vector2 probe, Team team)
        {
            Physics2D.OverlapBox(position, probe, 0f, SolidOnly, CellProbe);
            foreach (Collider2D col in CellProbe)
            {
                if (col.TryGetComponent(out Ground _))
                    continue;
                if (col.TryGetComponent(out Base b) && b.Team == team)
                    continue;
                return false;
            }
            return true;
        }
        public float Forward => _team == Team.Ally ? 1f : -1f;
        // 같이 걸어가는 앞 유닛은 막은 게 아니다.
        // 속도값은 매 스텝 전진 속도로 덮어쓰므로 못 믿는다 — 실제로 움직인 거리로 판정
        private bool IsStopped => _advanceSpeed < MoveSpeed * 0.5f;
        private float MoveSpeed => Stat(UnitStat.MoveSpeed) * (Time.fixedTime < _slowUntil ? _slowFactor : 1f);

        // 잠깐 느려진다(사냥꾼의 활) — 이동속도 ×factor. 느려진 동안 다시 걸리면 시간은 새로, 배율은 더 센 쪽
        private float _slowFactor = 1f;
        private float _slowUntil;

        public void Slow(float factor, float seconds)
        {
            _slowFactor = Time.fixedTime < _slowUntil ? Mathf.Min(_slowFactor, factor) : factor;
            _slowUntil = Time.fixedTime + seconds;
            StatusDots.Watch(this);
        }

        // 지금 느려진 중 · 불타는 중 — 몸 상태 점(Fx/StatusDots)이 읽는다
        public bool IsSlowed => Time.fixedTime < _slowUntil;
        public bool IsBurning => _burnTicksLeft > 0;

        // 고지 점령 — 방금 공격한 적보다 높은 고도였으면 다음 공격까지의 간격에 공격속도 +%(합연산)를 얹는다
        private float IntervalAfter(IDamageable target)
        {
            if (!IsAbove(target))
                return AttackInterval;
            float bonus = 0f;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    bonus += e.Effect.HighGroundAttackSpeedBonus(_team);
            if (bonus > 0f)
                HighGroundSpark(target, new Vector2(Forward, 0f));
            return bonus > 0f ? _definition.AttackInterval / Compute(UnitStat.AttackSpeed, bonus) : AttackInterval;
        }

        // 고지 보너스가 붙은 타격 — 맞은 자리에 주황 섬광(기름투하대 · 고지 점령)
        private static void HighGroundSpark(IDamageable target, Vector2 direction)
        {
            if (FxDirector.Instance != null && target is Unit hit)
                FxDirector.Instance.HitSpark(hit.Position, direction, FxDirector.FireColor);
        }

        // 돌격 나팔 — 이 생애의 첫 공격에 곱하는 피해 배율(1 + 켜진 효과들의 +%). 한 번 읽으면 그 효과는 썼다
        private float FirstAttackScale()
        {
            float scale = 1f;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                {
                    float bonus = e.Effect.FirstAttackDamageBonus(_team);
                    if (bonus > 0f && UseOnce(e.Effect))
                        scale += bonus;
                }
            return scale;
        }

        // 연사 — 사거리 안의 적에게 화살 한 발을 바로 더 쏜다(공격 간격과 무관). 노릴 적이 없으면 쏘지 않는다
        public void ShootExtra()
        {
            if (_definition.AttackType == AttackType.Ranged && FindTarget(out IDamageable target, out Vector2 targetPoint))
                Shoot(target, targetPoint, true);
        }
        // 다음 공격까지 — 이번 스텝에 넘친 시간(한 스텝까지)을 이월한다. 안 하면 간격이 물리 스텝 배수로 올림돼 공격이 느려진다
        // (0.033초 스텝에서 0.167초 간격이 0.2초가 되는 식). 오래 쉬다 쏜 첫 발은 한 스텝까지만 이월해 연사가 몰리지 않는다
        public static float NextCooldown(float current, float interval) => interval + Mathf.Max(current, -Time.fixedDeltaTime);
        // 공격속도 +10% = 간격이 1.1로 나뉜다
        public float AttackInterval => _definition.AttackInterval / Stat(UnitStat.AttackSpeed);
        // 타워 효과를 함께 받는 유닛이면 타워의 사거리 변경이 얹힌다(화살 강화기 신화)
        private float AttackRange => TowerBook.SharedCompute(_team, _definition, TowerStat.Range, _definition.AttackRange);
        // 전투 공격력 가속
        public float DamageScale => BattleManager.DamageMultiplier * _attackScale;
        // 지금 하는 공격 한 번에만 곱하는 배율(돌격 나팔의 첫 공격) — 공격이 끝나면 1
        private float _attackScale = 1f;

        // 소환됐다(BoardRunner.Summon) — 소환 이벤트
        public void NotifySummoned(int sourceSlot, BuildingDefinition source)
        {
            SummonedBy = source;
            SourceSlot = sourceSlot;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    e.Effect.OnSummoned(this, sourceSlot, e.Stacks);
        }
        public float Feet => _rb.position.y - _halfHeight;
        public float Top => _rb.position.y + _halfHeight;

        // 고도(트라이브 명세 §2-3 · 사용자 결정 10/13) — 발 높이 − 그 맵에서 가장 낮은 땅의 높이, 1 = 1칸. 지금 값이다:
        // 맵의 언덕 · 뼛가루 언덕 · 아군을 밟고 선 높이 · 점프가 전부 들어간다. 「대상보다 높은가」(기름투하대 · 고지 점령 — IsAbove)와는 다른 값이다
        // 탑(본진 · 그 위의 층)은 고도로 치지 않는다(사용자 결정 10/14 「고도는 땅, 유닛, 뼛가루 위에서만」) — 어느 진영이든 본진의 폭 안에 있는 동안은 그 자리의 땅 높이로 센다
        public float Altitude
        {
            get
            {
                if (Ground.Instance == null)
                    return 0f;
                float feet = Feet;
                for (int team = 0; team < 2; team++)
                {
                    Base home = Base.For((Team)team);
                    if (home != null && _rb.position.x >= home.Bounds.min.x && _rb.position.x <= home.Bounds.max.x)
                        feet = Mathf.Min(feet, Ground.Instance.HeightAt(_rb.position.x));
                }
                return Mathf.Max(0f, feet - Ground.Instance.LowestY);
            }
        }
        // 뼛가루 언덕 위에 있나(맵의 원래 언덕과 구분된다) — 발밑 자리에 뼛가루가 쌓여 있으면. 그 위에서 아군을 밟고 선 유닛도 친다
        public bool OnBoneDust => Ground.Instance != null && Ground.Instance.OnBoneDust(_rb.position.x);

        // 발밑의 땅이 자랐다(뼛가루 언덕 — Ground) — 그만큼 그대로 올라간다
        public void Lift(float rise) => _rb.position += Vector2.up * rise;
        // 점프 높이(발이 올라가는 높이) = 내 키 + N칸(BattleConfig, 기본 2) — 모든 유닛 공통. 2면 한 번에 두 층을 올라타 산이 가파르게 선다(사용자 10/6 "뾰족한 산")
        private float JumpHeight => _halfHeight * 2f + BattleManager.ClimbJumpExtra;

        // 머리 바로 위 한 칸 — 거기 선 유닛(없으면 null), 칸이 완전히 비었나.
        // 한 물리 스텝 안에서는 몸이 움직이지 않으므로 처음 물은 답을 그 스텝 동안 다시 쓴다 — 산 뒤의 여러 유닛이 같은 기둥을 훑는다
        private Unit OnHead(out bool free)
        {
            if (_onHeadAt != Time.fixedTime)
            {
                _onHeadAt = Time.fixedTime;
                _onHeadUnit = null;
                Vector2 above = new Vector2(_rb.position.x, Top + 0.5f);
                _onHeadFree = Physics2D.OverlapBox(above, new Vector2(0.8f, 0.8f), 0f, SolidOnly, CellProbe) == 0;
                foreach (Collider2D col in CellProbe)
                    if (col.TryGetComponent(out Unit unit) && unit != this)
                    {
                        _onHeadUnit = unit;
                        break;
                    }
            }
            free = _onHeadFree;
            return _onHeadUnit;
        }
        private float _onHeadAt = -1f;
        private Unit _onHeadUnit;
        private bool _onHeadFree;

        // 이번 스텝에 앞에서 맞닿은, 나보다 느린 아군들(접촉 순서) — 그중 하나라도 기둥 위로 올라탈 수 있나
        private readonly List<Unit> _climbCandidates = new List<Unit>();
        private Unit FindClimbTarget()
        {
            foreach (Unit front in _climbCandidates)
            {
                Unit top = ClimbTarget(front);
                if (top != null)
                    return top;
            }
            return null;
        }

        // 앞 아군 위로 쌓인 기둥의 꼭대기 — 점프로 닿고, 그 위가 비었고, 층 정렬상 그 위에 서도 되면 그 유닛. 아니면 null
        private Unit ClimbTarget(Unit front)
        {
            using var _ = ClimbMarker.Auto();
            Unit top = front;
            for (int i = 0; i < ClimbMaxColumn; i++)
            {
                if (top.Top - Feet > JumpHeight - ClimbReachMargin)
                    return null;   // 닿지도 않는 높이에 계속 뛰지 않게
                Unit above = top.OnHead(out bool free);
                if (free)
                    // 나보다 위층 역할(예: 근접 앞의 원딜) 위에도 오른다 — 올라탄 뒤 층 정렬(FixedUpdate의 BelongsBelow)이 앞으로 걸어 내려보내
                    // 원딜을 넘어간다. 뒤에서 기다리면 멈춰 쏘는 원딜 뒤에 근접이 줄 서서 못 싸운다(사용자 10/10)
                    return top;
                if (above == null || above._team != _team || !above.IsAlive)
                    return null;   // 유닛 아닌 것(기지 등)·적이 막고 있다
                top = above;
            }
            return null;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _body = GetComponent<BoxCollider2D>();
            _weaponRenderer = _weapon.GetComponent<SpriteRenderer>();
            _weaponSortingOrder = _weaponRenderer.sortingOrder;
            var bodies = new List<SpriteRenderer>();
            foreach (SpriteRenderer sr in _visual.GetComponentsInChildren<SpriteRenderer>())
                if (sr != _weaponRenderer)
                    bodies.Add(sr);
            _bodyRenderers = bodies.ToArray();
            _bodyColors = new Color[_bodyRenderers.Length];
            _bodySortingOrders = new int[_bodyRenderers.Length];
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                // 영주 색 — 그림의 파란 자리만 이 진영 영주의 색으로(Core/LordPaint). color는 건드리지 않아 번쩍임·말 색과 부딪히지 않는다
                _bodyRenderers[i].sprite = LordPaint.Of(_bodyRenderers[i].sprite, _team);
                _bodyColors[i] = _bodyRenderers[i].color;
                _bodySortingOrders[i] = _bodyRenderers[i].sortingOrder;
            }
            _baseBodySize = _body.size;
            _halfHeight = _body.size.y * 0.5f;
            _baseVisualScale = _visualScale = _visual.localScale;
            _weaponRestPosition = _weapon.localPosition;
            // 진영별 유닛 레이어 — 충돌 규칙은 그대로 두고 검색에서만 거른다. 적 찾기가 산 속 아군 수백 마리를 훑지 않게
            int ownLayer = LayerMask.NameToLayer(_team == Team.Ally ? "AllyUnit" : "EnemyUnit");
            gameObject.layer = _definition.AttackType == AttackType.Build ? Base.BuilderLayer(_team) : ownLayer;   // 건축가는 같은 진영 유닛과 부딪히지 않는다(Base.SetupLayers)
            _enemyFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = ~(1 << ownLayer) };
            _allyFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << ownLayer };
            // 그림은 오른쪽(아군 전방)을 보고 그려져 있다 — 전방이 -x면 자식(무기)까지 통째로 뒤집는다
            if (Forward < 0f)
                transform.localScale = Vector3.Scale(transform.localScale, new Vector3(-1f, 1f, 1f));
            foreach (Collider2D col in GetComponents<Collider2D>())
                ByCollider[col] = this;
        }

        private void OnDestroy()
        {
            foreach (Collider2D col in GetComponents<Collider2D>())
                ByCollider.Remove(col);
        }

        // 한 생애의 시작 — 처음 만들 때도, 풀에서 다시 꺼낼 때도(Pooled) 여기서 지난 생애의 흔적을 모두 지운다
        private void OnEnable()
        {
            Life++;
            SummonedBy = null;
            SourceSlot = -1;
            CombatPower.NoteSpawn(_team);
            if (Size != 1f)
                SetSize(1f);
            _ownModifiers.Clear();
            _effectReadyAt.Clear();
            _invulnerableTimer = 0f;
            _doomTimer = 0f;
            _doomKiller = null;
            _usedOnce.Clear();
            _effectCounts.Clear();
            Engaged = false;
            AllyInFront = null;
            Reviving = false;
            _slowUntil = 0f;
            _stats[(int)UnitStat.MaxHp] = 0f;  // 지난 생애의 최대 체력으로 비율을 맞추지 않게
            _stats[(int)UnitStat.Attack] = 0f;  // 지난 생애의 공격력과 비교해 오라가 켜지지 않게
            _attackAuraTimer = 0f;
            _hp = 0f;
            // 늘 켜진 효과(미노타우로스의 고도) — 이 유닛을 낸 기물과 상관없이 내 진영에 켠다(전투 시작에 TeamEffects가 비워진다)
            foreach (UnitEffect effect in _definition.InnateEffects)
                TeamEffects.Enable(_team, effect, 1);
            RefreshStats();
            _hp = MaxHp;
            _lastX = transform.position.x;
            _advanceSpeed = 0f;
            _attackCooldown = 0f;
            _knockbackTimer = 0f;
            _runTime = 0f;
            _bigKnockbackUsed = false;
            _stompTimer = 0f;
            _climbing = false;
            _holdBack = false;
            StartCoroutine(HoldBack());
            _climbRestTimer = 0f;
            _stackLayer = 0;
            _supportFeet = float.MaxValue;
            _allyBehind = false;
            _allyAhead = false;
            _onHeadAt = -1f;
            _slamTimer = _definition.SlamInterval;
            _slamJumping = false;
            _slamLeftGround = false;
            _onBase = false;
            _despawnTimer = 0f;
            // 검색 시점을 유닛마다 흩어 한 스텝에 몰리지 않게
            _targetScanTimer = Random.Range(0f, TargetScanInterval);
            _cachedTarget = null;
            _cachedTargetCollider = null;
            _targetHoldUntil = 0f;
            _turnBackRetryAt = 0f;
            _supportScanTimer = Random.Range(0f, TargetScanInterval);
            _walkScanTimer = Random.Range(0f, TargetScanInterval);
            _turnBackScanTimer = Random.Range(0f, TargetScanInterval);
            _turnBackX = float.NaN;
            _cachedPatient = null;
            _aheadOnBaseUnit = false;
            _buildPhase = BuildPhase.Find;
            _lookBack = false;
            _hammerTimer = 0f;
            _buildStallTimer = 0f;
            _builtCount = 0;
            _burstLeft = 0;
            _burstAimed.Clear();
            _volley.Clear();
            _burnTicksLeft = 0;
            _burnDamage = 0f;
            _burnAttacker = null;

            // 죽을 때 바꾼 물리·그림을 되돌린다(Die)
            foreach (Collider2D col in GetComponents<Collider2D>())
                col.enabled = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
            for (int i = 0; i < _bodyRenderers.Length; i++)
                _bodyRenderers[i].color = _bodyColors[i];
            _weapon.localPosition = _weaponRestPosition;
            _weapon.localRotation = Quaternion.identity;
            _visual.localPosition = Vector3.zero;
            _visual.localScale = _visualScale;
            _visual.gameObject.SetActive(true);
            _squashTime = SquashDuration;
            _flashTimer = 0f;
            _thrustTime = ThrustDuration;

            // 같은 진영 기지 통과 — 콜라이더를 껐다 켜면 무시 설정이 풀릴 수 있어 생애마다 다시 건다
            Base.IgnoreOwnBase(_team, _body);
        }

        private void FixedUpdate()
        {
            if (!IsAlive)
                return;

            if (_invulnerableTimer > 0f)
            {
                _invulnerableTimer -= Time.fixedDeltaTime;
                if (_invulnerableTimer <= 0f)
                {
                    _ownModifiersChanged = true;   // 무적 중 공격력 보너스가 끝난다
                    foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                        if (e.Effect.AppliesFor(_team, _definition))
                            e.Effect.OnInvulnerableEnded(this);
                }
            }
            // 충정의 맹세 — 유예가 끝나면 쓰러진다
            if (_doomTimer > 0f)
            {
                _doomTimer -= Time.fixedDeltaTime;
                if (_doomTimer <= 0f)
                {
                    _hp = 0f;
                    _invulnerableTimer = 0f;
                    Perish(_doomKiller);
                    return;
                }
            }

            if (_burstLeft > 0)
                Burst();
            _volley.Tick(Time.fixedDeltaTime, _team, WeaponPoint, this);
            // 불타는 중(기름투하대 신화) — 1초마다 피해. 밀치는 힘이 0이라 밀림·경직은 없다
            if (_burnTicksLeft > 0)
            {
                _burnTick -= Time.fixedDeltaTime;
                if (_burnTick <= 0f)
                {
                    _burnTick += BurnInterval;
                    float damage = _burnDamage;
                    if (--_burnTicksLeft == 0)
                        _burnDamage = 0f;
                    TakeDamage(damage, Vector2.up, 0f, _burnAttacker is Unit igniter && igniter.Life != _burnAttackerLife ? null : _burnAttacker);
                    if (!IsAlive)
                        return;
                }
            }

            _advanceSpeed = (_rb.position.x - _lastX) * Forward / Time.fixedDeltaTime;
            _lastX = _rb.position.x;
            _attackCooldown -= Time.fixedDeltaTime;
            _climbRestTimer -= Time.fixedDeltaTime;
            // 공격력이 오른 순간을 바로 잡도록(빨간 오라) 바뀐 스탯은 기다리지 않고 다시 계산
            if (_statsVersion != StatBook.Version || _ownModifiersChanged)
                RefreshStats();
            _targetScanTimer -= Time.fixedDeltaTime;
            _supportScanTimer -= Time.fixedDeltaTime;
            _walkScanTimer -= Time.fixedDeltaTime;
            _turnBackScanTimer -= Time.fixedDeltaTime;

            // 접촉 법선은 상대 → 나 방향: 위를 향하면 발밑, 전방 반대를 향하면 앞에서 막힌 것
            bool grounded = false;
            bool blockedByEnemy = false;
            _climbCandidates.Clear();
            Unit allyBelow = null;
            Unit allyAbove = null;
            Unit enemyBelow = null;
            bool onDeck = false;
            bool onGroundSurface = false;
            int belowLayer = -1;
            float frontSupport = float.NegativeInfinity;   // 발밑 아군 중 가장 앞(전방 좌표)
            bool allyBehind = false, allyAhead = false;   // 같은 높이에서 뒤·앞에 아군이 붙어 있나(등반 착지 자리 고르기)
            Unit allyInFront = null;                      // 앞에 맞닿은 아군 중 가장 느린 쪽(밀어붙이지 않기)
            _onBase = false;
            ContactsMarker.Begin();
            int count = _rb.GetContacts(_contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = _contacts[i];
                // 컴포넌트 조회는 접촉 하나에 한 번만 — 산 속 유닛은 접촉이 많아 여기가 제일 자주 돈다
                Collider2D other = contact.collider;   // 게터가 매번 네이티브에서 찾는다 — 한 번만
                ByCollider.TryGetValue(other, out Unit touched);
                if (contact.normal.y < -0.5f && touched != null && touched._team == _team)
                    allyAbove = touched;
                if (contact.normal.y > 0.5f)
                {
                    grounded = true;
                    if (touched != null)
                    {
                        belowLayer = Mathf.Max(belowLayer, touched._stackLayer);
                        if (touched._team == _team)
                        {
                            allyBelow = touched;
                            frontSupport = Mathf.Max(frontSupport, touched._rb.position.x * Forward);
                        }
                        else
                            enemyBelow = touched;
                    }
                    else if (other.usedByEffector)
                        onDeck = true;   // 이펙터를 쓰는 콜라이더는 탑의 층 바닥뿐(Tower)
                    else if (other.TryGetComponent(out Base _))
                        _onBase = true;
                    else
                        onGroundSurface = true;   // 유닛·층 바닥·기지가 아닌 발판 = 땅
                }
                if (contact.normal.x * Forward > 0.5f && touched != null && touched._team == _team)
                    allyBehind = true;
                if (contact.normal.x * Forward < -0.5f && touched != null && touched._team == _team)
                {
                    allyAhead = true;
                    if (allyInFront == null || touched._advanceSpeed < allyInFront._advanceSpeed)
                        allyInFront = touched;
                }
                if (contact.normal.x * Forward < -0.5f && touched != null && touched._team != _team)
                    blockedByEnemy = true;   // 발밑 판정과 별개 — 대각선 접촉(발밑이면서 앞)도 막힌 것
                else if (contact.normal.y <= 0.5f && contact.normal.x * Forward < -0.5f && touched != null   // 앞의 아군
                         && touched._advanceSpeed < MoveSpeed * BattleManager.ClimbSlowerThan   // 나보다 느리면(멈추지 않았어도) 넘는다 — 같은 속도로 같이 걷는 줄은 안 넘는다
                         && !touched._onBase)
                    _climbCandidates.Add(touched);   // 기둥 검사(물리)는 비싸다 — 정말 뛸 수 있는 자리(땅을 딛고 걷기·싸움 분기)에서만 FindClimbTarget으로
            }
            ContactsMarker.End();
            _allyBehind = allyBehind;
            _allyAhead = allyAhead;
            Engaged = blockedByEnemy;
            AllyInFront = allyInFront;
            ReportPile(grounded, onGroundSurface);
            if (grounded)
            {
                if (_rb.linearVelocity.y <= 0.1f)
                    _climbing = false;   // 뛰어오르는 첫 스텝은 아직 발이 닿아 있다
                _stackLayer = onGroundSurface || belowLayer < 0 ? 0 : belowLayer + 1;
                _supportFeet = Feet;
            }

            // 밟기 피해 — 적 머리 위에 서 있는 동안 초당 공격력의 일정 비율(BattleConfig). 숫자가 매 스텝 뜨지 않게 간격마다 몰아서.
            // 서 있던 시간만 센다 — 맞아서 잠깐 떴다 내려앉아도 쌓인 시간은 남는다. 처치는 처치로 친다(처치 효과) · 공격이 아니므로 "공격할 때마다" 효과는 없다
            if (enemyBelow != null && enemyBelow.IsAlive)
            {
                _stompTimer += Time.fixedDeltaTime;
                if (_stompTimer >= StompTickInterval)
                {
                    _stompTimer -= StompTickInterval;
                    float damage = AttackDamage * DamageScale * BattleManager.StompDamagePerSecond * StompTickInterval;
                    // 회복 유닛은 공격력이 회복량이다 — 밟아도 피해를 주지 않는다
                    if (damage > 0f && _definition.AttackType != AttackType.Heal)
                        enemyBelow.TakeDamage(damage, Vector2.down, 0f, this);
                }
            }

            // 점프 착지 충격: 뛰어오른 뒤 한 번이라도 땅에서 떨어졌다가 다시 발이 닿는 순간 (넉백 중이어도 착지는 착지)
            if (_slamJumping)
            {
                if (!grounded)
                    _slamLeftGround = true;
                else if (_slamLeftGround)
                {
                    _slamJumping = false;
                    Slam();
                }
                else
                {
                    // 아직 발을 못 뗐다 — 뛴 스텝에 맞으면 넉백 속도가 점프 속도를 덮어쓴다. 뜰 때까지 다시 준다
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, JumpSpeed(_definition.SlamJumpHeight, _rb));
                    grounded = false;
                }
            }

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.fixedDeltaTime;
                _holdBack = false;
                return;
            }
            _holdBack = _definition.AttackType != AttackType.Build;   // 건축가는 성으로 걸어 돌아간다
            _holdX = _rb.position.x;

            // 공격·이동과 별개로 매 스텝 도는 효과(칼 던지기 등)
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    e.Effect.Tick(this, e.Stacks);

            // 점프 착지 충격: 간격마다 제자리에서 뛴다. 이번 스텝의 등반 점프 등이 덮어쓰지 않게 땅에서 떨어진 것으로 친다
            if (_definition.SlamInterval > 0f && !_slamJumping)
            {
                _slamTimer -= Time.fixedDeltaTime;
                if (_slamTimer <= 0f && grounded)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, JumpSpeed(_definition.SlamJumpHeight, _rb));
                    _slamTimer = _definition.SlamInterval;
                    _slamJumping = true;
                    _slamLeftGround = false;
                    grounded = false;
                }
            }

            Vector2 velocity = _rb.linearVelocity;

            // 탑의 층 바닥 위 — 싸우지도 뛰지도 않고 앞쪽 가장자리로 걸어 나가 떨어진다(사용자 10/8). 탑 위에 남아 활만 쏘지 않게
            if (onDeck)
            {
                velocity.x = Forward * MoveSpeed;
                _rb.linearVelocity = velocity;
                return;
            }

            if (_definition.AttackType == AttackType.Build)
            {
                velocity.x = BuildStep();
                // 오르막에서 발 모서리가 땅 꼭짓점에 걸려 서 버리면 살짝 뛰어 넘는다 — 안 그러면 잡아 둔 자리를 쥔 채 끝까지 서 있는다
                bool stalled = grounded && !blockedByEnemy && velocity.x != 0f && Mathf.Abs(_advanceSpeed) < MoveSpeed * BuildStallSpeedRatio;
                _buildStallTimer = stalled ? _buildStallTimer + Time.fixedDeltaTime : 0f;
                if (_buildStallTimer >= BuildStallSeconds)
                {
                    velocity.y = JumpSpeed(BuildStallHopHeight, _rb);
                    _buildStallTimer = 0f;
                }
                _rb.linearVelocity = velocity;
                return;
            }

            // 층 정렬 — 등반(원딜을 넘어가려고)·넉백·착지 등으로 뒤집혀 올라탔으면
            // 내가 바로 아래 아군보다 아래층 역할일 때(예: 원딜 위의 탱커) 싸움보다 먼저 앞으로 걸어 내려간다.
            // 밑의 아군은 그동안 멈춰 선다(BelongsBelow) — 내가 앞쪽 땅에 떨어지면 그 아군이 뒤에서 내 위로 올라탄다(보통 등반 점프).
            // 앞이 적 몸으로 막혔으면 내려갈 곳이 없으니 그 자리에서 싸운다
            if (allyBelow != null && BelongsBelow(allyBelow) && !blockedByEnemy)
            {
                velocity.x = Forward * MoveSpeed;
                _rb.linearVelocity = velocity;
                return;
            }
            bool holdForAbove = allyAbove != null && allyAbove.BelongsBelow(this);
            // 피라미드의 중요 자리 — 적과 맞닿은 맨 앞줄 · 머리에 아군이 올라탄 받침. 스스로 등반 점프를 하지 않는다(사용자 10/8)
            bool keySpot = blockedByEnemy || allyAbove != null;

            // 프리스트 — 적은 공격하지 않는다. 다친 아군을 회복하고, 적이 사거리에 들면 뒤에 멈춰 선다(앞으로 걸어가 죽지 않게)
            if (_definition.AttackType == AttackType.Heal)
            {
                bool enemyNear = FindTargetCached(out _, out _);
                // 회복 대상 찾기도 아군 전부를 훑어 비싸다 — 적 찾기와 같은 간격으로만 새로 찾고, 그 사이엔 찾아 둔 대상이 아직 유효한지만 본다
                SupportMarker.Begin();
                if (_supportScanTimer <= 0f)
                {
                    _cachedPatient = FindHealTarget(null);
                    _supportScanTimer = TargetScanInterval;
                }
                if (_cachedPatient != null && !_cachedPatient.IsDamaged)
                    _cachedPatient = null;
                Unit patient = _cachedPatient;
                SupportMarker.End();
                if (patient != null && _attackCooldown <= 0f)
                {
                    Vector2 toPatient = patient._rb.position - _rb.position;
                    LookToward(toPatient.x);
                    ThrowHeal(patient);
                    foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                        if (e.Effect.AppliesFor(_team, _definition))
                            e.Effect.OnHealed(this, patient, e.Stacks);
                    StartThrust(toPatient.sqrMagnitude > 0.0001f ? toPatient.normalized : new Vector2(Forward, 0f));
                    _attackCooldown = NextCooldown(_attackCooldown, AttackInterval);
                }
                // 한동안 피해가 없으면(대기 풀림) 뒤에 서 있지 않고 같이 전진한다 — 회복 전용끼리 사거리에서 마주 서면 영영 안 끝난다
                if ((enemyNear || patient != null) && !BattleManager.Stalled)
                {
                    velocity.x = 0f;
                    _rb.linearVelocity = velocity;
                    return;
                }
            }
            // 싸움이 점프·전진보다 우선 (프리스트는 위에서 처리 — 회복할 대상도 적도 없으면 여기로 내려와 전진)
            if (_definition.AttackType != AttackType.Heal && FindTargetCached(out IDamageable target, out Vector2 targetPoint))
            {
                // 싸우는 중에도 앞 아군 기둥 위로 올라탄다 — 사거리 안에 적이 있다고 그 자리에 서면 산이 옆으로만 넓어진다(사용자 10/6 "뾰족한 산").
                // 뛰어오른 뒤 내려앉을 때까지는 앞으로 나아가야 기둥 위에 앉는다
                // 피라미드의 중요 자리(맨 앞줄·받침)는 뛰지 않는다 — 그 밖(원딜 포함)은 적극적으로 올라타 피라미드를 쌓는다(사용자 10/8)
                if (grounded && !keySpot && _climbRestTimer <= 0f)
                {
                    Unit top = FindClimbTarget();
                    if (top != null && HeadClear())
                        StartClimb(top, ref velocity);
                }
                // 아군 위에서 싸우는 유닛도 계단 자리까지는 앞으로 다가앉는다 — 내려앉은 자리에 그대로 서면 기둥이 생긴다
                bool settle = allyBelow != null && !blockedByEnemy && !holdForAbove && StairActive;
                // 대기 풀림 — 사거리 안에 적이 있어도 한동안 아무도 피해를 못 줬으면(언덕에 화살이 막히는 등) 서지 않고 적 몸에 닿을 때까지 다가가며 친다
                bool press = BattleManager.Stalled && !blockedByEnemy;
                velocity.x = NoPress(_climbing ? ClimbVx(velocity.y) : StairAdvance(settle || press ? Forward * MoveSpeed : 0f, onGroundSurface, frontSupport), allyInFront);
                _rb.linearVelocity = velocity;
                if (_attackCooldown <= 0f)
                {
                    Vector2 toTarget = targetPoint - _rb.position;
                    Vector2 hitDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : new Vector2(Forward, 0f);
                    // 화염 · 휩쓸기는 늘 앞으로만 나간다 — 뒤돌지 않는다
                    if (_definition.AttackType != AttackType.Flame && _definition.AttackType != AttackType.Sweep)
                        LookToward(toTarget.x);
                    _attackScale = FirstAttackScale();
                    // 돌격 나팔 — 첫 공격이 세졌다: 노린 자리에 빨간 섬광(스탯 색)
                    if (_attackScale > 1f && FxDirector.Instance != null)
                        FxDirector.Instance.HitSpark(targetPoint, hitDirection, BuffLinkFx.ColorOf(BuffKind.Stat));
                    if (_definition.AttackType == AttackType.Flame)
                    {
                        BreatheFire();
                    }
                    else if (_definition.AttackType == AttackType.Sweep)
                    {
                        Sweep();
                    }
                    else if (_definition.AttackType == AttackType.Ranged)
                    {
                        FireProjectile(target, targetPoint);
                    }
                    else
                    {
                        // 달려와서 치는 첫 타격은 돌격 — 피해·밀치기에 배율
                        bool charge = _runTime >= _definition.ChargeReadySeconds;
                        float damage = AttackDamage * (charge ? _definition.ChargeDamageMultiplier : 1f) * DamageScale;
                        float push = _definition.PushPower * (charge ? _definition.ChargePushMultiplier : 1f);
                        Strike(target, damage, hitDirection, push);
                    }
                    _attackScale = 1f;
                    foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                        if (e.Effect.AppliesFor(_team, _definition))
                            e.Effect.OnAttack(this, target, e.Stacks);
                    _runTime = 0f;
                    if (_definition.AttackType != AttackType.Flame)   // 화염은 입(무기 자리)이 움직이면 안 된다
                        StartThrust(hitDirection);
                    _attackCooldown = NextCooldown(_attackCooldown, IntervalAfter(target));
                }
                return;
            }

            // 싸울 상대가 없다 — 다시 앞을 본다
            _lookBack = false;
            _runTime = IsStopped ? 0f : _runTime + Time.fixedDeltaTime;

            // 뒤돌아 싸움(사용자 10/16 — 백도어 줄이기) — 「뒤돌아 싸움」 병종은 등 뒤 가까이 적 유닛이 남아 있으면 돌아서서 그쪽으로 걸어간다.
            // 사거리에 들면 위의 싸움 분기가 치고, 대상이 사라지거나 멀어지면 다시 앞으로. 땅을 딛고 선 유닛만(산 위에서는 머리 넘기 규칙이 우선)
            // 이미 돌아서 있으면 발밑이 한 스텝 땅이 아니어도(작은 턱 · 아군 어깨) 이어 간다 — 안 그러면 그 스텝만 앞을 보고 앞으로 걸어 떨린다
            if (!_climbing && (onGroundSurface || !float.IsNaN(_turnBackX)))
            {
                if (_turnBackScanTimer <= 0f)
                {
                    // 들어가는 거리와 나오는 거리가 다르다(히스테리시스) — 돌아서 있으면 칸 + 여유 안까지 계속, 그만두면 TargetHold 동안은 다시 안 돌아선다
                    bool turning = !float.IsNaN(_turnBackX);
                    float range = BattleManager.TurnBackRange + (turning ? BattleManager.TurnBackExit : 0f);
                    float x = turning || Time.fixedTime >= _turnBackRetryAt ? BehindEnemyX(range) : float.NaN;
                    if (turning && float.IsNaN(x))
                        _turnBackRetryAt = Time.fixedTime + BattleManager.TargetHold;
                    _turnBackX = x;
                    _turnBackScanTimer = TargetScanInterval;
                }
                if (!float.IsNaN(_turnBackX))
                {
                    // 뒤로 걷는 동안은 늘 뒤를 본다 — 적이 거의 같은 x면 LookToward의 무감 구간에 걸려 앞을 본 채 뒤로 걷던 것을 막는다
                    LookToward(-Forward);
                    // 스스로 뒤로 걷는 유일한 경우 — 뒤로 못 가는 래칫(HoldBack)을 이 스텝만 끈다
                    _holdBack = false;
                    velocity.x = -Forward * MoveSpeed;
                    _rb.linearVelocity = velocity;
                    return;
                }
            }

            // 기지 위는 한 층만 — 기지 위 유닛 머리로 걸어 올라가지도 않는다
            WalkMarker.Begin();
            if (_walkScanTimer <= 0f)
            {
                _aheadOnBaseUnit = IsAheadOnBaseUnit();
                _walkScanTimer = TargetScanInterval;
            }
            // 기다림이 풀린 동안(Released)에는 기지 위 유닛 뒤 · 머리 위 아군 때문에 서지 않는다 — 기다려 줄 상대가 더 오지 않는다
            velocity.x = (_aheadOnBaseUnit || holdForAbove) && !Released ? 0f : Forward * MoveSpeed;
            // 앞면 유닛(앞에 맞닿은 아군이 없다)은 계단 상한에 묶이지 않고 걷는다 — 사거리에 적이 없는 맨 앞 유닛은 적과 맞붙을 때까지 전진한다(사용자 10/10).
            // 앞면이 나가면 그 뒤 유닛의 앞이 비어 차례로 따라간다. 사거리에 적이 들면 위의 싸움 분기가 다시 계단 자리에 세운다
            bool frontFace = !allyAhead;
            velocity.x = NoPress(_climbing ? ClimbVx(velocity.y) : StairAdvance(velocity.x, onGroundSurface || frontFace, frontSupport), allyInFront);
            WalkMarker.End();
            // 못 올라탈 땐 뛰지 않고 서 있어야 뒤 유닛의 발판이 된다 — 계속 뛰면 계단(산)이 안 생긴다
            // 피라미드의 중요 자리(맨 앞줄·받침)는 뛰지 않고, 그 밖은 적극적으로 올라탄다(사용자 10/8).
            // 등반 점프마다 쉬는 시간(BattleConfig) — 너무 자주 뛰어 산이 쌓이기 전에 넘어가지 않게
            if (grounded && !keySpot && _climbRestTimer <= 0f)
            {
                Unit top = FindClimbTarget();
                if (top != null && HeadClear())
                    StartClimb(top, ref velocity);
            }
            _rb.linearVelocity = velocity;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker)
        {
            if (!IsAlive)
                return;
            // 무적 — 피해도 밀림도 없다. 막은 피해를 돌려주는 효과(공주 루니카 신화)만 여기서 돈다
            if (IsInvulnerable)
            {
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition) && e.Effect.ReflectsBlockedDamage(attacker))
                    {
                        DamageReflect.Back(attacker, amount, hitDirection, this);
                        break;
                    }
                return;
            }

            // 받는 피해 배율(방패 대열 · 뼈 갑옷) — 대상은 효과가 가린다
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                amount *= e.Effect.DamageTakenFactor(this);
            DamageMeter.Credit(attacker, _team, UnityEngine.Mathf.Min(amount, UnityEngine.Mathf.Max(0f, _hp)));   // 실제로 깎인 몫만(초과 피해 제외)
            _hp -= amount;
            if (amount > 0f)
            {
                BattleManager.NoteProgress();
                // 가시 갑옷 — 받은 피해의 일부를 공격한 쪽에 돌려준다
                float reflect = 0f;
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition))
                        reflect += e.Effect.DamageReflectRatio(_team);
                if (reflect > 0f)
                    DamageReflect.Back(attacker, amount * reflect, hitDirection, this);
                // 때린 유닛의 진영에 「피해를 줬다」를 알린다(원시 의식 제단 — 준 피해만큼 회복). 아군을 때린 것도 준 피해다
                if (attacker is Unit dealer && dealer.IsAlive)
                    foreach (TeamEffects.Entry e in TeamEffects.For(dealer._team))
                        if (e.Effect.AppliesFor(dealer._team, dealer._definition))
                            e.Effect.OnDealtDamage(dealer, this, amount);
            }
            DamageNumbers.Damage(this, _team, new Vector2(_rb.position.x, Top), amount);
            // 맞은 면(공격이 들어온 쪽)에서 공격 방향으로 파편이 튄다
            if (FxDirector.Instance != null)
                FxDirector.Instance.HitSpark(_rb.position - hitDirection * SparkSurfaceOffset, hitDirection, SparkColor);
            if (!IsAlive)
            {
                // 신정 — 처음 죽을 때 죽는 대신 최대 체력의 일부를 회복한다(유닛마다 한 번 · 처치가 아니다)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition) && e.Effect.FirstDeathHealRatio(_team) > 0f && UseOnce(e.Effect))
                    {
                        _hp = MaxHp * e.Effect.FirstDeathHealRatio(_team);
                        PillarFx.Rise(new Vector2(_rb.position.x, Feet), Top - Feet);   // 죽을 뻔한 순간 다시 일어난다 — 기둥
                        return;
                    }
                // 충정의 맹세 — 처음 죽을 때 대신 N초 무적, 그 뒤 쓰러진다(처치자는 그때 정해진 대로)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.FirstDeathDelay > 0f && e.Effect.AppliesFor(_team, _definition) && UseOnce(e.Effect))
                    {
                        _hp = DoomedHp;
                        _doomKiller = attacker;
                        // 유예는 실제로 받은 무적과 같은 길이다 — 지속시간 보정(충정의 맹세 신화)이 붙으면 그만큼 더 버틴다
                        _doomTimer = GrantInvulnerable(e.Effect.FirstDeathDelay);
                        BuffLinks.EffectToPoint(_team, e.Effect, _rb.position, BuffKind.Invulnerable);
                        return;
                    }
                Perish(attacker);
                return;
            }

            _squashTime = 0f;
            _flashTimer = FlashDuration;

            // 피격 효과(돌격 반사 등) — 하나라도 true면 이번 피격에 밀리지 않는다
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition) && e.Effect.OnHit(this, attacker, hitDirection, push, e.Stacks))
                    return;

            // 냥코식 큰 넉백 — 생명당 한 번, 맞은 방향과 상관없이 후방으로 튕겨 오른다
            if (!_bigKnockbackUsed && _hp <= MaxHp * _definition.BigKnockbackAt)
            {
                _bigKnockbackUsed = true;
                _knockbackTimer = BigKnockbackDuration;
                float speed = _definition.BigKnockbackSpeed;
                _rb.linearVelocity = new Vector2(-Forward * speed, speed * BigKnockbackHopRatio);
                _squashVertical = false;
                if (FxDirector.Instance != null)
                {
                    FxDirector.Instance.HitStop(BigKnockbackHitStop);
                    FxDirector.Instance.Shake(BigKnockbackShake);
                }
                return;
            }

            // 맞은 방향으로 밀리고, 맞은 축으로 찌그러진다 (위에서 맞으면 납작, 옆에서 맞으면 홀쭉).
            // 세게 맞을수록 오래 조종 불능 — 짧으면 다음 스텝에 걷기가 속도를 덮어써 날아가다 끊긴다
            _squashVertical = Mathf.Abs(hitDirection.y) > Mathf.Abs(hitDirection.x);
            // 밀치는 힘이 0인 공격(화염)은 밀림·경직 없음 — 연속으로 맞아도 반격할 수 있어야 한다
            if (push <= 0f)
                return;
            _knockbackTimer = KnockbackDuration * Mathf.Max(1f, push);
            _rb.linearVelocity = hitDirection * (_definition.HitKnockback * push);
        }

        private void Update()
        {
            if (_invulnerableRing != null && _invulnerableRing.enabled != (IsAlive && IsInvulnerable))
                _invulnerableRing.enabled = IsAlive && IsInvulnerable;
            // 고도로 강해진 유닛(미노타우로스) — 머리 위 배율 · 붉은 입자(Fx/AltitudeMark, 사용자 10/22 · 10/23). 고도 0이면 없다
            if (IsAlive && AltitudeAttackScale > 1.05f)
                AltitudeMark.Watch(this);
            if (_attackAura != null && (_attackAura.enabled || _attackAuraTimer > 0f))
            {
                _attackAuraTimer -= Time.deltaTime;
                float alpha = IsAlive && _attackAuraTimer > 0f ? AttackAuraColor.a * _attackAuraTimer / AttackAuraDuration : 0f;
                _attackAura.enabled = alpha > 0f;
                if (alpha > 0f)
                    _attackAura.color = new Color(AttackAuraColor.r, AttackAuraColor.g, AttackAuraColor.b, alpha);
            }
            if (!IsAlive)
            {
                // 그림은 숨겨져 있다(Die) — 시간이 되면 풀로 돌아간다
                _despawnTimer -= Time.deltaTime;
                if (_despawnTimer <= 0f)
                    gameObject.SetActive(false);
                return;
            }

            // 맞은 축으로 눌렸다가 출렁이며 원래 모양으로. 발바닥 높이는 고정
            float t = Mathf.Min(_squashTime / SquashDuration, 1f);
            float wobble = SquashAmount * (1f - t) * Mathf.Cos(t * Mathf.PI * 3f);
            float scaleX = _squashVertical ? 1f + wobble : 1f - wobble;
            float scaleY = _squashVertical ? 1f - wobble : 1f + wobble;
            // 고도로 커 보이는 그림(그림만 — 충돌 박스 · 키는 그대로, 발은 그 자리)
            float look = AltitudeLook;
            _visual.localScale = new Vector3(scaleX * look * _visualScale.x * Look, scaleY * look * _visualScale.y, 1f);
            _visual.localPosition = new Vector3(0f, (scaleY * look - 1f) * _halfHeight, 0f);
            _squashTime += Time.deltaTime;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                for (int i = 0; i < _bodyRenderers.Length; i++)
                    _bodyRenderers[i].color = _flashTimer > 0f ? SparkColor : _bodyColors[i]; // 몸이 흰색이라 흰 번쩍임은 안 보인다
            }

            UpdateThrust();
        }

        // ── 뒤돌기(사용자 10/12) — 뒤쪽의 대상을 치거나 회복할 때, 건축가가 일을 마치고 성으로 돌아갈 때 그림이 그쪽을 본다. 보이는 것만이다:
        // 사거리 · 대상 고르기 · 판정은 그대로다. 투사체는 뒤돈 그림의 무기 자리(WeaponPoint)에서 떠난다. 밀려나는 것(넉백)은 뒤돌지 않는다.
        // 진영의 방향(뿌리의 좌우 뒤집기) 위에 그림(_visual)만 한 번 더 뒤집는다 — 무기 · 말 같은 겹친 부품도 같이 뒤집힌다
        // 대상이 거의 바로 위 · 아래일 때 좌우로 떨리지 않게 두는 폭(칸) — 이 안이면 보던 쪽을 그대로 본다
        private const float LookDeadZone = 0.25f;
        private bool _lookBack;

        private float Look => _lookBack || _buildPhase == BuildPhase.Returning ? -1f : 1f;

        // dx = 대상까지의 가로 거리(월드). 다음에 칠 때까지 그쪽을 보고, 싸울 상대가 없어지면 다시 앞을 본다
        private void LookToward(float dx)
        {
            if (Mathf.Abs(dx) < LookDeadZone)
                return;
            _lookBack = dx * Forward < 0f;
            // 바로 뒤집는다 — 이어지는 찌르기(StartThrust)가 뒤집힌 몸 기준으로 방향을 잡게
            Vector3 scale = _visual.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(_visualScale.x) * Look;
            _visual.localScale = scale;
        }

        // 투사체가 떠나는 자리 = 지금 그림의 무기 자리 — 뒤돌아 있으면 뒤쪽 무기 자리에서 나간다(사용자 10/13)
        private Vector2 WeaponPoint => _weapon.position;

        private void StartThrust(Vector2 worldDirection)
        {
            // 좌우 반전된 몸 기준으로 바꿔야 칼끝이 실제 타겟을 향한다
            Vector2 local = _visual.InverseTransformDirection(worldDirection);
            _thrustDirection = local.sqrMagnitude > 0.0001f ? local.normalized : Vector2.right;
            _thrustTime = 0f;
        }

        private void UpdateThrust()
        {
            if (_thrustTime >= ThrustDuration)
                return;

            _thrustTime += Time.deltaTime;
            if (_thrustTime >= ThrustDuration)
            {
                _weapon.localPosition = _weaponRestPosition;
                _weapon.localRotation = Quaternion.identity;
                return;
            }

            float lunge = Mathf.Sin(_thrustTime / ThrustDuration * Mathf.PI) * ThrustDistance;
            // 찌르기 기준점 = 무기를 든 손(쉬는 위치에서 팔 길이만큼 몸 쪽) — 기사는 말 위, 보통은 몸 가운데
            Vector2 anchor = (Vector2)_weaponRestPosition - new Vector2(WeaponReach, 0f);
            _weapon.localPosition = anchor + _thrustDirection * (WeaponReach + lunge);
            float angle = Mathf.Atan2(_thrustDirection.y, _thrustDirection.x) * Mathf.Rad2Deg;
            _weapon.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private bool FindTarget(out IDamageable target, out Vector2 targetPoint)
        {
            // 사거리 안에서 가장 가까운 적 — 사거리가 긴 원거리딜이 먼 적부터 쏘지 않게.
            // 우선 공격 병종(꼭두각시 왕: 지원병)이 사거리 안에 있으면 그중 가장 가까운 적
            Physics2D.OverlapCircle(_rb.position, AttackRange, _enemyFilter, _overlaps);
            UnitCategory preferred = PreferredTarget();
            target = null;
            targetPoint = default;
            float best = float.MaxValue;
            bool bestPreferred = false;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive)
                    continue;
                if (IsUnderfoot(damageable))
                    continue;

                bool isPreferred = preferred != null && damageable is Unit u && UnitAliases.Is(u._team, u._definition, preferred);
                if (bestPreferred && !isPreferred)
                    continue;
                Vector2 point = col.ClosestPoint(_rb.position);
                float distance = (point - _rb.position).sqrMagnitude;
                if (distance < best || (isPreferred && !bestPreferred))
                {
                    best = distance;
                    bestPreferred = isPreferred;
                    target = damageable;
                    targetPoint = point;
                    _foundCollider = col;
                }
            }
            return target != null;
        }

        // 뒤돌아 싸움 — 등 뒤 높이 TurnBackHeight 칸 · 가로 range 칸(돌아서기 = BattleConfig 「뒤돌아 싸움」 칸, 그만두기 = 그 칸 + 여유) 안의 가장 가까운 살아 있는 적 유닛의 x. 없거나 내 병종이 안 돌아서면 NaN.
        // 발밑 적(머리 넘기)은 세지 않는다
        private const float TurnBackHeight = 2f;

        private float BehindEnemyX(float range)
        {
            if (range <= 0f || _definition.Category == null || !_definition.Category.TurnBack)
                return float.NaN;
            Physics2D.OverlapBox(_rb.position - new Vector2(Forward * range * 0.5f, 0f), new Vector2(range, TurnBackHeight), 0f, _enemyFilter, _overlaps);
            float best = float.MaxValue, x = float.NaN;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit enemy) || enemy._team == _team || !enemy.IsAlive || IsUnderfoot(enemy))
                    continue;
                float behind = (_rb.position.x - enemy._rb.position.x) * Forward;
                if (behind > 0f && behind < best)
                {
                    best = behind;
                    x = enemy._rb.position.x;
                }
            }
            if (float.IsNaN(x))
                return x;
            // 그 적과 나 사이에 앞을 보고 선 내 아군이 있으면 돌아서지 않는다 — 앞쪽과 싸우는 아군 벽 너머의 적을 향해 걷다 막혀
            // 경계에서 앞뒤로 떨던 것(사용자 10/20)을 막는다. 사이의 아군이 이미 그쪽으로 돌아서 있으면 따라 돌아선다(뒤에서 함께 막는다)
            Physics2D.OverlapBox(new Vector2((_rb.position.x + x) * 0.5f, _rb.position.y), new Vector2(best, TurnBackHeight), 0f, _allyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
                if (col.TryGetComponent(out Unit ally) && ally != this && ally._team == _team && ally.IsAlive && !ally._lookBack
                    && (_rb.position.x - ally._rb.position.x) * Forward > TurnBackAllyGap)
                    return float.NaN;
            return x;
        }

        // 사이에 선 아군으로 치는 거리 — 나와 맞닿아 겹친 아군은 빼고
        private const float TurnBackAllyGap = 0.5f;

        private UnitCategory PreferredTarget()
        {
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.PreferredTargetCategory != null && e.Effect.AppliesFor(_team, _definition))
                    return e.Effect.PreferredTargetCategory;
            return null;
        }

        // 산이 넘쳐흐르게 — 적 머리 위에 선 근접은 발밑 적을 치느라 멈추지 않고 머리를 밟고 계속 걷는다.
        // 같은 높이의 적을 만나거나 적 줄 끝에서 떨어지면 다시 싸운다. 원거리·보스·기지·탑은 그대로.
        // 단, 내 진영 산이 적 산보다 높을 때만 넘는다(사용자 10/8) — 그 전엔 발밑 적을 쳐서 그 자리에 서고, 뒤 유닛이 그 위로 쌓인다
        private bool IsUnderfoot(IDamageable damageable)
            => _definition.AttackType == AttackType.Melee && damageable is Unit unit && unit.Top <= Feet + StandOnMargin
               && PileNow[(int)_team] > PileNow[1 - (int)_team] + PileLeadToCross;

        // 진영별 전선 산 높이 — 선두(가장 앞 유닛)에서 PileFrontRange 안에 무언가를 딛고 선 유닛의 가장 높은 머리(땅 기준).
        // 물리 검사 없이 유닛마다 제 값을 보고하고, 물리 스텝이 바뀌면 굳힌다(한 스텝 늦은 값). 선두도 같은 방식
        private const float PileFrontRange = 6f;
        private const float PileLeadToCross = 0.5f;
        private static readonly float[] FrontNow = { float.NegativeInfinity, float.NegativeInfinity };
        private static readonly float[] FrontNext = { float.NegativeInfinity, float.NegativeInfinity };
        private static readonly float[] PileNow = { 0f, 0f };
        private static readonly float[] PileNext = { 0f, 0f };
        private static float _pileStep = -1f;
        // 진영별 땅 선두 — 땅을 딛고 선 유닛 중 가장 앞(전방 좌표). 계단 피라미드의 기준선
        private static readonly float[] LineNow = { float.NegativeInfinity, float.NegativeInfinity };
        private static readonly float[] LineNext = { float.NegativeInfinity, float.NegativeInfinity };
        // 계단 피라미드(사용자 10/9) — 층마다 반 칸씩 물러난다
        private const float StairStep = 0.5f;

        // 계단은 막기만 한다 — 유닛이 스스로 뒤로 걷지 않는다(사용자 10/9 "갑자기 뒤로 가는 거 어색"). 앞으로 가는 속도만 줄인다.
        // 땅을 딛지 않은 유닛(위층·공중)은 두 상한 중 가까운 쪽 앞에서 멈춘다(닿기 전 StairBrakeTime 동안 서서히 줄인다):
        // ① 전선 — 땅 선두에서 층 × 반 칸 뒤까지. 위층이 적 산 위로 넘어가지 않는다
        // ② 받침 — 발밑 가장 앞 아군보다 반 칸 뒤까지
        // 반 칸 자리에 앉히는 건 등반 점프가 한다(StartClimb — 뛰기 전에 착지 자리를 정하고 가로 속도를 맞춘다).
        // 내 산이 StairHoldLead 넘게 높을 때와 데스매치(기물 정지 뒤)에는 걸지 않는다 — 이긴 쪽이 넘어가 판을 끝낸다(안 풀면 위층이 영영 안 싸워 판이 멈춘다)
        private bool StairActive => !Released && PileNow[(int)_team] <= PileNow[1 - (int)_team] + StairHoldLead;
        // 기다리는 규칙(계단 상한 · 받침 기다리기 · 기지 위 유닛 뒤에서 서기)을 푸는 때 — 데스매치로 생산이 끝난 뒤(더 올 유닛이 없다), 그리고 한동안 피해가 없을 때.
        // 양 진영 같은 판정이라 한쪽만 풀리지 않는다
        private static bool Released => BattleManager.BoardsStopped || BattleManager.Stalled;
        // 내 산이 이만큼(칸) 넘게 높으면 계단을 풀고 넘쳐흐른다. 산 높이는 출렁여서 0.5처럼 작게 잡으면 비슷한 판에서도 수시로 열린다(10/9 측정)
        private const float StairHoldLead = 2f;

        private const float StairBrakeTime = 0.25f;
        // 등반 점프의 가로 속도 상한 — 이동 속도의 이 배(착지 자리가 멀어도 날아가듯 튀지 않게)
        private const float ClimbMaxSpeedRatio = 1.5f;

        private float StairAdvance(float vx, bool onGroundSurface, float frontSupport)
        {
            if (onGroundSurface || vx * Forward <= 0f || !StairActive)
                return vx;
            float ahead = _rb.position.x * Forward;
            float room = float.PositiveInfinity;
            float line = LineNow[(int)_team];
            if (!float.IsNegativeInfinity(line))
            {
                float layer = _stackLayer + Mathf.Max(0f, Feet - _supportFeet);   // 공중이면 마지막 디딘 층 + 그보다 오른 높이
                room = line - StairStep * layer - ahead;
            }
            if (!float.IsNegativeInfinity(frontSupport))
                room = Mathf.Min(room, frontSupport - StairStep - ahead);
            if (float.IsPositiveInfinity(room))
                return vx;
            return Forward * Mathf.Min(vx * Forward, Mathf.Max(room, 0f) / StairBrakeTime);
        }

        // 등반 점프 — 기둥 꼭대기(top) 위의 반 칸 자리를 착지점으로 정하고 뛴다.
        // top 앞에 아군이 붙어 있으면 그 둘 사이, 뒤에 붙어 있으면 그 둘 사이, 둘 다 없으면 top 바로 위. 계단 상한(전선)을 넘지 않고, 지금 자리보다 뒤로는 안 잡는다
        private void StartClimb(Unit top, ref Vector2 velocity)
        {
            float topAhead = top._rb.position.x * Forward;
            float seat = top._allyAhead ? topAhead + StairStep : top._allyBehind ? topAhead - StairStep : topAhead;
            float line = LineNow[(int)_team];
            if (StairActive && !float.IsNegativeInfinity(line))
                seat = Mathf.Min(seat, line - StairStep * (top._stackLayer + 1));
            _climbSeat = Mathf.Max(seat, _rb.position.x * Forward);
            _climbLandFeet = top.Top;
            _climbing = true;
            _climbRestTimer = BattleManager.ClimbRest;
            velocity.y = Mathf.Sqrt(2f * -Physics2D.gravity.y * _rb.gravityScale * JumpHeight);
        }

        // 앞에 맞닿은 아군을 밀어붙이지 않는다 — 그 아군의 실제 속도 + PressSpeed까지만(맞닿음은 유지돼 등반 후보로 남는다).
        // 멈춘 줄에 걸어 들어가 짓누르면 산 속 유닛이 충돌 밀어내기로 위·뒤로 튄다(사용자 10/10 "갑자기 뒤로 점프")
        private const float PressSpeed = 0.3f;
        private float NoPress(float vx, Unit allyInFront)
        {
            if (allyInFront == null || vx * Forward <= 0f)
                return vx;
            return Forward * Mathf.Min(vx * Forward, Mathf.Max(allyInFront._advanceSpeed, 0f) + PressSpeed);
        }

        // 머리 위 한 칸이 비었나 — 막혔는데 뛰면 머리를 부딪쳐 뒤로 튕긴다. 뛰기 직전에만 묻는다(물리 검사, 스텝 캐시)
        private bool HeadClear()
        {
            OnHead(out bool free);
            return free;
        }

        // 등반 중 가로 속도 — 남은 체공 시간 안에 착지 자리에 닿을 만큼(앞으로만). 체공이 거의 끝났으면 서서히 다가간다
        private float ClimbVx(float vy)
        {
            float g = -Physics2D.gravity.y * _rb.gravityScale;
            float disc = vy * vy + 2f * g * (Feet - _climbLandFeet);
            float flight = disc > 0f ? (vy + Mathf.Sqrt(disc)) / g : 0f;
            float distance = _climbSeat - _rb.position.x * Forward;
            if (distance <= 0f)
                return 0f;
            return Forward * Mathf.Min(distance / Mathf.Max(flight, StairBrakeTime), MoveSpeed * ClimbMaxSpeedRatio);
        }
        private float _climbSeat;
        private float _climbLandFeet;

        // 뒤로 못 가는 래칫(사용자 10/10 "뒤로 점프는 아예 하지마") — 넉백(경직)이 아닌 유닛은 물리 스텝이 뒤로 민 만큼 되돌린다.
        // 유닛은 스스로 뒤로 걷지 않으므로(뒤돌아 싸움 — 그 스텝은 끈다) 남는 뒤로 움직임은 전부 충돌(짓눌림 · 받침이 밀림 · 넉백된 유닛에 부딪힘)이다.
        // 물리 스텝 직후(WaitForFixedUpdate)에 고쳐야 밀린 자리가 한 번도 그려지지 않는다. 물리 검사는 없다
        private static readonly WaitForFixedUpdate AfterPhysics = new WaitForFixedUpdate();
        private bool _holdBack;
        private float _holdX;
        private System.Collections.IEnumerator HoldBack()
        {
            while (true)
            {
                yield return AfterPhysics;
                if (!_holdBack || _knockbackTimer > 0f || !IsAlive)
                    continue;   // _holdX = 이번 물리 스텝 전 자리(FixedUpdate가 적는다)
                Vector2 position = _rb.position;
                if ((position.x - _holdX) * Forward < 0f)
                    _rb.position = new Vector2(_holdX, position.y);
                Vector2 velocity = _rb.linearVelocity;
                if (velocity.x * Forward < 0f)
                    _rb.linearVelocity = new Vector2(0f, velocity.y);
            }
        }
        private bool _allyAhead;
        // 쌓인 층 — 땅 0, 유닛 위면 그 유닛 층 + 1(한 스텝 늦은 값) · 그때의 발 높이
        private int _stackLayer;
        private float _supportFeet;
        // 바로 뒤(같은 높이)에 아군이 붙어 있다(한 스텝 늦은 값)
        private bool _allyBehind;

        private void ReportPile(bool standing, bool onGroundSurface)
        {
            if (Time.fixedTime != _pileStep)
            {
                _pileStep = Time.fixedTime;
                for (int i = 0; i < 2; i++)
                {
                    FrontNow[i] = FrontNext[i];
                    FrontNext[i] = float.NegativeInfinity;
                    PileNow[i] = PileNext[i];
                    PileNext[i] = 0f;
                    LineNow[i] = LineNext[i];
                    LineNext[i] = float.NegativeInfinity;
                }
            }
            int team = (int)_team;
            float ahead = _rb.position.x * Forward;
            FrontNext[team] = Mathf.Max(FrontNext[team], ahead);
            if (onGroundSurface)
                LineNext[team] = Mathf.Max(LineNext[team], ahead);
            if (standing && ahead >= FrontNow[team] - PileFrontRange && Ground.Instance != null)
                PileNext[team] = Mathf.Max(PileNext[team], Top - Ground.Instance.HeightAt(_rb.position.x));
        }

        // 적 찾기는 비싸다(사거리 원 안의 콜라이더를 전부 본다 — 산 속 원딜은 수백 개). 그래서 TargetScanInterval마다만 새로 찾고,
        // 그 사이엔 찾아 둔 적이 아직 살아 있고 사거리 안인지만 본다. 못 찾았으면 다음 검색까지 없는 것으로 친다
        private bool FindTargetCached(out IDamageable target, out Vector2 targetPoint)
        {
            using (ScanMarker.Auto())
                return FindTargetCachedCore(out target, out targetPoint);
        }

        // 찾은 적은 TargetHold 동안 다시 고르지 않는다(검색 간격이 돌아와도) — 죽거나 · 발밑으로 들어가거나 · 사거리 밖이면 바로 다시 찾는다.
        // 공격은 지금처럼 사거리 안이면 바로 한다(대상만 붙잡는다)
        private bool FindTargetCachedCore(out IDamageable target, out Vector2 targetPoint)
        {
            if (_targetScanTimer > 0f || (_cachedTarget != null && Time.fixedTime < _targetHoldUntil))
            {
                target = null;
                targetPoint = default;
                if (_cachedTarget == null)
                    return false;
                if (_cachedTarget.IsAlive && _cachedTargetCollider.enabled && !IsUnderfoot(_cachedTarget))
                {
                    targetPoint = _cachedTargetCollider.ClosestPoint(_rb.position);
                    float range = AttackRange;
                    if ((targetPoint - _rb.position).sqrMagnitude <= range * range)
                    {
                        target = _cachedTarget;
                        return true;
                    }
                }
            }

            _targetScanTimer = TargetScanInterval;
            bool found = FindTarget(out target, out targetPoint);
            _cachedTarget = found ? target : null;
            _cachedTargetCollider = found ? _foundCollider : null;
            if (found)
                _targetHoldUntil = Time.fixedTime + BattleManager.TargetHold;
            return found;
        }

        // 원거리 공격 한 번 — target = 노리는 적 · targetPoint = 그 적의 조준점. 첫 발은 바로 나가고, 더 쏘는 발(궁술수련관 신화)은 연발로 이어진다
        public void FireProjectile(IDamageable target, Vector2 targetPoint)
        {
            Shoot(target, targetPoint);
            // 궁술수련관 신화 — 남은 화살을 짧은 간격으로 잇달아 쏜다(FixedUpdate의 Burst). 발마다 다른 적을 노리고, 노릴 적이 모자라면 같은 적에게(사용자 10/13).
            // 간격은 효과의 값이되, 다음 공격 전에 다 나가도록 공격 간격을 발 수로 나눈 것보다 길지 않게
            int extra = 0;
            float interval = float.MaxValue;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.ExtraProjectiles > 0 && e.Effect.AppliesFor(_team, _definition))
                {
                    extra += e.Effect.ExtraProjectiles;
                    interval = Mathf.Min(interval, e.Effect.ExtraProjectileInterval);
                }
            _burstAimed.Clear();
            _burstLeft = extra;
            if (extra > 0)
            {
                _burstAimed.Add(target);
                _burstInterval = Mathf.Min(interval, AttackInterval / (extra + 1));
                _burstTimer = _burstInterval;
            }
        }

        // 화살(돌) 한 발(타워 효과를 함께 받는 유닛이면 대상 하나에 그 수만큼) — 고도 보너스 · 높은 고도였나(기름투하대)는 쏘는 순간 그 적 기준,
        // 관통 · 관통 타격마다의 피해 배율(관통탄 특허국)은 켜진 효과에서 읽는다
        // extra = 처치해서 더 쏜 발(연사 — 노란 꼬리). 여러 발이면 첫 발 뒤로 짧은 시차를 두고 연달아 나간다(Units/Volley — 조준점은 지금 정한 그대로)
        private void Shoot(IDamageable target, Vector2 targetPoint, bool extra = false)
        {
            bool pierce = false;
            float pierceBonus = 0f;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                {
                    pierce |= e.Effect.ProjectilesPierce;
                    pierceBonus += e.Effect.PierceHitDamageBonus;
                }
            // 타워 효과를 함께 받는 유닛이면 타워의 화살 수 변경이 얹힌다(화살 강화기 신화)
            int count = Mathf.Max(1, Mathf.RoundToInt(TowerBook.SharedCompute(_team, _definition, TowerStat.ArrowCount, 1f)));
            float highGround = HighGroundScale(target);
            float damage = AttackDamage * DamageScale * highGround;
            bool above = IsAbove(target);
            for (int i = 0; i < count; i++)
                _volley.Fire(new Volley.Shot
                {
                    Prefab = _definition.Projectile,
                    Target = targetPoint,
                    Damage = damage,
                    Push = _definition.PushPower,
                    Arc = _definition.ProjectileArcHeight,
                    Splash = _definition.ProjectileSplashRadius,
                    Pierce = pierce,
                    PierceBonus = pierceBonus,
                    HighGround = above,
                    Boosted = highGround > 1f,
                    Extra = extra,
                }, i, _team, WeaponPoint, this);
        }

        // 여러 발의 2번째 발부터 — 짧은 시차로 연달아(Units/Volley). 죽으면 남은 발은 버린다(OnEnable에서 비운다)
        private readonly Volley _volley = new Volley();

        // 연발(궁술수련관 신화) — 남은 발 수 · 다음 발까지 · 발 사이 간격 · 이번 연발에서 이미 노린 적
        private int _burstLeft;
        private float _burstTimer;
        private float _burstInterval;
        private readonly List<IDamageable> _burstAimed = new List<IDamageable>();

        // 연발의 다음 발 — 쏘는 순간 사거리 안에서 다시 고른다(그사이 죽거나 벗어난 적은 빠진다). 이번 연발에서 아직 안 노린 적 중 조준 순서가 가장 앞선 적,
        // 다 노렸으면 조준 순서가 가장 앞선 적. 사거리 안에 적이 하나도 없으면 남은 발은 버린다
        private void Burst()
        {
            _burstTimer -= Time.fixedDeltaTime;
            if (_burstTimer > 0f)
                return;
            _burstTimer += _burstInterval;
            Physics2D.OverlapCircle(_rb.position, AttackRange, _enemyFilter, _overlaps);
            UnitCategory preferred = PreferredTarget();
            IDamageable pick = null;
            Vector2 pickPoint = default;
            float best = float.MaxValue;
            bool bestPreferred = false, bestFresh = false;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive || IsUnderfoot(damageable))
                    continue;
                bool fresh = !_burstAimed.Contains(damageable);
                bool isPreferred = preferred != null && damageable is Unit u && UnitAliases.Is(u._team, u._definition, preferred);
                Vector2 point = col.ClosestPoint(_rb.position);
                float distance = (point - _rb.position).sqrMagnitude;
                // 아직 안 노린 적이 먼저, 그 안에서는 FindTarget과 같은 순서(우선 병종 먼저 · 가까운 순)
                bool better = pick == null
                              || (fresh != bestFresh ? fresh : isPreferred != bestPreferred ? isPreferred : distance < best);
                if (!better)
                    continue;
                pick = damageable;
                pickPoint = point;
                best = distance;
                bestPreferred = isPreferred;
                bestFresh = fresh;
            }
            if (pick == null)
            {
                _burstLeft = 0;
                return;
            }
            _burstAimed.Add(pick);
            _burstLeft--;
            Shoot(pick, pickPoint);
        }

        // 기름투하대 — 내 머리가 상대 유닛 머리보다 높으면 주는 피해 +비율(언덕·점프·크기·타고난 키 모두 고도). 상대가 본진이면 본진이 늘 높은 고도라 해당 없음
        private const float HighGroundMargin = 0.25f;

        private bool IsAbove(IDamageable target) => target is Unit enemy && Top > enemy.Top + HighGroundMargin;

        private float HighGroundScale(IDamageable target)
        {
            if (!IsAbove(target))
                return 1f;
            float bonus = 0f;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, _definition))
                    bonus += e.Effect.HighGroundDamageBonus(e.Level);
            return 1f + bonus;
        }

        // 직접 때린다(근접 · 화염 · 휩쓸기 · 착지 충격) — 고도 보너스를 얹고, 높은 고도에서 때렸으면 켜진 효과에 알린다(기름투하대 신화)
        private void Strike(IDamageable target, float damage, Vector2 direction, float push)
        {
            bool above = IsAbove(target);
            float highGround = HighGroundScale(target);
            damage *= highGround;
            target.TakeDamage(damage, direction, push, this);
            if (highGround > 1f)
                HighGroundSpark(target, direction);
            if (above)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition))
                        e.Effect.OnHighGroundHit((Unit)target, damage, this);
        }

        // 불탄다(기름투하대 신화) — seconds초 동안 1초마다 damagePerSecond 피해. 다시 붙으면 시간은 새로, 피해는 큰 쪽.
        // 처치자는 불을 붙인 쪽이다(유닛이면 그 생애일 때만)
        public void Ignite(float seconds, float damagePerSecond, IDamageable attacker)
        {
            if (!IsAlive)
                return;
            if (_burnTicksLeft <= 0)
                _burnTick = BurnInterval;
            _burnTicksLeft = Mathf.RoundToInt(seconds / BurnInterval);
            _burnDamage = Mathf.Max(_burnDamage, damagePerSecond);
            _burnAttacker = attacker;
            _burnAttackerLife = attacker is Unit unit ? unit.Life : 0;
            StatusDots.Watch(this);
        }

        private const float BurnInterval = 1f;
        private int _burnTicksLeft;
        private float _burnTick;
        private float _burnDamage;
        private IDamageable _burnAttacker;
        private int _burnAttackerLife;

        public void ThrowHeal(Unit patient)
        {
            Projectile orb = Pooled.Get(_definition.Projectile, WeaponPoint, Quaternion.identity);
            // 회복량 = 공격력(버프·영구 강화·공격력 가속 포함 — 공격 피해와 같은 식, 사용자 10/10)
            orb.LaunchHeal(this, AttackDamage * DamageScale, patient._rb.position, _definition.ProjectileArcHeight);
        }

        // 사거리 안에서 체력 비율이 가장 낮은 다친 아군(자기·exclude 제외)
        public Unit FindHealTarget(List<Unit> exclude)
        {
            Physics2D.OverlapCircle(_rb.position, _definition.AttackRange, _allyFilter, _overlaps);
            Unit best = null;
            float bestRatio = 1f;
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit ally) || ally == this || (exclude != null && exclude.Contains(ally)) || ally._team != _team || !ally.IsAlive)
                    continue;

                float ratio = ally._hp / ally.MaxHp;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = ally;
                }
            }
            return best;
        }

        // 화염방사 — 입(무기 자리)에서 앞으로 뻗은 띠 안의 적 전부에게 피해
        private void BreatheFire()
        {
            Vector2 mouth = _weapon.position;
            float length = _definition.AttackRange;
            Vector2 center = mouth + new Vector2(Forward * length * 0.5f, 0f);
            Physics2D.OverlapBox(center, new Vector2(length, _definition.FlameThickness), 0f, _enemyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (col.TryGetComponent(out IDamageable damageable) && damageable.Team != _team && damageable.IsAlive)
                    Strike(damageable, AttackDamage * DamageScale, new Vector2(Forward, 0f), _definition.PushPower);
            }
            if (FxDirector.Instance != null)
                FxDirector.Instance.Flame(mouth, Forward, length);
        }

        // 휩쓸기 — 사거리 안, 몸 중심보다 앞쪽(가장 가까운 점 기준)에 있는 적 전부(탑·기지 포함)
        private void Sweep()
        {
            Physics2D.OverlapCircle(_rb.position, AttackRange, _enemyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out IDamageable damageable) || damageable.Team == _team || !damageable.IsAlive)
                    continue;
                Vector2 point = col.ClosestPoint(_rb.position);
                if ((point.x - _rb.position.x) * Forward < 0f)
                    continue;
                Vector2 toTarget = point - _rb.position;
                Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : new Vector2(Forward, 0f);
                Strike(damageable, AttackDamage * DamageScale, direction, _definition.PushPower);
            }
        }

        // 점프 착지 충격 — 발밑 반경 안의 적 유닛(탑·기지 제외)에게 피해를 주고 위로 띄운다
        private void Slam()
        {
            Vector2 feet = new Vector2(_rb.position.x, Feet);
            Physics2D.OverlapCircle(feet, _definition.SlamRadius, _enemyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
            {
                if (!col.TryGetComponent(out Unit enemy) || enemy._team == _team || !enemy.IsAlive)
                    continue;
                Strike(enemy, _definition.SlamDamage * DamageScale, Vector2.up, 0f);
                enemy.Shove(new Vector2(0f, JumpSpeed(_definition.SlamLiftHeight, enemy._rb)), _definition.SlamStun);
            }
        }

        // 이 높이까지 솟는 위쪽 속도
        private static float JumpSpeed(float height, Rigidbody2D body)
        {
            return Mathf.Sqrt(2f * -Physics2D.gravity.y * body.gravityScale * height);
        }

        // 피해 없이 밀어낸다(보스 등장 충격파 등). 그동안 조종 불능
        public void Shove(Vector2 velocity, float stunSeconds)
        {
            if (!IsAlive)
                return;
            _knockbackTimer = stunSeconds;
            _rb.linearVelocity = velocity;
        }

        // 돌려주는 값 = 실제로 찬 양(가득 차 있었으면 0)
        public float Heal(float amount)
        {
            if (!IsAlive)
                return 0f;
            // 최대 체력을 먼저 읽는다 — 밀린 스탯 갱신(RefreshStats)이 체력을 비율로 올린 몫을 회복량으로 세지 않게(가득 찬 유닛은 회복량 0)
            float maxHp = MaxHp;
            float before = _hp;
            _hp = Mathf.Min(_hp + amount, maxHp);
            float healed = _hp - before;
            // 회복 숫자 · 초록 십자(출처가 있으면 회복 오브가 닿을 때 — Fx/HealFx)
            HealFx.Received(this, new Vector2(_rb.position.x, Top), healed, maxHp);
            // 실제로 찬 만큼이 있을 때만 — 가득 찬 유닛은 회복받은 게 아니다(순례지)
            if (healed > 0f)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition))
                        e.Effect.OnHealReceived(this, healed, e.Stacks);
            return healed;
        }

        // 크기 — 1 = 프리팹 그대로. 기준 크기의 amount만큼 더한다(+10% = 0.1). 발은 그 자리, 위로 커진다
        public float Size { get; private set; } = 1f;

        // 상한(BattleConfig — 기본 3배)을 넘는 성장은 버린다
        public void Grow(float amount)
        {
            float before = Size;
            SetSize(Mathf.Min(Size + amount, BattleManager.MaxUnitSize));
            // 크기가 느는 길은 여기 하나다 — 실제로 커졌을 때만 알린다(상한에 막힌 성장은 커진 것이 아니다)
            if (Size > before)
                foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                    if (e.Effect.AppliesFor(_team, _definition))
                        e.Effect.OnGrew(this, e.Stacks);
        }

        private void SetSize(float size)
        {
            float oldHalf = _halfHeight;
            Size = size;
            _body.size = _baseBodySize * size;
            _halfHeight = _body.size.y * 0.5f;
            _visualScale = _baseVisualScale * size;
            if (IsAlive)
                _rb.position += Vector2.up * (_halfHeight - oldHalf);
        }

        // 지금 체력의 비율만큼 잃는다 — 피해가 아니다(죽지 않는다, 승천의 전당)
        public void LoseHpRatio(float ratio)
        {
            if (IsAlive)
                _hp = Mathf.Max(DoomedHp, _hp * (1f - ratio));
        }

        // 최대 체력의 비율만큼 잃는다 — 피해가 아니다(죽지 않는다, 종탑 신화)
        public void LoseMaxHpRatio(float ratio)
        {
            if (IsAlive)
                _hp = Mathf.Max(DoomedHp, _hp - MaxHp * ratio);
        }

        // 사거리 안 가장 가까운 적 유닛 주변 반경의 적 전부에게 피해(교황청 사제). 사거리 안에 적이 없으면 false
        public bool AreaAttackNearest(float radius, float damage)
        {
            Physics2D.OverlapCircle(_rb.position, AttackRange, _enemyFilter, _overlaps);
            Unit nearest = null;
            float best = float.MaxValue;
            foreach (Collider2D col in _overlaps)
                if (col.TryGetComponent(out Unit enemy) && enemy._team != _team && enemy.IsAlive)
                {
                    float d = (enemy._rb.position - _rb.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = enemy;
                    }
                }
            if (nearest == null)
                return false;
            Vector2 center = nearest._rb.position;
            RingFx.Pulse(center, radius, Color.white, this);   // 맞는 범위(교황청 강타)
            Physics2D.OverlapCircle(center, radius, _enemyFilter, _overlaps);
            foreach (Collider2D col in _overlaps)
                if (col.TryGetComponent(out IDamageable target) && target.Team != _team && target.IsAlive)
                {
                    Vector2 away = ((Vector2)col.bounds.center - center).sqrMagnitude > 0.0001f ? ((Vector2)col.bounds.center - center).normalized : new Vector2(Forward, 0f);
                    target.TakeDamage(damage * DamageScale, away, 0f, this);
                }
            return true;
        }

        // 반경 안의 아군(나 포함) 전부 회복(크루세이더 길드) — 회복 오브는 나에게서 각 아군으로(source = 이 회복을 준 효과)
        public void HealAlliesAround(float radius, float amount, UnitEffect source)
        {
            Physics2D.OverlapCircle(_rb.position, radius, _allyFilter, _overlaps);
            var allies = new List<Unit>();
            foreach (Collider2D col in _overlaps)
                if (col.TryGetComponent(out Unit ally) && ally._team == _team && ally.IsAlive)
                    allies.Add(ally);
            foreach (Unit ally in allies)
            {
                ally.Heal(amount);
                if (ally != this)
                    BuffLinks.EffectHeal(_team, source, _rb.position, ally);
            }
        }

        private bool IsAheadOnBaseUnit()
        {
            // 한 칸 앞, 발밑 높이에 기지 위 유닛이 있으면 그 머리로 걸어 들어가게 된다
            Vector2 aheadBelow = new Vector2(_rb.position.x + Forward, Feet - 0.5f);
            Physics2D.OverlapBox(aheadBelow, new Vector2(0.8f, 0.8f), 0f, SolidOnly, CellProbe);
            foreach (Collider2D col in CellProbe)
            {
                if (col.TryGetComponent(out Unit unit) && unit._onBase)
                    return true;
            }
            return false;
        }

        // 나는 other보다 아래층 역할인가(예: 원딜 위의 탱커) — 층 정렬에서 위에 탄 쪽이 내려갈지 정한다
        private bool BelongsBelow(Unit other)
        {
            return _definition.StackRank < other._definition.StackRank;
        }

        private void Die()
        {
            // 물리 제거 — 콜라이더가 꺼지면 위에 서 있던 유닛들이 빈자리로 내려앉는다
            foreach (Collider2D col in GetComponents<Collider2D>())
                col.enabled = false;

            // 기본 자세로 되돌린 모습을 시체가 본떠 날아가고(CorpseFx), 유닛의 그림은 숨긴다
            for (int i = 0; i < _bodyRenderers.Length; i++)
                _bodyRenderers[i].color = _bodyColors[i];
            _weapon.localPosition = _weaponRestPosition;
            _weapon.localRotation = Quaternion.identity;
            _visual.localPosition = Vector3.zero;
            _visual.localScale = _visualScale;

            if (FxDirector.Instance != null)
            {
                FxDirector.Instance.Corpse(_rb.position, _bodyRenderers, _weaponRenderer, -Forward);
                FxDirector.Instance.DeathPuff(_rb.position);
                FxDirector.Instance.Shake(DeathShake);
            }
            _visual.gameObject.SetActive(false);

            _despawnTimer = DeathDestroyDelay;
        }
    }
}

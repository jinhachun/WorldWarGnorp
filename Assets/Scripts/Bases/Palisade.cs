using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 목책(사용자 10/27) — 성 앞에 세우는 울타리. 적 유닛만 막는다: 같은 진영 유닛 · 건축가는 몸이 통과하고(작은 타워 레이어 — Base.SetupLayers),
    // 적 유닛은 부딪혀 멈추고 부숴야 지나간다(유닛은 IDamageable이면 무엇이든 친다). 같은 진영 투사체도 지나간다(Projectile — 같은 진영은 통과).
    // 쏘지 않고 '내 타워' 효과도 받지 않는다(타워가 아니다 — Base.SmallTowers에 안 잡힌다). 체력 · 가시는 세운 기물이 정한다(BuildPalisadeAction — PalisadeAtStartPassive는 10/29부터 에셋에서 안 쓴다).
    // 전투 씬마다 새로 뜨므로 지우는 곳은 없다
    [RequireComponent(typeof(BoxCollider2D))]
    public class Palisade : MonoBehaviour, IDamageable
    {
        private const float GroundSampleStep = 0.25f;
        private static readonly List<Palisade> All = new List<Palisade>();

        // 가장 앞 아군 앞 자리를 찾을 때 뒤(성 쪽)로 물러나며 보는 간격
        private const float SiteSearchStep = 0.25f;

        private Team _team;
        private float _hp;
        private float _maxHp;
        // 받은 피해 중 공격한 쪽에 돌려주는 비율(신화) — 0 = 없음
        private float _reflect;
        // 세운 기물(BuildPalisadeAction이 동시 상한을 기물마다 센다) — 없으면 null
        private OwnedBuilding _source;
        private BoxCollider2D _box;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;
        public float Hp => _hp;
        public float MaxHp => _maxHp;

        private void Awake() => _box = GetComponent<BoxCollider2D>();
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        // team의 성 앞에 세운다 — 성 앞면에서 gap 띄우고, 내 목책이 이미 있으면 가장 앞 목책에서 spacing 앞으로. 밑면은 폭 안에서 가장 낮은 땅. 본진이 없으면 null
        public static Palisade Build(Palisade prefab, Team team, float hp, float reflect, float gap, float spacing)
        {
            Base home = Base.For(team);
            if (home == null || Ground.Instance == null)
                return null;
            float forward = team == Team.Ally ? 1f : -1f;
            var box = prefab.GetComponent<BoxCollider2D>();
            float half = box.size.x * 0.5f;
            float x = (forward > 0f ? home.Bounds.max.x : home.Bounds.min.x) + forward * (gap + half);
            foreach (Palisade other in All)
                if (other._team == team && (other.transform.position.x + forward * spacing - x) * forward > 0f)
                    x = other.transform.position.x + forward * spacing;
            return Spawn(prefab, team, x, hp, reflect, null);
        }

        // 가장 앞 아군 앞에 세운다(BuildPalisadeAction — 사용자 10/29 안 C). 땅을 딛는 내 유닛(날지 않는 · 건축가 아닌) 중 가장 앞 유닛에서 gap 앞, 없으면 성 앞면에서 gap 앞.
        // 그 자리에 적 유닛 · 적 타워 · 내 타워(목책 포함)가 겹치면 겹치지 않을 때까지 성 쪽으로 물러난다 — 적을 목책 안에 가두거나 목책끼리 포개지 않는다(같은 진영 유닛은 몸이 통과하므로 겹쳐도 된다).
        // 성 앞면까지 물러나도 자리가 없으면 null
        public static Palisade BuildAhead(Palisade prefab, Team team, float hp, float reflect, float gap, OwnedBuilding source)
        {
            Base home = Base.For(team);
            if (home == null || Ground.Instance == null)
                return null;
            float forward = team == Team.Ally ? 1f : -1f;
            var box = prefab.GetComponent<BoxCollider2D>();
            float half = box.size.x * 0.5f;
            float start = (forward > 0f ? home.Bounds.max.x : home.Bounds.min.x) + forward * (gap + half);
            float x = start;
            foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == team && !unit.Definition.Flies && unit.Definition.AttackType != AttackType.Build
                    && (unit.Position.x + forward * (gap + half) - x) * forward > 0f)
                    x = unit.Position.x + forward * (gap + half);
            Team foe = team == Team.Ally ? Team.Enemy : Team.Ally;
            int blockers = (1 << LayerMask.NameToLayer(foe == Team.Ally ? "AllyUnit" : "EnemyUnit")) | (1 << Base.TowerLayer(foe)) | (1 << Base.TowerLayer(team));
            for (; (x - start) * forward >= -0.001f; x -= forward * SiteSearchStep)
            {
                float feet = FeetAt(x, half);
                if (Physics2D.OverlapBox(new Vector2(x, feet + box.size.y * 0.5f), box.size, 0f, blockers) == null)
                    return Spawn(prefab, team, x, hp, reflect, source);
            }
            return null;
        }

        public static int CountOf(OwnedBuilding source)
        {
            int count = 0;
            foreach (Palisade palisade in All)
                if (palisade._source == source && palisade.IsAlive)
                    count++;
            return count;
        }

        // source가 세운 목책 중 맨 뒤(내 성 쪽) 것(없으면 null)
        public static Palisade Rearmost(OwnedBuilding source)
        {
            Palisade rear = null;
            foreach (Palisade palisade in All)
                if (palisade._source == source && palisade.IsAlive
                    && (rear == null || (palisade.transform.position.x - rear.transform.position.x) * (palisade._team == Team.Ally ? 1f : -1f) < 0f))
                    rear = palisade;
            return rear;
        }

        // 자리 찾기(BuildAhead)에 안 걸리게 몸을 껐다 켠다 — BuildPalisadeAction이 상한일 때 맨 뒤 것의 자리도 쓸 수 있게
        public void SetSolid(bool solid) => _box.enabled = solid;

        // 부서지지 않고 걷혔다(실측 도구가 부서진 것과 가른다)
        public bool Removed { get; private set; }

        // 상한일 때 맨 뒤 것을 걷는다. 체력을 0으로 — 같은 프레임의 다음 발동이 다시 세지 않고, 이걸 노리던 유닛도 대상을 놓는다(Unit은 IsAlive를 본다)
        public void Remove()
        {
            Removed = true;
            _hp = 0f;
            if (FxDirector.Instance != null)
                FxDirector.Instance.DeathPuff(transform.position);
            _box.enabled = false;
            Destroy(gameObject);
        }

        // 밑면은 폭 안에서 가장 낮은 땅
        private static float FeetAt(float x, float half)
        {
            float feet = float.MaxValue;
            for (float s = x - half; s <= x + half + 0.001f; s += GroundSampleStep)
                feet = Mathf.Min(feet, Ground.Instance.HeightAt(s));
            return feet;
        }

        private static Palisade Spawn(Palisade prefab, Team team, float x, float hp, float reflect, OwnedBuilding source)
        {
            var box = prefab.GetComponent<BoxCollider2D>();
            Vector2 bottom = new Vector2(box.offset.x, box.offset.y - box.size.y * 0.5f);
            Palisade palisade = Instantiate(prefab, new Vector2(x, FeetAt(x, box.size.x * 0.5f)) - bottom, Quaternion.identity);
            palisade._team = team;
            palisade._hp = hp;
            palisade._maxHp = hp;
            palisade._reflect = reflect;
            palisade._source = source;
            palisade.gameObject.layer = Base.TowerLayer(team);
            if (team == Team.Enemy)
                palisade.transform.localScale = Vector3.Scale(palisade.transform.localScale, new Vector3(-1f, 1f, 1f));
            palisade.StartCoroutine(palisade.Rise());
            return palisade;
        }

        // 솟아오르기(사용자 10/29 — Gnorp Apologue 건물처럼, 작은 물건이니 작게): 그림만 땅에서 1px씩 솟고 발치 양옆으로 네모난 먼지 몇 알이 튄다.
        // 몸(충돌체)은 세운 순간부터 다 있다 — 판정은 그대로. 땅 밑을 가리지 않고 그림 윗줄부터 잘라 밑면에 붙여 그린다(잘린 그림은 줄 수마다 한 번만 만든다)
        private const float RiseSeconds = 0.25f;
        private const int DustGrains = 5;
        private static Sprite _riseSource;
        private static Sprite[] _riseFrames;
        // 연출은 게임의 난수(Random)를 쓰지 않는다 — 전투의 난수 순서를 바꾸지 않게
        private static readonly System.Random FxRandom = new System.Random();

        private System.Collections.IEnumerator Rise()
        {
            var body = GetComponent<SpriteRenderer>();
            Sprite full = body.sprite;
            int rows = Mathf.RoundToInt(full.rect.height);
            if (_riseSource != full)
            {
                _riseSource = full;
                _riseFrames = new Sprite[rows];
            }
            // 막 세운 프레임이라 충돌체 bounds가 아직 안 옮겨졌을 수 있다 — 위치에서 센다(Spawn이 밑면을 땅에 맞췄다)
            Vector2 feet = (Vector2)transform.position + new Vector2(0f, _box.offset.y - _box.size.y * 0.5f);
            float half = _box.size.x * 0.5f;
            for (int i = 0; i < DustGrains; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                // 밑면 양 끝 바로 바깥 · 땅 장식(풀) 위에서 시작한다 — 먼지(DustFx)는 목책 · 땅 장식보다 뒤에 그려진다
                DustFx.Billow(feet + new Vector2(side * (half + PixelMesh.Pixel * Rand(0f, 1f)), PixelMesh.Pixel * Rand(2f, 4f)),
                    FxRandom.Next(1, 3), new Vector2(side * Rand(1.5f, 3f), Rand(0.5f, 1.5f)), Rand(0.25f, 0.4f), Rand(0.6f, 1f), 0f);
            }
            for (float t = 0f; t < RiseSeconds; t += Time.deltaTime)
            {
                float k = t / RiseSeconds;
                int shown = Mathf.Clamp(Mathf.CeilToInt(rows * (1f - (1f - k) * (1f - k))), 1, rows);   // 처음에 빠르고 끝에 느리게
                body.sprite = shown == rows ? full : Frame(full, shown);
                yield return null;
            }
            body.sprite = full;
        }

        // full의 윗줄 shown줄만 — 밑면이 원래 그림의 밑면에 오게 피벗을 내린다
        private static Sprite Frame(Sprite full, int shown)
        {
            if (_riseFrames[shown - 1] != null)
                return _riseFrames[shown - 1];
            Rect r = full.rect;
            Vector2 pivot = full.pivot;   // 픽셀 — 원래 그림의 왼쪽 아래 기준
            var cut = Sprite.Create(full.texture, new Rect(r.x, r.yMax - shown, r.width, shown),
                new Vector2(pivot.x / r.width, pivot.y / shown), full.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cut.name = $"{full.name}_rise{shown}";
            return _riseFrames[shown - 1] = cut;
        }

        private static float Rand(float min, float max) => min + (float)FxRandom.NextDouble() * (max - min);

        public void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker)
        {
            if (!IsAlive)
                return;
            DamageMeter.Credit(attacker, _team, Mathf.Min(amount, _hp));   // 실제로 깎인 몫만
            _hp -= amount;
            if (amount > 0f)
                BattleManager.NoteProgress();
            DamageNumbers.Damage(this, _team, new Vector2(_box.bounds.center.x, _box.bounds.max.y), amount, attacker);
            if (_reflect > 0f)
                DamageReflect.Back(attacker, amount * _reflect, hitDirection, this);
            if (IsAlive)
                return;
            if (FxDirector.Instance != null)
                FxDirector.Instance.DeathPuff(_box.bounds.center);
            Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 시체 — 죽은 유닛의 모습(그림들·방향·크기)을 본떠 후방(자기 성 쪽) 비스듬히 위로 날려 보낸다. 빙글 돌며 포물선으로 날아가 화면 아래로 빠지거나 수명이 다하면 사라진다.
    // 연출만이다: Unit·Collider·Rigidbody가 없어 유닛 수·표적 탐색·충돌·판정 어디에도 안 잡힌다. 땅도 그냥 통과한다.
    // 유닛이 한꺼번에 많이 죽어도 버티도록 시작할 때 상한만큼 만들어 두고 돌려 쓴다(꽉 차면 가장 오래된 것부터). 갱신은 여기 Update 하나.
    // 위치는 던진 순간부터의 게임 시간으로 계산한다 — 배속·프레임 간격이 달라도 궤적이 같고, 히트스톱 동안은 같이 멈춘다. 수치는 BattleConfig
    public class CorpseFx : MonoBehaviour
    {
        // 시체 하나의 그림 수 — 기사가 3장(말 · 탄 gnorp · 무기)
        private const int PartsPerCorpse = 4;
        // 살아 있는 유닛(0~2)·탑(-1)·땅 채움(-10)보다 뒤, 뒷배경(-30~)보다 앞 — 땅 밑으로 떨어지면 땅에 가려진다
        private const int SortingOrder = -15;
        // 화면 아래로 이만큼(칸) 빠지면 수명 전이라도 치운다
        private const float ViewMargin = 3f;

        private struct Corpse
        {
            public bool Active;
            public Transform Root;
            public Vector2 Origin;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
            // 지나온 길의 먼지(DustFx) — 크기 배율 · 다음 조각까지 남은 시간
            public float Size;
            public float Puff;
            // 사망 순간 흰 실루엣(J2) — 그동안 사방 1px 크다. 던진 프레임 + 1까지는 끝내지 않는다(적어도 한 프레임은 보이게)
            public bool Popping;
            public int PopFrame;
        }

        // 사망 순간 실루엣 색 — 흰 몸에서도 말 · 영주 색 자리 · 1px 커진 테두리가 번쩍 읽힌다
        private static readonly Color PopColor = Color.white;

        private Corpse[] _corpses;
        private SpriteRenderer[] _parts;
        private Color[] _partColors;
        private int _next;
        private int _activeCount;
        private BattleConfig _config;
        private Camera _camera;
        // 게임의 난수(Random)를 쓰지 않는다 — 연출이 전투의 난수 순서를 바꾸지 않게
        private readonly System.Random _random = new System.Random();

        private void Start()
        {
            _config = BattleManager.Config;
            _camera = Camera.main;
            _corpses = new Corpse[_config.CorpseMax];
            _parts = new SpriteRenderer[_corpses.Length * PartsPerCorpse];
            _partColors = new Color[_parts.Length];
            for (int i = 0; i < _corpses.Length; i++)
            {
                var root = new GameObject("Corpse");
                root.transform.SetParent(transform, false);
                for (int p = 0; p < PartsPerCorpse; p++)
                {
                    var part = new GameObject("Part", typeof(SpriteRenderer));
                    part.transform.SetParent(root.transform, false);
                    _parts[i * PartsPerCorpse + p] = part.GetComponent<SpriteRenderer>();
                }
                root.SetActive(false);
                _corpses[i].Root = root.transform;
            }
        }

        // back = 유닛의 후방(-전방) 부호. 그림은 지금 놓인 그대로(위치·뒤집힘·크기) 본뜬다 — 부르는 쪽이 자세를 기본으로 되돌린 뒤 부른다. height = 유닛 키(월드)
        public void Throw(Vector2 position, SpriteRenderer[] bodies, SpriteRenderer weapon, float back, float size, float height)
        {
            if (_corpses == null || _corpses.Length == 0)
                return;
            int index = _next;
            _next = (_next + 1) % _corpses.Length;
            ref Corpse c = ref _corpses[index];
            if (!c.Active)
                _activeCount++;

            c.Root.SetPositionAndRotation(position, Quaternion.identity);
            int used = 0;
            for (int i = 0; i < bodies.Length && used < PartsPerCorpse; i++)
                Copy(bodies[i], _parts[index * PartsPerCorpse + used++], position);
            if (used < PartsPerCorpse)
                Copy(weapon, _parts[index * PartsPerCorpse + used++], position);
            for (; used < PartsPerCorpse; used++)
                _parts[index * PartsPerCorpse + used].enabled = false;

            // 대난투처럼 후방 비스듬히 위로 날려 보낸다 — 진영마다 거의 한 방향, 뒤로 구르는 쪽으로 돈다
            float angle = (_config.CorpseAngle + Range(-_config.CorpseAngleSpread, _config.CorpseAngleSpread)) * Mathf.Deg2Rad;
            float speed = Range(_config.CorpseSpeedMin, _config.CorpseSpeedMax);
            c.Active = true;
            c.Origin = position;
            c.Velocity = new Vector2(back * Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            c.Spin = -back * Range(_config.CorpseSpinMin, _config.CorpseSpinMax);
            c.Age = 0f;
            c.Size = size;
            c.Puff = DustFx.TrailInterval;
            // 사망 순간 흰 실루엣 + 키 기준 사방 1px 크게(그림 1px 단위 — Juice.SnapScale)
            c.Popping = _config.DeathPopSeconds > 0f;
            c.PopFrame = Time.frameCount;
            if (c.Popping)
            {
                float heightPx = height / PixelMesh.Pixel;
                c.Root.localScale = Vector3.one * (heightPx >= 1f ? Juice.SnapScale((heightPx + 2f) / heightPx, heightPx) : 1f);
                for (int p = index * PartsPerCorpse; p < (index + 1) * PartsPerCorpse; p++)
                {
                    _partColors[p] = _parts[p].color;
                    _parts[p].color = PopColor;
                }
            }
            else
                c.Root.localScale = Vector3.one;
            c.Root.gameObject.SetActive(true);
        }

        // 흰 실루엣을 끝내고 본뜬 색 · 크기로
        private void EndPop(int index)
        {
            ref Corpse c = ref _corpses[index];
            c.Popping = false;
            c.Root.localScale = Vector3.one;
            for (int p = index * PartsPerCorpse; p < (index + 1) * PartsPerCorpse; p++)
                _parts[p].color = _partColors[p];
        }

        // 시체의 뿌리는 회전 없음·스케일 1로 유닛 중심에 놓였다 — 월드 값을 그대로 옮기면 같은 자리·같은 모습이 된다
        private static void Copy(SpriteRenderer source, SpriteRenderer part, Vector2 origin)
        {
            Transform from = source.transform;
            Transform to = part.transform;
            to.localPosition = (Vector2)from.position - origin;
            to.localRotation = from.rotation;
            to.localScale = from.lossyScale;
            part.sprite = source.sprite;
            part.sharedMaterial = source.sharedMaterial;
            part.color = source.color;
            part.flipX = source.flipX;
            part.flipY = source.flipY;
            part.sortingOrder = SortingOrder + source.sortingOrder;
            part.enabled = source.enabled;
        }

        private float Range(float min, float max) => min + (max - min) * (float)_random.NextDouble();

        private void Update()
        {
            if (_activeCount == 0)
                return;
            float dt = Time.deltaTime;
            float gravity = _config.CorpseGravity;
            float lifetime = _config.CorpseLifetime;
            float floor = _camera.transform.position.y - _camera.orthographicSize - ViewMargin;
            for (int i = 0; i < _corpses.Length; i++)
            {
                ref Corpse c = ref _corpses[i];
                if (!c.Active)
                    continue;
                c.Age += dt;
                if (c.Popping && c.Age >= _config.DeathPopSeconds && Time.frameCount > c.PopFrame + 1)
                    EndPop(i);
                float t = c.Age;
                float y = c.Origin.y + c.Velocity.y * t - 0.5f * gravity * t * t;
                if (t >= lifetime || y < floor)
                {
                    c.Active = false;
                    _activeCount--;
                    c.Root.gameObject.SetActive(false);
                    continue;
                }
                var at = new Vector2(c.Origin.x + c.Velocity.x * t, y);
                c.Root.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, c.Spin * t));
                c.Puff -= dt;
                if (c.Puff <= 0f)
                {
                    c.Puff = DustFx.TrailInterval;
                    DustFx.Trail(at, c.Size);
                }
            }
        }
    }
}

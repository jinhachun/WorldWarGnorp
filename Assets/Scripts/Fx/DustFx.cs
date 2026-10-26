using UnityEngine;

namespace GnorpWar
{
    // 사망 먼지(사용자 10/23 — 월드맵에서 영주가 죽을 때처럼): 죽는 자리에서 흰 구름 조각이 터지고, 날아가는 시체(CorpseFx)가 지나온 길에 구름 조각을 남긴다.
    // 조각은 제자리에서 작아지며 흐려진다. 모양 · 크기 · 간격 · 수명은 월드맵의 것(UI/WorldLords의 Cloud 상수)을 전장 배율로 옮겼다 — 지도의 1칸 = 영주 몸 높이 = 전장 1칸.
    // 조각 전부를 메시 하나로(PixelMesh) · 상한을 넘으면 가장 오래된 조각을 돌려 쓴다 · 같은 자리에 잇달아 터지면 한 번만(대량 사망) · 화면 밖 꼬리는 안 남긴다. FxDirector가 만든다
    public class DustFx : MonoBehaviour
    {
        // 조각 지름(px) — 월드맵의 큰 조각(5px 그림 × 6) · 작은 조각(× 3)을 1칸 = 24px에서 8px로 옮긴 값
        private const int LargePixels = 10;
        private const int SmallPixels = 5;
        private const float Seconds = 0.6f;
        // 죽는 순간 터지는 조각 수 · 퍼지는 반경(칸) — 크기 배율을 곱한다
        private const int BurstCount = 7;
        private const float BurstSpread = 0.6f;
        // 꼬리 — 남기는 간격(초) · 흔들림(칸). 간격은 CorpseFx가 센다
        public const float TrailInterval = 0.025f;
        private const float TrailJitter = 0.25f;
        // 같은 자리(이 칸 크기의 격자 한 칸)에서 이 시간 안에 또 터지면 건너뛴다
        private const float RepeatCell = 1f;
        private const float RepeatGap = 0.15f;
        private const int Capacity = 600;
        // 움직이는 조각(Billow)의 속도가 1초에 줄어드는 비율(e^-값)
        private const float BillowDrag = 2.5f;
        // 시체(-15~-13) 바로 뒤 — 날아가는 시체는 늘 구름 위(월드맵과 같다). 땅 채움(-10)에 아래쪽이 가려진다
        public const int SortingOrder = -16;
        private const float ViewMargin = 2f;

        private struct Cloud
        {
            public Vector2 Point;
            public int Pixels;
            public float Age;
            // 무너짐 먼지(Billow)만 쓴다 — 사망 먼지는 제자리 · 같은 수명 · 흰색 그대로
            public Vector2 Velocity;
            public float Life;
            public float Brightness;
            public float Grow;
        }

        private static DustFx _instance;
        private PixelMesh _mesh;
        private readonly Cloud[] _clouds = new Cloud[Capacity];
        private int _next;
        private int _count;
        private Camera _camera;
        private Rect _view;
        private readonly System.Collections.Generic.Dictionary<long, float> _recent = new System.Collections.Generic.Dictionary<long, float>();
        // 게임의 난수(Random)를 쓰지 않는다 — 연출이 전투의 난수 순서를 바꾸지 않게
        private readonly System.Random _random = new System.Random();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "Dust", material, SortingOrder, Capacity * 3);
            _camera = Camera.main;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 죽는 자리 — size = 크기 배율(큰 유닛은 1보다 크다)
        public static void Burst(Vector2 center, float size)
        {
            if (_instance == null)
                return;
            long key = ((long)Mathf.FloorToInt(center.x / RepeatCell) << 32) ^ (uint)Mathf.FloorToInt(center.y / RepeatCell);
            float now = Time.time;
            if (_instance._recent.TryGetValue(key, out float last) && now - last < RepeatGap)
                return;
            _instance._recent[key] = now;
            int count = Mathf.RoundToInt(BurstCount * size);
            for (int i = 0; i < count; i++)
                _instance.Add(center + _instance.Jitter(BurstSpread * size), Mathf.RoundToInt(LargePixels * size));
        }

        // 날아가는 시체가 지나는 자리 — 크고 작은 조각을 번갈아
        public static void Trail(Vector2 point, float size)
        {
            if (_instance == null || !_instance._view.Contains(point))
                return;
            int pixels = _instance._random.Next(2) == 0 ? SmallPixels : LargePixels;
            _instance.Add(point + _instance.Jitter(TrailJitter), Mathf.RoundToInt(pixels * size));
        }

        private Vector2 Jitter(float reach)
            => new Vector2((float)_random.NextDouble() * 2f - 1f, (float)_random.NextDouble() * 2f - 1f) * reach;

        // 움직이는 조각 하나(본진 무너짐 — Fx/BaseCollapse가 크기 · 속도 · 수명 · 밝기를 무작위로 정해 부른다). 속도는 점점 줄고(공기 저항),
        // 2px 이하 조각(흩날리는 부스러기)은 줄지 않고 흐려지기만 한다. grow > 0이면 줄지 않고 수명 동안 (1 + grow)배까지 커진다(솟는 뭉게구름). 화면 밖이면 안 남긴다
        public static void Billow(Vector2 point, int pixels, Vector2 velocity, float life, float brightness, float grow)
        {
            if (_instance == null || !_instance._view.Contains(point) || pixels <= 0 || life <= 0f)
                return;
            _instance.Add(point, pixels, velocity, life, brightness, grow);
        }

        private void Add(Vector2 point, int pixels) => Add(point, pixels, Vector2.zero, Seconds, 1f, 0f);

        private void Add(Vector2 point, int pixels, Vector2 velocity, float life, float brightness, float grow)
        {
            _clouds[_next] = new Cloud { Point = point, Pixels = pixels, Velocity = velocity, Life = life, Brightness = brightness, Grow = grow };
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
                _count++;
        }

        private void LateUpdate()
        {
            if (_camera != null)
            {
                float halfH = _camera.orthographicSize + ViewMargin, halfW = _camera.orthographicSize * _camera.aspect + ViewMargin;
                Vector2 c = _camera.transform.position;
                _view = new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);
            }
            if (_recent.Count > 256)
                _recent.Clear();
            _mesh.Begin();
            // 판이 끝나 시간이 멈춘 동안(본진 무너짐 · 결과 창)에도 조각은 실제 시간으로 사그라든다(Fx/BaseCollapse)
            float dt = Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
            // 다한 조각은 대개 가장 오래된 쪽에 몰려 있다 — 앞에서 이어진 것만 덜어 낸다(수명이 다른 무너짐 먼지는 중간에서 먼저 다하면 그리지만 않는다)
            int dead = 0;
            bool front = true;
            float drag = Mathf.Exp(-BillowDrag * dt);
            // 가장 오래된 것부터 — 나중에 난 조각이 위에 그려진다
            for (int n = 0; n < _count; n++)
            {
                int i = (_next - _count + n + Capacity) % Capacity;
                ref Cloud cloud = ref _clouds[i];
                cloud.Age += dt;
                float left = 1f - cloud.Age / cloud.Life;
                if (left <= 0f)
                {
                    if (front)
                        dead++;
                    continue;
                }
                front = false;
                if (cloud.Velocity != Vector2.zero)
                {
                    cloud.Point += cloud.Velocity * dt;
                    cloud.Velocity *= drag;
                }
                int d = cloud.Pixels <= 2 ? cloud.Pixels : Mathf.RoundToInt(cloud.Pixels * (cloud.Grow > 0f ? 1f + cloud.Grow * (1f - left) : left));
                if (d <= 0)
                    continue;
                Color32 color = PixelMesh.Faded(new Color(cloud.Brightness, cloud.Brightness, cloud.Brightness), 0.4f + 0.6f * left);
                int x = PixelMesh.ToGrid(cloud.Point.x) - d / 2, y = PixelMesh.ToGrid(cloud.Point.y) - d / 2;
                // 동그란 도트 — 네 귀퉁이를 지름의 1/5만큼 깎는다(월드맵 그림 .###. / ##### 와 같은 비율)
                int k = Mathf.RoundToInt(d / 5f);
                if (k == 0 || d <= 2)
                {
                    _mesh.Rect(x, y, d, d, color);
                    continue;
                }
                _mesh.Rect(x, y + k, d, d - 2 * k, color);
                _mesh.Rect(x + k, y, d - 2 * k, k, color);
                _mesh.Rect(x + k, y + d - k, d - 2 * k, k, color);
            }
            _count -= dead;
            _mesh.End();
        }

        // 떠 있는 조각 수(검사용)
        public static int Clouds => _instance != null ? _instance._count : 0;
    }
}

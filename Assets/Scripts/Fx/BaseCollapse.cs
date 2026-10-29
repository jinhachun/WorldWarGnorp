using UnityEngine;
using UnityEngine.InputSystem;

namespace GnorpWar
{
    // 진 쪽 본진이 무너진다(사용자 10/26 — Gnorp Apologue의 건물 짓기를 거꾸로):
    // ① 카메라가 그 본진으로 계단식으로 옮겨 가며 확대된다(사용자 10/26 — 기지 부술 때 줌). 그동안 카메라 끌기 · 휠(UI/CameraDrag)은 끈다 — 판이 끝났으니 다시 켜지 않는다.
    //    확대 배율은 그림 1px이 화면 정수 px가 되는 크기 중 본진 전체 + 위로 솟는 먼지가 들어오는 가장 가까운 것. 화면 아래 끝은 땅선 바로 아래로 — 맵 끝(Ground.MinX~MaxX) 밖은 보이지 않게 묶는다.
    // ② 성 · 탑 층 · 지붕 · 깃발이 한 덩어리로 그림 1px 단위 계단을 밟으며 땅속으로 꺼지고, 그동안 흰 먼지(DustFx.Billow)가 몸체 전체에서 피어오른다(사용자 10/26: 바닥에만 생기지 말게) —
    //    꺼져 내려가는 꼭대기 둘레 · 남은 몸체 곳곳 · 꼭대기 위로 솟는 큰 구름 · 바닥 양옆(적게). 크기 · 시점 · 방향 · 수명 · 밝기는 범위 안에서 무작위, 부스러기 점이 섞인다.
    //    다 꺼지면 본진을 끈다(빈 터). 땅선은 그대로 — 본진 그림들을 먼지(DustFx.SortingOrder) 뒤로 보낸다. 땅 속 채움은 그보다 앞이라 땅 아래로 내려간 부분이 가려진다.
    // 판은 멈춰 있다(timeScale 0) — 실제 시간으로 돈다. 클릭 · 키면 카메라 · 꺼짐 모두 바로 끝 상태. BattleManager.Finish가 부르고 Busy가 풀리면 결과 창을 연다. FxDirector가 만든다
    public class BaseCollapse : MonoBehaviour
    {
        // 먼지 — 바닥 양옆 출발 자리가 본진 가장자리 밖으로 나가는 폭(본진 절반 폭의 비율) · 구름 크기 배율 = 본진 폭 / 이 칸 · 배율 1의 구름 지름(px, 사망 먼지의 큰 조각과 같다)
        private const float SpreadOfWidth = 0.35f;
        private const float DustWidthPerSize = 6f;
        private const int PuffPixels = 10;
        // 성벽 위로 솟은 탑의 절반 폭 — 본진(성) 절반 폭의 이 비율
        private const float TowerOfWidth = 0.4f;
        // 뿜는 곳마다 다음까지의 간격(초, 최소 ~ 최대 사이 무작위) — 꼭대기 둘레 · 몸체 곳곳 · 꼭대기 위 큰 구름 · 바닥 왼쪽 · 바닥 오른쪽
        private static readonly Vector2[] Gaps = { new Vector2(0.015f, 0.05f), new Vector2(0.015f, 0.045f), new Vector2(0.14f, 0.3f), new Vector2(0.06f, 0.15f), new Vector2(0.06f, 0.15f) };
        // 꺼지기 시작할 때 한 번에 뿜는 구름 수(꼭대기 · 바닥 양옆 각각)
        private const int OpeningPuffs = 4;
        // 땅선보다 조금 위에서 — 조각 아래쪽은 땅 채움에 가려진다
        private const float DustLift = 0.25f;
        // 카메라 — 옮겨 가는 계단 수 · 확대 화면에서 본진 위(솟는 구름 몫) · 양옆 여유(칸)
        private const int CameraSteps = 6;
        private const float TopMargin = 4f;
        private const float SideMargin = 2f;
        // 확대 화면에서 땅선 아래로 보이는 높이(칸) — 먼지가 땅 위에 앉아 있으니 화면 아래를 땅 가까이로 올린다
        private const float BottomMargin = 1.5f;

        private static BaseCollapse _instance;

        private Base _home;
        private Vector3 _start;
        private float _ground;
        private float _top;       // 처음 본진 그림 꼭대기(월드 y)
        private float _wallTop;   // 처음 성 그림 꼭대기 — 이보다 위는 탑(좁다)
        private int _depth;   // 꺼질 깊이(px) = 땅선에서 본진 그림 꼭대기까지
        private int _sunk;
        private float _cameraSeconds, _sinkSeconds;
        private float _elapsed;
        private float _left, _right, _dustSize;
        private Camera _camera;
        private Transform _rig;
        private Vector3 _rigFrom, _rigTo;
        private float _sizeFrom, _sizeTo;
        // 뿜는 곳(Gaps 순서)마다 다음 시각(_elapsed 기준)
        private readonly float[] _next = new float[Gaps.Length];
        // 게임의 난수(Random)를 쓰지 않는다 — DustFx와 같은 이유
        private readonly System.Random _random = new System.Random();

        public static bool Busy => _instance != null && _instance._home != null;
        // 이 진영 본진이 지금 꺼지는 중 — 탑 위에 덧그리는 연출(TerrainFx 창 표시)이 건너뛴다
        public static bool Sinks(Team team) => Busy && _instance._home.Team == team;

        public void Init() => _instance = this;

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static void Play(Base home, float cameraSeconds, float sinkSeconds)
        {
            if (_instance == null || sinkSeconds <= 0f)
            {
                home.gameObject.SetActive(false);
                return;
            }
            _instance.Begin(home, Mathf.Max(0f, cameraSeconds), sinkSeconds);
        }

        private void Begin(Base home, float cameraSeconds, float sinkSeconds)
        {
            Renderer[] renderers = home.GetComponentsInChildren<Renderer>(true);   // 꺼 둔 층 · 표시도 — 나중에 켜져도 순서가 맞게
            SpriteRenderer castle = home.GetComponent<SpriteRenderer>();
            Bounds look = castle.bounds;
            int highest = int.MinValue;
            foreach (Renderer r in renderers)
            {
                look.Encapsulate(r.bounds);
                highest = Mathf.Max(highest, r.sortingOrder);
            }
            // 사이 순서는 그대로 두고 통째로 먼지 뒤로(땅 속 채움은 먼지보다 앞)
            int shift = DustFx.SortingOrder - 1 - highest;
            foreach (Renderer r in renderers)
                r.sortingOrder += shift;

            _home = home;
            _start = home.transform.position;
            _ground = castle.bounds.min.y;
            _top = look.max.y;
            _wallTop = castle.bounds.max.y;
            _depth = Mathf.CeilToInt((_top - _ground) / PixelMesh.Pixel);
            _sunk = 0;
            _cameraSeconds = cameraSeconds;
            _sinkSeconds = sinkSeconds;
            _elapsed = 0f;
            _left = home.Bounds.min.x;
            _right = home.Bounds.max.x;
            _dustSize = (_right - _left) / DustWidthPerSize;
            for (int i = 0; i < _next.Length; i++)
                _next[i] = cameraSeconds + Range(0f, Gaps[i].y);
            AimCamera();
        }

        // 확대 끝 상태를 정한다 — 화면 아래 끝은 땅선 BottomMargin 아래, 가로 가운데는 본진(맵 끝이 화면 밖으로 안 나가게)
        private void AimCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
                return;
            _rig = _camera.transform.parent != null ? _camera.transform.parent : _camera.transform;
            if (_rig.TryGetComponent(out CameraDrag drag))
                drag.enabled = false;
            _rigFrom = _rig.position;
            _sizeFrom = _camera.orthographicSize;
            float bottom = _ground - BottomMargin;
            float reach = (_right - _left) * (0.5f + SpreadOfWidth) + _dustSize + SideMargin;
            float need = Mathf.Max((_top + TopMargin - bottom) * 0.5f, reach / _camera.aspect);
            // 그림 1px = 화면 n px(정수) — 화면 높이 = 2 × 크기 × 8 × n
            int n = Mathf.FloorToInt(_camera.pixelHeight / (16f * need));
            float size = n >= 1 ? _camera.pixelHeight / (16f * n) : need;
            _sizeTo = Mathf.Min(size, _sizeFrom);   // 이미 더 가까우면 그대로
            float half = _sizeTo * _camera.aspect;
            float x = (_left + _right) * 0.5f;
            if (Ground.Instance != null)
                x = Mathf.Clamp(x, Ground.Instance.MinX + half, Mathf.Max(Ground.Instance.MinX + half, Ground.Instance.MaxX - half));
            _rigTo = new Vector3(x, bottom + _sizeTo, _rigFrom.z);
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
        private float Sign => _random.Next(2) == 0 ? -1f : 1f;
        private float Center => (_left + _right) * 0.5f;

        // 높이 y에서 몸체의 절반 폭 — 성벽 위는 탑
        private float HalfWidthAt(float y, float sunk)
            => (_right - _left) * 0.5f * (y > _wallTop - sunk ? TowerOfWidth : 1f);

        // 구름 하나 + 부스러기 몇. 출발 자리 · 기본 속도를 받아 크기 · 속도 · 수명 · 밝기를 범위 안에서 흔든다.
        // grow > 0 = 솟으며 커지는 뭉게구름(그만큼 커진다), 0 = 사망 먼지처럼 줄어든다
        private void Puff(Vector2 at, Vector2 velocity, float scale, float grow, float life)
        {
            at.x = OnMap(at.x);
            DustFx.Billow(at, Mathf.RoundToInt(PuffPixels * _dustSize * scale * Range(0.6f, 1.3f)),
                velocity * Range(0.7f, 1.3f), life * Range(0.75f, 1.25f), Range(0.7f, 1f), grow * Range(0.6f, 1.2f));
            int specks = _random.Next(0, 3);
            for (int i = 0; i < specks; i++)
                DustFx.Billow(at + new Vector2(Range(-0.5f, 0.5f), Range(0f, 0.5f)), _random.Next(1, 3),
                    new Vector2(Mathf.Sign(velocity.x == 0f ? Sign : velocity.x) * Range(2f, 7f), Range(1.5f, 5f)), Range(0.3f, 0.7f), 1f, 0f);
        }

        // 뿜는 곳 하나(Gaps 순서) — top = 지금 보이는 꼭대기(월드 y), sunk = 내려간 거리
        private void Emit(int kind, float top, float sunk)
        {
            float half;
            switch (kind)
            {
                case 0:   // 꺼져 내려가는 꼭대기 둘레 — 옆 · 위로 퍼진다
                    half = HalfWidthAt(top - 0.5f, sunk);
                    Puff(new Vector2(Center + Range(-1.2f, 1.2f) * half, top + Range(-0.8f, 0.4f)),
                        new Vector2(Range(-3f, 3f), Range(0.5f, 2f)), 1f, 0.8f, 0.8f);
                    break;
                case 1:   // 남은 몸체 곳곳 — 무작위 높이 · 폭에서 터진다
                    float y = Range(_ground + DustLift, Mathf.Max(_ground + DustLift, top));
                    half = HalfWidthAt(y, sunk);
                    Puff(new Vector2(Center + Range(-1.1f, 1.1f) * half, y),
                        new Vector2(Range(-2.5f, 2.5f), Range(0.3f, 1.5f)), 1f, 0.5f, 0.7f);
                    break;
                case 2:   // 꼭대기 위로 솟는 큰 뭉게구름 — 본진 꼭대기 높이 위까지
                    half = HalfWidthAt(top - 0.5f, sunk);
                    Puff(new Vector2(Center + Range(-1f, 1f) * half, top + Range(0f, 1f)),
                        new Vector2(Range(-1.5f, 1.5f), Range(1.5f, 3.5f)), 1.5f, 1.2f, 1f);
                    break;
                default:  // 바닥 양옆 — 바깥 + 살짝 위
                    float side = kind == 3 ? -1f : 1f;
                    float edge = (_right - _left) * 0.5f;
                    Puff(new Vector2(Center + side * Range(edge * 0.5f, edge * (1f + SpreadOfWidth)), _ground + DustLift + Range(0f, 0.5f)),
                        new Vector2(side * Range(1.5f, 5f), Range(0.4f, 2f)), 1f, 0f, 0.7f);
                    break;
            }
        }

        // 본진 뒤쪽은 맵 끝에 붙어 있다 — 그쪽 먼지는 맵 끝에서 더 퍼지지 않는다(카메라가 맵 밖을 안 비춘다)
        private static float OnMap(float x)
            => Ground.Instance != null ? Mathf.Clamp(x, Ground.Instance.MinX, Ground.Instance.MaxX) : x;

        private void Update()
        {
            if (_home == null)
                return;
            bool skip = Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
                        || Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            _elapsed += Time.unscaledDeltaTime;

            // ① 카메라 — 고르게 흐르는 시간을 계단 CameraSteps번으로 끊는다
            float c = skip || _cameraSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _cameraSeconds);
            if (_rig != null)
            {
                float k = Mathf.Floor(c * CameraSteps) / CameraSteps;
                _rig.position = Vector3.Lerp(_rigFrom, _rigTo, k);
                _camera.orthographicSize = Mathf.Lerp(_sizeFrom, _sizeTo, k);
            }
            if (c < 1f)
                return;

            // ② 꺼짐 — 고르게 흐르는 시간을 정수 px로 끊는다(이징 없음). 카메라가 닿은 뒤부터 센다
            float t = skip ? 1f : Mathf.Clamp01((_elapsed - _cameraSeconds) / _sinkSeconds);
            bool opening = _sunk == 0 && t > 0f && !skip;
            int sunk = Mathf.Max(1, Mathf.FloorToInt(_depth * t));
            if (sunk != _sunk)
            {
                _sunk = sunk;
                _home.transform.position = _start + Vector3.down * (sunk * PixelMesh.Pixel);
            }
            if (!skip)
            {
                float drop = sunk * PixelMesh.Pixel, top = Mathf.Max(_top - drop, _ground);
                if (opening)
                    for (int i = 0; i < OpeningPuffs; i++)
                    {
                        Emit(0, top, drop);
                        Emit(3, top, drop);
                        Emit(4, top, drop);
                    }
                // 뿜는 곳마다 따로 들쭉날쭉한 간격으로
                for (int kind = 0; kind < _next.Length; kind++)
                    while (_next[kind] <= _elapsed)
                    {
                        Emit(kind, top, drop);
                        _next[kind] += Range(Gaps[kind].x, Gaps[kind].y);
                    }
            }
            if (t >= 1f)
            {
                _home.gameObject.SetActive(false);
                _home = null;
            }
        }
    }
}

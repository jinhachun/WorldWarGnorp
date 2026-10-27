using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 투사체 꼬리(전투 시각화 F1) — 화살 뒤로 지난 자리 점 몇 개가 따라온다. 관통 = 불꽃(사용자 10/27 — 점이 노랑에서 빨강으로 · 불씨를 떨군다, Fx/FireTrails) · 처치해서 더 쏜 화살 = 노랑.
    // 관통 신화(관통 타격마다 피해 ×)는 뚫을 때마다 점이 하나씩 는다(Grow). 화살마다 TrailRenderer · GameObject를 붙이지 않는다 —
    // 꼬리 전부를 메시 하나로(PixelMesh). 화살이 닿아 사라지면 남은 점이 짧게 따라 줄어든다. FxDirector가 만든다
    public class ArrowTrails : MonoBehaviour
    {
        private const int Capacity = 256;
        // 지난 자리를 찍는 간격 — 날아간 거리(2px)마다 하나라 느린 화살(꼭짓점 근처)도 꼬리 길이가 같다. 닿은 뒤에는 이 시간마다 한 점씩 거둔다
        private const float Spacing = 2f * PixelMesh.Pixel;
        private const float SampleGap = 0.025f;
        // 기본 점 수 · 상한
        private const int BaseDots = 3;
        private const int MaxDots = 8;
        private const int SortingOrder = 120;   // 유닛 위 · 버프 선(150) 아래
        private const int MaxQuads = Capacity * MaxDots;
        // 관통탄 불씨(Fx/FireTrails) — 찍은 자리마다 이 확률로 하나, 2px에서 줄며 이만큼(초) 산다
        private const float FireEmberChance = 0.5f;
        private const float FireEmberLifeMin = 0.25f;
        private const float FireEmberLifeMax = 0.4f;
        private const float FireEmberDrift = 0.3f;   // 칸/초

        private class Trail
        {
            public Projectile Shot;
            public int Flight;
            // 0 = 가장 최근
            public readonly Vector2[] Points = new Vector2[MaxDots + 1];
            public int Count;
            public int Dots;
            public Color32 Color;
            public bool Fire;
            public float SampleTimer;
            public bool Landed;
        }

        private static ArrowTrails _instance;
        private PixelMesh _mesh;
        private readonly List<Trail> _active = new List<Trail>();
        private readonly Stack<Trail> _free = new Stack<Trail>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "ArrowTrails", material, SortingOrder, MaxQuads);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 이번 비행에 꼬리를 단다(같은 비행에 또 부르면 색만 바꾼다)
        public static void Follow(Projectile shot, Color color)
        {
            if (_instance == null)
                return;
            Trail trail = _instance.Find(shot);
            if (trail == null)
            {
                if (_instance._active.Count >= Capacity)
                    return;   // 꽉 차면 꼬리 없이 — 프레임이 무거워지는 것보다 낫다
                trail = _instance._free.Count > 0 ? _instance._free.Pop() : new Trail();
                trail.Shot = shot;
                trail.Flight = shot.Flight;
                trail.Count = 0;
                trail.Dots = BaseDots;
                trail.SampleTimer = 0f;
                trail.Landed = false;
                trail.Fire = false;
                _instance._active.Add(trail);
            }
            trail.Color = color;
        }

        // 이번 비행에 불꽃 꼬리를 단다(관통탄) — 뒤에 Follow로 색을 바꿔도 불꽃이 남는다
        public static void FollowFire(Projectile shot)
        {
            Follow(shot, Color.white);
            Trail trail = _instance != null ? _instance.Find(shot) : null;
            if (trail != null)
                trail.Fire = true;
        }

        // 꼬리를 점 하나 늘린다(관통 신화 — 뚫을 때마다)
        public static void Grow(Projectile shot)
        {
            Trail trail = _instance != null ? _instance.Find(shot) : null;
            if (trail != null)
                trail.Dots = Mathf.Min(trail.Dots + 1, MaxDots);
        }

        private Trail Find(Projectile shot)
        {
            foreach (Trail trail in _active)
                if (trail.Shot == shot && trail.Flight == shot.Flight && !trail.Landed)
                    return trail;
            return null;
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Trail trail = _active[i];
                // 닿아 사라졌거나 풀에서 다른 비행이 됐다 — 남은 점을 하나씩 거둔다
                if (!trail.Landed && (!trail.Shot.isActiveAndEnabled || trail.Shot.Flight != trail.Flight))
                    trail.Landed = true;
                if (trail.Landed)
                {
                    trail.SampleTimer -= dt;
                    while (trail.SampleTimer <= 0f && trail.Count > 0)
                    {
                        trail.SampleTimer += SampleGap;
                        trail.Count--;
                    }
                }
                else
                {
                    Vector2 now = trail.Shot.Body.position;
                    if (trail.Count == 0 || (now - trail.Points[0]).sqrMagnitude >= Spacing * Spacing)
                    {
                        Push(trail, now);
                        // 불꽃 — 찍은 자리 둘에 하나꼴로 불씨를 떨군다(화살이 많아도 수수하게)
                        if (trail.Fire && Random.value < FireEmberChance)
                            FireTrails.Emit(now, Random.insideUnitCircle * FireEmberDrift, 2, Random.Range(FireEmberLifeMin, FireEmberLifeMax), true);
                    }
                }
                if (trail.Count <= 0)
                {
                    _active.RemoveAt(i);
                    _free.Push(trail);
                    continue;
                }
                Write(trail);
            }
            _mesh.End();
        }

        private static void Push(Trail trail, Vector2 point)
        {
            int keep = Mathf.Min(trail.Count, trail.Dots);
            for (int k = keep; k > 0; k--)
                trail.Points[k] = trail.Points[k - 1];
            trail.Points[0] = point;
            trail.Count = keep + 1;
        }

        // 가장 최근 자리(0)는 화살 꽁무니 근처 — 앞쪽 점은 2px, 뒤로 1px · 흐려진다
        private void Write(Trail trail)
        {
            for (int k = 0; k < trail.Count; k++)
            {
                float t = (k + 1f) / (trail.Dots + 1f);
                Color color = trail.Fire ? FireTrails.Heat(t) : (Color)trail.Color;
                _mesh.Square(trail.Points[k], k == 0 ? 2 : 1, PixelMesh.Faded(color, 1f - t * 0.7f));
            }
        }
    }
}

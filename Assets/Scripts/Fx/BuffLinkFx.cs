using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 전장의 버프 선 — 기물이 누구에게 무엇을 줬는지(BuffLinks.Raised)를 그 층에서 대상까지 굽은 선 하나로 바로 긋는다.
    // 효과는 이미 걸린 뒤다(게임 타이밍과 무관). 모양·시간은 BuffLinkShape(UI 탑과 공용) — 머리가 대상으로 지나가고 가는 꼬리가 따라 사라진다.
    // 그림 픽셀(1/8칸) 격자에 맞춘 네모를 곡선 따라 찍는다(머리 크고 꼬리 작게) — 선 전부를 메시 하나로(드로우콜 1 · 프레임 할당 0).
    // 색 = 종류. 층 → 층은 탑 앞쪽으로 불룩한 호, 층 → 유닛·타워는 위로 불룩한 호. FxDirector가 만든다(씬에 따로 두지 않는다)
    public class BuffLinkFx : MonoBehaviour
    {
        // 같은 선(진영·출발·대상 칸·종류)을 이 시간 안에 또 받으면 긋지 않는다 — 처치·회복마다 터지는 효과가 선으로 화면을 덮지 않게
        private const float MinGap = 0.15f;
        // 그림 픽셀 하나(PPU 8)
        private const float Pixel = 0.125f;
        // 불룩함 하한·상한(칸)
        private const float MinBulge = 0.75f;
        private const float MaxBulge = 3f;
        private const int SortingOrder = 150;   // 유닛 위 · 데미지 숫자(200) 아래
        // 화면 밖 판정 여유(칸)
        private const float ViewMargin = 2f;

        // BuffKind 순서 — 소환 초록 · 스탯 빨강(유닛 공격력 원판 색) · 무적 노랑(무적 링 색) · 가속 하늘 · 디버프 밝은 보라(등급 테두리의 영웅 보라보다 밝고 분홍 쪽) · 발동 흰
        private static readonly Color[] Colors =
        {
            new Color(0.12f, 0.67f, 0.24f),
            new Color(1f, 0.15f, 0.1f),
            new Color(1f, 0.85f, 0.1f),
            new Color(0.4f, 0.85f, 1f),
            new Color(0.95f, 0.45f, 1f),
            Color.white,
        };

        // 종류별 선 색 — UI 탑의 선·층 표시도 이걸 읽는다
        public static Color ColorOf(BuffKind kind) => Colors[(int)kind];

        private class Line
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public Color32 Color;
            public bool Weak;
            public float Age;
        }

        private Mesh _mesh;
        private bool _drawn;
        private readonly List<Line> _active = new List<Line>();
        private readonly Stack<Line> _free = new Stack<Line>();
        private readonly Dictionary<int, float> _lastDrawn = new Dictionary<int, float>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();

        public void Init(Material material)
        {
            // 정점을 월드 좌표로 쓴다 — 메시 오브젝트는 원점에
            var go = new GameObject("BuffLinks", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            _mesh = new Mesh { name = "BuffLinks" };
            _mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = SortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void OnEnable() => BuffLinks.Raised += OnRaised;
        private void OnDisable() => BuffLinks.Raised -= OnRaised;

        private void OnRaised(BuffLink link)
        {
            Tower tower = Tower.For(link.Team);
            if (tower == null || link.From >= tower.FloorCount || link.To >= tower.FloorCount || (link.To < 0 && link.At == null))
                return;

            // 대상이 유닛이면 대상 자리와 상관없이 (출발·종류)로 솎는다. 타워면 타워마다 하나씩 남긴다(타워 번호 — 16채부터는 번호가 겹쳐 솎인다)
            // 유닛이 낸 선(FromAt)은 같은 기물의 층에서 나간 선과 따로 솎는다
            int key = ((((((int)link.Team * 64 + link.From + 1) * 64 + link.To + 1) * 16 + (int)link.Kind) * 2 + (link.Weak ? 1 : 0)) * 16 + (link.Tower & 15)) * 2 + (link.FromAt.HasValue ? 1 : 0);
            float now = Time.unscaledTime;
            if (_lastDrawn.TryGetValue(key, out float last) && now - last < MinGap)
                return;

            // 맵 효과(준 기물 없음)는 그 진영의 출발점 그림에서(얼음 덩이 — Fx/MapMarker), 없으면 탑 꼭대기에서
            MapMarker marker = MapMarker.For(link.Team);
            // 유닛이 낸 것은 그 유닛의 자리에서(모양은 층 → 유닛과 같은 위로 불룩한 호)
            Vector2 from = link.FromAt ?? (link.From >= 0 ? tower.FloorFront(link.From) : marker != null ? marker.Position : tower.RoofTop);
            Vector2 to = link.To >= 0 ? tower.FloorFront(link.To) : link.At.Value;
            if (!InView(from) && !InView(to))
                return;
            _lastDrawn[key] = now;
            // 굽는 쪽 — 위·탑 앞쪽
            float forward = link.Team == Team.Ally ? 1f : -1f;
            // 스탯 · 쿨다운 선이면 대상 위에 무슨 버프인지(약하게 다시 알리는 선은 빼고). 대상이 층 · 타워면 탑을 가리지 않게 앞쪽으로 붙인다
            if (!link.Weak)
                BuffTexts.Show(to, link.Label, link.To >= 0 || link.Tower > 0 ? forward : 0f);

            Line line = _free.Count > 0 ? _free.Pop() : new Line();
            BuffLinkShape.Fill(line.Points, from, to, BuffLinkShape.NewSeed(), new Vector2(forward * 0.5f, 1f), MinBulge, MaxBulge, Pixel * 0.5f);
            line.Color = Colors[(int)link.Kind];
            line.Weak = link.Weak;
            line.Age = 0f;
            _active.Add(line);
        }

        private static bool InView(Vector2 point)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return true;
            float halfHeight = cam.orthographicSize + ViewMargin;
            float halfWidth = cam.orthographicSize * cam.aspect + ViewMargin;
            Vector3 c = cam.transform.position;
            return Mathf.Abs(point.x - c.x) <= halfWidth && Mathf.Abs(point.y - c.y) <= halfHeight;
        }

        private void LateUpdate()
        {
            if (_active.Count == 0 && !_drawn)
                return;

            _vertices.Clear();
            _uvs.Clear();
            _colors.Clear();
            _triangles.Clear();
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Line line = _active[i];
                // 한 프레임이 길어도(전투 시작 프레임 등) 머리가 지나가는 게 건너뛰어지지 않게 프레임당 진행을 묶는다
                line.Age += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                if (line.Age >= BuffLinkShape.Lifetime)
                {
                    _free.Push(line);
                    _active.RemoveAt(i);
                    continue;
                }
                Write(line);
            }

            _drawn = _vertices.Count > 0;
            _mesh.Clear(true);
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0, false);
            // 카메라에 잘리지 않게 넉넉히 — 정점이 월드 좌표라 계산할 필요가 없다
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 1f));
        }

        // 선의 보이는 구간을 격자에 맞춘 네모들로 쓴다 — 같은 칸에 같은 크기로 또 찍지 않는다
        private void Write(Line line)
        {
            BuffLinkShape.Visible(line.Age, out float tail, out float head);
            int count = line.Points.Count;
            int lastX = int.MinValue, lastY = 0, lastSize = 0;
            for (int i = Mathf.FloorToInt(tail * (count - 1)); i < count; i++)
            {
                float t = i / (count - 1f);
                int size = BuffLinkShape.Pixels(t, tail, head, line.Weak);
                if (size == 0)
                {
                    if (t > head)
                        break;
                    continue;
                }
                // 네모의 왼쪽 아래 칸 — 점이 네모 가운데에 오게
                Vector2 point = line.Points[i];
                int x = Mathf.RoundToInt(point.x / Pixel - size * 0.5f), y = Mathf.RoundToInt(point.y / Pixel - size * 0.5f);
                if (x == lastX && y == lastY && size == lastSize)
                    continue;
                lastX = x;
                lastY = y;
                lastSize = size;

                Color32 color = line.Color;
                color.a = (byte)(255f * BuffLinkShape.Alpha(t, tail, head, line.Weak));
                float left = x * Pixel, bottom = y * Pixel, side = size * Pixel;
                int v = _vertices.Count;
                _vertices.Add(new Vector3(left, bottom, 0f));
                _vertices.Add(new Vector3(left, bottom + side, 0f));
                _vertices.Add(new Vector3(left + side, bottom + side, 0f));
                _vertices.Add(new Vector3(left + side, bottom, 0f));
                for (int k = 0; k < 4; k++)
                {
                    _uvs.Add(new Vector2(0.5f, 0.5f));
                    _colors.Add(color);
                }
                _triangles.Add(v);
                _triangles.Add(v + 1);
                _triangles.Add(v + 2);
                _triangles.Add(v);
                _triangles.Add(v + 2);
                _triangles.Add(v + 3);
            }
        }
    }
}

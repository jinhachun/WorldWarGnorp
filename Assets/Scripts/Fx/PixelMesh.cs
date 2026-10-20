using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 그림 픽셀(1/8칸) 격자에 맞춘 네모들을 메시 하나로 그린다 — 전장 연출 부품(꼬리 · 고리 · 상태 점 · 기둥 · 층 배수 글자)이 하나씩 갖는다.
    // 버프 선(BuffLinkFx)과 같은 방식: 정점은 월드 좌표 · 프레임마다 다시 쓰고 · 드로우콜 1. 한 메시의 네모 수에 상한을 둔다(넘치면 그 프레임엔 버린다)
    public class PixelMesh
    {
        public const float Pixel = 0.125f;

        private readonly Mesh _mesh;
        private readonly int _maxQuads;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();
        private bool _drawn;

        public PixelMesh(Transform parent, string name, Material material, int sortingOrder, int maxQuads)
        {
            _maxQuads = maxQuads;
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        public bool Full => _vertices.Count >= _maxQuads * 4;

        public void Begin()
        {
            _vertices.Clear();
            _uvs.Clear();
            _colors.Clear();
            _triangles.Clear();
        }

        // 격자 칸 (x, y)를 왼쪽 아래로 하는 w×h 픽셀 네모
        public void Rect(int x, int y, int w, int h, Color32 color)
        {
            if (Full || w <= 0 || h <= 0)
                return;
            float left = x * Pixel, bottom = y * Pixel, right = left + w * Pixel, top = bottom + h * Pixel;
            int v = _vertices.Count;
            _vertices.Add(new Vector3(left, bottom, 0f));
            _vertices.Add(new Vector3(left, top, 0f));
            _vertices.Add(new Vector3(right, top, 0f));
            _vertices.Add(new Vector3(right, bottom, 0f));
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

        // 월드 점 하나를 가운데로 하는 size×size 픽셀 네모(격자에 맞춘다)
        public void Square(Vector2 center, int size, Color32 color)
            => Rect(Mathf.RoundToInt(center.x / Pixel - size * 0.5f), Mathf.RoundToInt(center.y / Pixel - size * 0.5f), size, size, color);

        public static int ToGrid(float world) => Mathf.RoundToInt(world / Pixel);

        public void End()
        {
            if (_vertices.Count == 0 && !_drawn)
                return;
            _drawn = _vertices.Count > 0;
            _mesh.Clear(true);
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0, false);
            // 카메라에 잘리지 않게 넉넉히 — 정점이 월드 좌표라 계산할 필요가 없다
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 1f));
        }

        public static Color32 Faded(Color color, float alpha)
        {
            Color32 c = color;
            c.a = (byte)(255f * Mathf.Clamp01(alpha) * color.a);
            return c;
        }
    }
}

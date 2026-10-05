using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 데미지·회복 숫자. 유닛 수백 마리가 동시에 맞아도 버티도록 화면의 숫자 전부를 메시 하나로 그린다
    // (드로우콜 1 · 프레임마다 할당 없음). 글꼴은 3×5 픽셀 숫자를 코드로 만든다.
    // 같은 대상이 짧은 간격으로 또 맞으면(화염 등) 새 숫자를 띄우지 않고 떠 있는 숫자에 더한다.
    // 정점은 월드 좌표로 쓰므로 이 오브젝트는 원점·회전 없음·스케일 1인 루트에 둔다
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DamageNumbers : MonoBehaviour
    {
        private const int Capacity = 512;
        private const int MaxDigits = 5;
        private const int MaxValue = 99999;
        private const float Lifetime = 0.7f;
        private const float RiseHeight = 1.2f;
        // 이 나이 안의 숫자에는 같은 대상의 새 피해를 더한다
        private const float MergeWindow = 0.2f;
        // 사라지기 시작하는 나이 비율
        private const float FadeFrom = 0.6f;
        private const float JitterX = 0.2f;
        private const int SortingOrder = 200;
        // 픽셀 하나의 월드 크기 — PPU 8
        private const float Pixel = 0.125f;
        // 글자 칸 = 3×5 숫자 + 사방 1픽셀 테두리. 글자 간격은 4픽셀(테두리끼리 겹친다)
        private const int CellW = 5;
        private const int CellH = 7;
        private const int Advance = 4;

        private static readonly Color32 EnemyHurt = new Color32(255, 220, 60, 255);
        private static readonly Color32 AllyHurt = new Color32(255, 80, 70, 255);
        private static readonly Color32 Healed = new Color32(90, 255, 110, 255);

        // 위에서 아래로 3칸씩
        private static readonly string[] Glyphs =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
        };

        private struct Entry
        {
            public bool Active;
            public object Target;
            public Vector2 Position;
            public float Amount;
            public float Age;
            public Color32 Color;
        }

        [Tooltip("Sprite-Unlit-Default 재질 — 복제해 글꼴 텍스처를 입힌다")]
        [SerializeField] private Material _material;

        private readonly Entry[] _entries = new Entry[Capacity];
        private readonly Stack<int> _free = new Stack<int>(Capacity);
        // 대상 → 그 대상의 가장 최근 숫자
        private readonly Dictionary<object, int> _latest = new Dictionary<object, int>(Capacity);

        private readonly Vector3[] _vertices = new Vector3[Capacity * MaxDigits * 4];
        private readonly Vector2[] _uvs = new Vector2[Capacity * MaxDigits * 4];
        private readonly Color32[] _colors = new Color32[Capacity * MaxDigits * 4];
        private readonly int[] _triangles = new int[Capacity * MaxDigits * 6];
        private Mesh _mesh;
        private int _drawnQuads;

        private static DamageNumbers _instance;

        public static void Damage(object target, Team victim, Vector2 point, float amount)
        {
            if (_instance != null)
                _instance.Show(target, point, amount, victim == Team.Ally ? AllyHurt : EnemyHurt);
        }

        public static void Heal(object target, Vector2 point, float amount)
        {
            if (_instance != null)
                _instance.Show(target, point, amount, Healed);
        }

        private void Awake()
        {
            _instance = this;
            for (int i = Capacity - 1; i >= 0; i--)
                _free.Push(i);
            for (int q = 0; q < Capacity * MaxDigits; q++)
            {
                int v = q * 4;
                int t = q * 6;
                _triangles[t] = v;
                _triangles[t + 1] = v + 1;
                _triangles[t + 2] = v + 2;
                _triangles[t + 3] = v;
                _triangles[t + 4] = v + 2;
                _triangles[t + 5] = v + 3;
            }

            _mesh = new Mesh { name = "DamageNumbers" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = new Material(_material) { mainTexture = BuildFont() };
            meshRenderer.sortingOrder = SortingOrder;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Show(object target, Vector2 point, float amount, Color32 color)
        {
            if (amount <= 0f)
                return;

            if (_latest.TryGetValue(target, out int last))
            {
                ref Entry e = ref _entries[last];
                if (e.Active && e.Age < MergeWindow && e.Color.Equals(color))
                {
                    e.Amount += amount;
                    return;
                }
            }
            // 꽉 차면 버린다 — 숫자 하나 빠지는 게 프레임이 무거워지는 것보다 낫다
            if (_free.Count == 0)
                return;

            int index = _free.Pop();
            _entries[index] = new Entry
            {
                Active = true,
                Target = target,
                Position = point + new Vector2(Random.Range(-JitterX, JitterX), 0f),
                Amount = amount,
                Color = color,
            };
            _latest[target] = index;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            int quads = 0;
            for (int i = 0; i < Capacity; i++)
            {
                ref Entry e = ref _entries[i];
                if (!e.Active)
                    continue;
                e.Age += dt;
                if (e.Age >= Lifetime)
                {
                    e.Active = false;
                    if (_latest.TryGetValue(e.Target, out int last) && last == i)
                        _latest.Remove(e.Target);
                    e.Target = null;
                    _free.Push(i);
                    continue;
                }
                quads = WriteNumber(ref e, quads);
            }

            if (quads == 0 && _drawnQuads == 0)
                return;
            _drawnQuads = quads;
            _mesh.Clear(true);
            _mesh.SetVertices(_vertices, 0, quads * 4);
            _mesh.SetUVs(0, _uvs, 0, quads * 4);
            _mesh.SetColors(_colors, 0, quads * 4);
            _mesh.SetTriangles(_triangles, 0, quads * 6, 0, false);
            // 카메라에 잘리지 않게 넉넉히 — 정점이 월드 좌표라 계산할 필요가 없다
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 1f));
        }

        // 숫자 하나를 quad 칸들에 쓴다 — 다음 빈 quad 번호를 돌려준다
        private int WriteNumber(ref Entry e, int quad)
        {
            int value = Mathf.Clamp(Mathf.RoundToInt(e.Amount), 1, MaxValue);
            int digits = 1;
            for (int v = value; v >= 10; v /= 10)
                digits++;

            float t = e.Age / Lifetime;
            float rise = RiseHeight * (1f - (1f - t) * (1f - t));
            Color32 color = e.Color;
            color.a = (byte)(255f * (t < FadeFrom ? 1f : 1f - (t - FadeFrom) / (1f - FadeFrom)));

            float width = (digits * Advance + 1) * Pixel;
            float left = e.Position.x - width * 0.5f;
            float bottom = e.Position.y + rise;
            float w = CellW * Pixel;
            float h = CellH * Pixel;
            const float glyphU = 1f / 10f;

            // 오른쪽 자리부터
            for (int d = digits - 1; d >= 0; d--, value /= 10)
            {
                float x = left + d * Advance * Pixel;
                float u = (value % 10) * glyphU;
                int v = quad * 4;
                _vertices[v] = new Vector3(x, bottom, 0f);
                _vertices[v + 1] = new Vector3(x, bottom + h, 0f);
                _vertices[v + 2] = new Vector3(x + w, bottom + h, 0f);
                _vertices[v + 3] = new Vector3(x + w, bottom, 0f);
                _uvs[v] = new Vector2(u, 0f);
                _uvs[v + 1] = new Vector2(u, 1f);
                _uvs[v + 2] = new Vector2(u + glyphU, 1f);
                _uvs[v + 3] = new Vector2(u + glyphU, 0f);
                _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = color;
                quad++;
            }
            return quad;
        }

        // 흰 숫자 + 검은 테두리(정점 색을 곱해도 테두리는 검게 남는다). 숫자 10개를 가로로 붙인 50×7
        private static Texture2D BuildFont()
        {
            int texW = CellW * 10;
            var pixels = new Color32[texW * CellH];
            var clear = new Color32(0, 0, 0, 0);
            var outline = new Color32(0, 0, 0, 255);
            var fill = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            for (int g = 0; g < 10; g++)
            {
                // 테두리 먼저, 그 위에 숫자
                for (int pass = 0; pass < 2; pass++)
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                        {
                            if (Glyphs[g][row * 3 + col] != '1')
                                continue;
                            int cx = g * CellW + 1 + col;
                            int cy = CellH - 2 - row;
                            if (pass == 1)
                            {
                                pixels[cy * texW + cx] = fill;
                                continue;
                            }
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                    pixels[(cy + dy) * texW + cx + dx] = outline;
                        }
            }

            var texture = new Texture2D(texW, CellH, TextureFormat.RGBA32, false)
            {
                name = "DamageNumbersFont",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

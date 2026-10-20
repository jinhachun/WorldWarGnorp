using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 내 국가의 테두리(사용자 10/15) — 킹덤 벽돌 · 티크톡 리벳 철판과 톱니 · 트라이브 통나무와 끈. 국가를 고르기 전에는 원래 그림 그대로.
    // 국가 고르기 카드는 ShowNation으로 보고 있는 국가의 테두리를 씌운다.
    // 조각 그림(WorldConfig 「UI 테두리 조각」 — 변 한 토막 · 왼쪽 위 모서리)을 이 판의 크기 그대로 이어 붙인 그림 한 장을 자식 Image로 깐다.
    // 9-slice · 늘려 쓰기 없이(§5-1) Simple + 원본 크기 — 크기가 바뀌면(툴팁 높이) 다시 만든다. 같은 (국가 · 모양 · 크기)는 한 번만 만든다.
    //   Box = 판 네 변 · Top = 가로 띠 하나(화면 위 줄 아래 선) · Side = 세로 띠 하나(왼쪽 패널 옆 선) — 띠는 이 오브젝트 가운데에 걸친다
    public class NationFrame : MonoBehaviour
    {
        public enum Shape { Box, Top, Side }

        // 조각 그림 1px = 화면 2px
        private const int ArtPixel = 2;

        [SerializeField] private Shape _shape;
        [Tooltip("이 그래픽의 색을 따라 칠한다(업그레이드 카드의 등급 색 · 강조). 비우면 흰색")]
        [SerializeField] private Graphic _tintFrom;
        [Tooltip("테두리를 깔면 끄는 원래 그래픽(흰 선 · 테두리 그림) — 국가를 고르기 전에는 그대로 둔다")]
        [SerializeField] private Graphic _replaces;

        private static readonly Dictionary<(BuildingNation, Shape, int, int), Sprite> Built = new Dictionary<(BuildingNation, Shape, int, int), Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Built.Clear();

        private const string ImageName = "NationFrame";

        private Image _image;
        private (BuildingNation, Shape, int, int) _shown;
        // 내 국가 대신 이 국가로(국가 고르기 카드 — 보고 있는 국가). 없으면 내 국가
        private BuildingNation? _nation;

        public void ShowNation(BuildingNation nation) => _nation = nation;

        private void LateUpdate()
        {
            bool themed = _nation != null || RunState.Started && RunState.NationChosen;
            BuildingNation nation = _nation ?? (themed ? RunState.Nation : BuildingNation.Common);
            WorldConfig world = RunState.Config != null ? RunState.Config.World : null;
            Sprite edge = world != null ? world.FrameEdge(nation) : null;
            // 국가를 고르기 전이거나 조각이 없으면 원래 그림 그대로
            if (edge == null)
            {
                Show(false);
                return;
            }
            Vector2 rect = ((RectTransform)transform).rect.size;
            int thick = (int)edge.rect.height * ArtPixel;
            int w = _shape == Shape.Side ? thick : Mathf.Max(1, Mathf.RoundToInt(rect.x));
            int h = _shape == Shape.Top ? thick : Mathf.Max(1, Mathf.RoundToInt(rect.y));
            var key = (nation, _shape, w, h);
            // 복제된 판(툴팁의 둘째 유닛 판 등)은 원본이 만든 그림 자식까지 복사돼 온다 — 새로 하나 더 만들지 말고 그걸 쓴다(옛 크기로 남아 빈 테두리가 겹쳐 보이던 것, 사용자 10/21)
            if (_image == null && transform.Find(ImageName) is Transform copied && copied.TryGetComponent(out Image existing))
                _image = existing;
            if (_image == null)
            {
                var go = new GameObject(ImageName, typeof(RectTransform), typeof(Image));
                _image = go.GetComponent<Image>();
                _image.rectTransform.SetParent(transform, false);
                _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _image.raycastTarget = false;
                _image.preserveAspect = true;
            }
            if (_image.transform.GetSiblingIndex() != transform.childCount - 1)
                _image.transform.SetAsLastSibling();
            if (!key.Equals(_shown) || _image.sprite == null)
            {
                _shown = key;
                if (!Built.TryGetValue(key, out Sprite sprite))
                    Built[key] = sprite = Build(edge, world.FrameCorner(nation), _shape, w, h);
                _image.sprite = sprite;
                _image.SetNativeSize();
            }
            Show(true);
            _image.color = _tintFrom != null ? new Color(_tintFrom.color.r, _tintFrom.color.g, _tintFrom.color.b, 1f) : Color.white;
        }

        private void Show(bool on)
        {
            if (_image != null && _image.enabled != on)
                _image.enabled = on;
            if (_replaces != null && _replaces.enabled == on)
                _replaces.enabled = !on;
        }

        // 판 크기(w × h 화면 px)의 테두리 그림 — 변 조각을 바깥에서 안쪽으로 이어 붙이고(끝 토막은 잘린다), 네 모서리에 모서리 조각을 뒤집어 놓는다
        private static Sprite Build(Sprite edge, Sprite corner, Shape shape, int w, int h)
        {
            var pixels = new Color32[w * h];
            Color32[] e = Read(edge, out int ew, out int eh);
            int cw = 0, ch = 0;
            Color32[] c = corner != null ? Read(corner, out cw, out ch) : null;
            // 변 조각의 (토막 안 자리, 바깥에서 몇째 줄) — 줄 0이 그림 맨 윗줄
            Color32 Edge(int along, int depth) => e[(eh - 1 - depth) * ew + along % ew];

            int cornerW = c != null ? cw * ArtPixel : 0, cornerH = c != null ? ch * ArtPixel : 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int top = (h - 1 - y) / ArtPixel, bottom = y / ArtPixel, left = x / ArtPixel, right = (w - 1 - x) / ArtPixel;
                    Color32 p = default;
                    switch (shape)
                    {
                        case Shape.Top:
                            p = Edge(left, top);
                            break;
                        case Shape.Side:
                            p = Edge(bottom, left);
                            break;
                        default:
                            if (c != null && (x < cornerW || x >= w - cornerW) && (y < cornerH || y >= h - cornerH))
                            {
                                // 모서리 — 왼쪽 위 조각을 그 모서리 쪽으로 뒤집어서
                                int cx = x < cornerW ? left : right, cy = y >= h - cornerH ? top : bottom;
                                p = c[(ch - 1 - cy) * cw + cx];
                            }
                            else if (top < eh)
                                p = Edge(left, top);
                            else if (bottom < eh)
                                p = Edge(left, bottom);
                            else if (left < eh)
                                p = Edge(bottom, left);
                            else if (right < eh)
                                p = Edge(bottom, right);
                            break;
                    }
                    pixels[y * w + x] = p;
                }
            return ToSprite(pixels, w, h);
        }

        private static Color32[] Read(Sprite sprite, out int w, out int h)
        {
            Rect r = sprite.rect;
            w = (int)r.width;
            h = (int)r.height;
            Color[] colors = sprite.texture.GetPixels((int)r.x, (int)r.y, w, h);
            var result = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                result[i] = colors[i];
            return result;
        }

        // 캔버스 기준(그림 1px = 100 ÷ 100)이라 원본 크기(SetNativeSize) = 판 크기
        private static Sprite ToSprite(Color32[] pixels, int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

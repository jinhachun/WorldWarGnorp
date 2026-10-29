using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 게임 화면을 16:9 영역에만 그린다 — 남는 곳(위아래 또는 좌우)은 검은 띠(사용자 10/30)
    // 카메라는 rect를 그 영역으로 줄이고, Overlay 캔버스(카메라 rect를 무시한다)는 맨 위 검은 띠 넷이 덮는다.
    // 캔버스 UI는 CanvasScaler(1920×1080 · Expand)의 `Canvas/Screen` 판이 이미 같은 영역에 놓인다.
    // 창 크기가 바뀐 프레임에 다른 Update보다 먼저 맞춘다(창 끌어 조절 — 사용자 10/30)
    [DefaultExecutionOrder(-1000)]
    public class Letterbox : MonoBehaviour
    {
        private const float Aspect = 16f / 9f;

        // 16:9 영역(화면 px, 왼쪽 아래 원점) — 화면 밖 판정 · 띄우는 판 자리는 Screen.width/height 대신 이것으로
        // (아직 안 정했으면 — 에디트 모드 — 화면 전체)
        public static Rect Rect => _rect.width > 0f ? _rect : new Rect(0f, 0f, Screen.width, Screen.height);
        private static Rect _rect;

        private readonly RectTransform[] _bars = new RectTransform[4];
        private Vector2Int _size;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Spawn()
        {
            var host = new GameObject("Letterbox");
            DontDestroyOnLoad(host);
            host.AddComponent<Letterbox>();
        }

        private void Awake()
        {
            // 치트 패널 가리개(short.MaxValue) 바로 아래 — 그 밖의 모든 캔버스 위. 띠가 그 자리 클릭도 막는다
            var canvas = new GameObject("Bars", typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            for (int i = 0; i < _bars.Length; i++)
            {
                var image = new GameObject("Bar", typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(canvas.transform, false);
                image.color = Color.black;
                _bars[i] = image.rectTransform;
                _bars[i].offsetMin = _bars[i].offsetMax = Vector2.zero;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        // 씬의 Start보다 먼저 — 카메라 aspect를 읽는 계산(끌기 끝 · 확대 한계)이 16:9 값을 받는다
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        private void Update()
        {
            if (_size.x != Screen.width || _size.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            _size = new Vector2Int(Screen.width, Screen.height);
            float w = _size.x, h = _size.y;
            float width = Mathf.Min(w, Mathf.Round(h * Aspect)), height = Mathf.Min(h, Mathf.Round(w / Aspect));
            _rect = new Rect(Mathf.Round((w - width) * 0.5f), Mathf.Round((h - height) * 0.5f), width, height);
            var view = new Rect(_rect.x / w, _rect.y / h, _rect.width / w, _rect.height / h);

            // 왼쪽 · 오른쪽 · 아래 · 위
            Place(_bars[0], new Vector2(0f, 0f), new Vector2(view.xMin, 1f));
            Place(_bars[1], new Vector2(view.xMax, 0f), new Vector2(1f, 1f));
            Place(_bars[2], new Vector2(0f, 0f), new Vector2(1f, view.yMin));
            Place(_bars[3], new Vector2(0f, view.yMax), new Vector2(1f, 1f));

            foreach (Camera cam in Camera.allCameras)
                if (cam.targetTexture == null)
                    cam.rect = view;
        }

        private static void Place(RectTransform bar, Vector2 min, Vector2 max)
        {
            bar.anchorMin = min;
            bar.anchorMax = max;
        }
    }
}

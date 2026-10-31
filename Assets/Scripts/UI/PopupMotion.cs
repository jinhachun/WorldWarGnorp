using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 팝업 공통 열림 · 닫힘(Docs/팝업_이벤트화면_계획.md K1) — 판(panel)에 붙인다. 실제 시간(timeScale 0에서도 돈다).
    //   열림 = 어둠 3계단 짙어짐 + 판이 위에서 3계단 내려앉으며 3계단으로 나타남(이벤트 방문 · 닉네임 · 결과 창의 문법)
    //   닫힘 = 판이 2계단 올라가며 흐려지고 어둠도 계단으로 옅어진 뒤 closed를 부른다(끄는 건 부르는 쪽이)
    // 움직임은 UI 픽셀 칸(1칸 = 3px) 단위. 값은 BattleConfig 「손맛 — 팝업 열림 · 닫힘」
    public class PopupMotion : MonoBehaviour
    {
        private const int OpenSteps = 3, CloseSteps = 2;

        private RectTransform _panel;
        private CanvasGroup _group;
        private Graphic _dim;
        private float _dimAlpha;
        private Vector2 _home;
        // 지난 실제 시간 — 열리는 중 양수 · 닫히는 중 음수 쪽 시계를 따로 둔다(-1 = 안 움직임)
        private float _openClock = -1f, _closeClock = -1f;
        private bool _dimOpening;
        private System.Action _closed;

        // 열려 있거나 열리는 중(닫히는 중 · 닫힘이면 거짓)
        public bool Shown { get; private set; }
        public bool Opening => _openClock >= 0f;
        public bool Closing => _closeClock >= 0f;

        // panel에 붙인다 — 제자리는 지금 자리. dim은 없어도 된다(알파 목표는 지금 색의 알파)
        public static PopupMotion Attach(RectTransform panel, Graphic dim = null)
        {
            PopupMotion motion = panel.gameObject.AddComponent<PopupMotion>();
            motion._panel = panel;
            motion._home = panel.anchoredPosition;
            motion._group = panel.GetComponent<CanvasGroup>();
            if (motion._group == null)
                motion._group = panel.gameObject.AddComponent<CanvasGroup>();
            motion._dim = dim;
            motion._dimAlpha = dim != null ? dim.color.a : 0f;
            return motion;
        }

        // 연다 — 이미 보이는 판(열림 · 열리는 중)이면 판만 다시 내려앉는다(어둠은 그대로). 닫히는 중이면 닫힘을 취소한다
        public void Open()
        {
            // 소리 자리(J6): 팝업 열림
            _dimOpening = !Shown;
            Shown = true;
            _closed = null;
            _closeClock = -1f;
            _openClock = 0f;
            _group.blocksRaycasts = true;
            Apply();
        }

        // 닫는다 — 끝나면 closed. 열리는 중이면 지금 자리에서 바로 닫기로 넘어간다. 닫히는 동안 판은 누르기를 받지 않는다(뒤로 지나간다)
        public void Close(System.Action closed)
        {
            if (Closing)
                return;
            // 소리 자리(J6): 팝업 닫힘
            Shown = false;
            _closed = closed;
            _openClock = -1f;
            _closeClock = 0f;
            _group.blocksRaycasts = false;
            if (_dim != null)
                _dim.raycastTarget = false;
            Apply();
        }

        // 닫히는 중이면 바로 끝낸다(closed를 지금 부른다) — 닫히는 판에 새 내용을 채우기 전에 · 건너뛰기
        public void Complete()
        {
            if (!Closing)
                return;
            _closeClock = CloseSeconds;
            Apply();
        }

        private void Update()
        {
            if (!Opening && !Closing)
                return;
            // 한 프레임이 길어도 계단이 건너뛰지 않게 묶는다
            float dt = Mathf.Min(Juice.RealDeltaTime, 1f / 30f);
            if (Opening)
                _openClock += dt;
            else
                _closeClock += dt;
            Apply();
        }

        private void Apply()
        {
            if (Opening)
            {
                _panel.anchoredPosition = _home + new Vector2(0f, OpenOffset(_openClock));
                _group.alpha = OpenAlpha(_openClock);
                if (_dim != null)
                {
                    _dim.raycastTarget = true;
                    SetDim(_dimOpening ? OpenAlpha(_openClock) : 1f);
                }
                if (_openClock >= OpenSeconds)
                    _openClock = -1f;
                return;
            }
            if (!Closing)
                return;
            int step = Mathf.Min(CloseSteps, Mathf.FloorToInt(_closeClock / (CloseSeconds / CloseSteps)) + 1);
            float rise = Mathf.Round(Config(c => c.PopupRise, 4f) * Intensity * step / CloseSteps) * UiHitRings.Pixel;
            _panel.anchoredPosition = _home + new Vector2(0f, rise);
            float alpha = (CloseSteps + 1f - step) / (CloseSteps + 1f);   // 2/3 → 1/3
            _group.alpha = alpha;
            SetDim(alpha);
            if (_closeClock < CloseSeconds)
                return;
            _closeClock = -1f;
            _panel.anchoredPosition = _home;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            SetDim(1f);
            if (_dim != null)
                _dim.raycastTarget = true;
            System.Action closed = _closed;
            _closed = null;
            closed?.Invoke();
        }

        private void SetDim(float share)
        {
            if (_dim != null)
                _dim.color = new Color(_dim.color.r, _dim.color.g, _dim.color.b, _dimAlpha * share);
        }

        // ── 열림 계단(판 하나에 붙이지 않는 곳 — 지형 배너 — 도 같이 쓴다). t = 열린 뒤 실제 시간
        public static float OpenSeconds => Config(c => c.PopupOpenSeconds, 0.12f);
        private static float CloseSeconds => Config(c => c.PopupCloseSeconds, 0.08f);
        private static float Intensity => Config(c => c.JuiceIntensity, 1f);

        private static int OpenStep(float t) => Mathf.Clamp(Mathf.FloorToInt(t / (OpenSeconds / OpenSteps)), 0, OpenSteps);

        // 위로 떠 있는 높이(px) — 칸 단위 계단 3번으로 0까지
        public static float OpenOffset(float t)
            => Mathf.Round(Config(c => c.PopupDrop, 6f) * Intensity * (OpenSteps - OpenStep(t)) / OpenSteps) * UiHitRings.Pixel;

        // 알파 — 1/3 · 2/3 · 1
        public static float OpenAlpha(float t) => Mathf.Min(1f, (OpenStep(t) + 1f) / OpenSteps);

        private static float Config(System.Func<BattleConfig, float> read, float fallback)
            => RunState.Config != null ? read(RunState.Config) : fallback;
    }
}

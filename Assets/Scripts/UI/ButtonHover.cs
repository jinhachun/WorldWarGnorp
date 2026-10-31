using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 버튼 호버 강조 — 카드·탑 칸의 창틀과 같은 결: 평소엔 판(테두리)이 한 단계 어둡고, 마우스를 올리면 제 밝기.
    // 못 누르는 버튼은 강조하지 않는다. 글·아이콘 색은 건드리지 않는다
    // 상태마다 그림이 따로 있는 버튼(지도의 명령 버튼 — Button의 Sprite Swap)은 _idle = 1로 두고, 누르는 동안 글 줄(첫 자식)을 _pressDepth만큼 내린다(눌린 그림의 얼굴이 받침만큼 내려가므로)
    // 손맛(J3 — Docs/폴리싱_계획.md): 올리면 UI 픽셀 칸만큼 떠오르고 · 누르면 내려앉았다 놓으면 탄성으로 튀어 오르고 · 못 누르는 버튼을 누르면 좌우로 흔들린다.
    // 버튼 자리(anchoredPosition)에 칸 단위 어긋남을 얹는다 — 다른 코드가 자리를 다시 놓으면 그 자리를 새 제자리로 삼는다. 값은 BattleConfig 「손맛 — 상점 · UI」
    [RequireComponent(typeof(Button))]
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("마우스를 올리지 않았을 때 판 밝기 — 흰 판 버튼(전투 시작 등)은 회색으로 죽어 보이지 않게 덜 어둡게")]
        [SerializeField] private float _idle = 0.65f;
        [Tooltip("누르는 동안 첫 자식(글 줄)을 내리는 거리(px) — 0이면 안 움직인다")]
        [SerializeField] private float _pressDepth;

        private Button _button;
        private bool _hovered;
        private bool _pressed;

        // 손맛 — 지금 얹은 어긋남(px) · 그걸 얹어 쓴 자리 · 떠오른 높이의 출발값과 시계 · 놓은 뒤 튐인가 · 흔들림 시계
        private RectTransform _rect;
        private Vector2 _applied;
        private Vector2 _written;
        private bool _wrote;
        private float _liftFrom, _liftTo, _liftAge = float.MaxValue;
        private bool _bounce;
        private float _shakeAge = float.MaxValue;
        private Vector4 _padding;

        // 판의 제 색 — 색을 바꾸는 쪽(판매 칸의 놓는 자리 노랑)은 판 색을 직접 쓰지 말고 여기에 쓴다
        public Color Base { get; set; } = Color.white;

        public bool Hovered => _hovered;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rect = (RectTransform)transform;
            _padding = _button.image != null ? _button.image.raycastPadding : Vector4.zero;
        }

        private void OnDisable()
        {
            _hovered = false;
            Press(false);
            // 얹은 어긋남을 걷는다
            if (_wrote && _rect.anchoredPosition == _written)
                _rect.anchoredPosition -= _applied;
            _applied = Vector2.zero;
            _wrote = false;
            _liftFrom = _liftTo = 0f;
            _liftAge = _shakeAge = float.MaxValue;
            if (_button.image != null)
                _button.image.raycastPadding = _padding;
        }

        private void LateUpdate()
        {
            float k = _hovered && _button.interactable ? 1f : _idle;
            _button.image.color = new Color(Base.r * k, Base.g * k, Base.b * k, Base.a);
            Move();
        }

        // 떠오름 · 눌림 · 흔들림 — 칸(UiHitRings.Pixel) 단위로 끊어 버튼 자리에 얹는다
        private void Move()
        {
            BattleConfig config = RunState.Config;
            float dt = Juice.RealDeltaTime;
            float lift = 0f;
            float shake = 0f;
            if (config != null)
            {
                float intensity = config.JuiceIntensity;
                // 갈 높이 — 누르는 중 = 내려앉음(그림이 눌림을 보이는 버튼은 제자리) · 올린 채 = 떠오름 · 나머지 = 제자리
                float target = _pressed ? (_pressDepth > 0f ? 0f : -config.ButtonSink) : _hovered && _button.interactable ? config.ButtonRise : 0f;
                target = Mathf.Round(target * intensity);
                if (target != _liftTo)
                {
                    // 눌렸다 놓으면 탄성으로 튀어 오른다 — 그 밖의 오르내림은 짧게 감속
                    _liftFrom = Mathf.LerpUnclamped(_liftFrom, _liftTo, LiftCurve(_liftAge));
                    _bounce = _liftTo < 0f && target >= 0f;
                    _liftTo = target;
                    _liftAge = 0f;
                }
                _liftAge += dt;
                lift = Mathf.Round(Mathf.LerpUnclamped(_liftFrom, _liftTo, LiftCurve(_liftAge)));

                // 못 누르는 버튼을 눌렀다 — 좌우로 엇갈리며 잦아든다
                _shakeAge += dt;
                float s = _shakeAge / Mathf.Max(0.001f, config.ButtonDeniedSeconds);
                if (s < 1f)
                {
                    float amp = Mathf.Round(config.ButtonDeniedShake * intensity * (1f - s));
                    shake = (int)(_shakeAge * 30f) % 2 == 0 ? amp : -amp;
                }
            }

            var offset = new Vector2(shake, lift) * UiHitRings.Pixel;
            // 다른 코드가 자리를 다시 놓았으면(배치 코드 · 레이아웃) 그 자리가 새 제자리다
            Vector2 home = _wrote && _rect.anchoredPosition == _written ? _rect.anchoredPosition - _applied : _rect.anchoredPosition;
            _rect.anchoredPosition = home + offset;
            _written = _rect.anchoredPosition;
            _applied = offset;
            _wrote = true;
            // 떠오른 만큼 아래로 누를 자리를 늘린다 — 아래 끝에 걸친 마우스가 떠오름 · 내려옴을 되풀이하지 않게
            if (_button.image != null)
                _button.image.raycastPadding = _padding + new Vector4(0f, -Mathf.Max(0f, offset.y), 0f, 0f);
        }

        private float LiftCurve(float age)
        {
            BattleConfig config = RunState.Config;
            float seconds = config != null ? Mathf.Max(0.001f, config.ButtonBounceSeconds) : 0.2f;
            // 튐 = 넘쳤다 앉음 · 나머지는 튐 시간의 절반 동안 감속
            return _bounce ? Juice.OutBack(age / seconds, config != null ? config.ButtonOvershoot : 0f) : Juice.OutQuad(age / (seconds * 0.5f));
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            Press(_button.interactable);
            if (!_button.interactable)
                _shakeAge = 0f;   // 소리 자리(J6): 못 누르는 버튼
            // 소리 자리(J6): 버튼 누름(누를 수 있을 때)
        }

        public void OnPointerUp(PointerEventData eventData) => Press(false);

        private void Press(bool on)
        {
            if (_pressed == on)
                return;
            _pressed = on;
            if (_pressDepth == 0f || transform.childCount == 0)
                return;
            var label = (RectTransform)transform.GetChild(0);
            label.anchoredPosition += new Vector2(0f, on ? -_pressDepth : _pressDepth);
        }
    }
}

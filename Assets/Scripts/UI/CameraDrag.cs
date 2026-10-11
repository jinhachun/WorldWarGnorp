using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GnorpWar
{
    // 전장을 끌어 화면을 옮기고 마우스 휠로 확대 · 축소한다(사용자 10/11). 놓으면 관성으로 미끄러지고, 맵 끝(_minX~_maxX)과 땅 아래가 화면 밖으로 안 나간다.
    // 확대는 가로로는 마우스가 가리키는 곳, 세로로는 화면 아래 끝을 기준으로 — 가장 넓게는 전장 전체가 한 화면, 가장 가깝게는 _closestSize. 화면 아래 끝(땅)은 어느 배율에서도 처음 자리보다 내려가지 않는다.
    // 확대하면 위아래로도 끌 수 있다. UI 위에서는 끌기도 휠도 받지 않는다.
    // 카메라의 부모(CameraRig)에 붙인다 — 흔들림(CameraShake)은 자식 카메라의 localPosition만 건드리므로 서로 안 겹친다.
    public class CameraDrag : MonoBehaviour
    {
        private const float GlideDecayPerSecond = 4f;
        // 휠 한 칸에 화면 크기가 바뀌는 비율
        private const float ZoomPerNotch = 0.88f;
        // 휠 한 칸의 입력 값(Windows) — 이보다 훨씬 작은 값을 주는 장치는 값 그대로 칸 수로 읽는다
        private const float NotchUnits = 120f;

        [SerializeField] private Camera _camera;
        [SerializeField] private float _minX = -72f;
        [SerializeField] private float _maxX = 72f;
        [Tooltip("가장 가깝게 확대했을 때의 카메라 크기(화면 높이의 절반, 유닛) — 씬의 카메라 크기가 처음 배율이다")]
        [SerializeField] private float _closestSize = 10f;

        private bool _dragging;
        private Vector2 _lastPointer;
        private Vector2 _velocity;
        // 화면 아래 끝이 내려갈 수 있는 한계(처음 화면의 아래 끝)
        private float _bottom;

        private void Start()
        {
            _bottom = transform.position.y - _camera.orthographicSize;
            // 판은 아군 기지 쪽 화면에서 시작
            SetPosition(new Vector2(_minX + HalfWidth, transform.position.y));
        }

        private float HalfWidth => _camera.orthographicSize * _camera.aspect;
        // 전장 전체가 가로로 딱 들어오는 크기
        private float WidestSize => (_maxX - _minX) * 0.5f / _camera.aspect;

        private void LateUpdate()
        {
            Pointer pointer = Pointer.current;
            float dt = Time.unscaledDeltaTime; // 히트스톱·배속 중에도 손에 붙게
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            Mouse mouse = Mouse.current;
            if (mouse != null && !overUi)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                    Zoom(Mathf.Abs(scroll) >= NotchUnits * 0.5f ? scroll / NotchUnits : scroll, mouse.position.ReadValue());
            }

            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                _dragging = !overUi;
                _lastPointer = pointer.position.ReadValue();
                _velocity = Vector2.zero;
            }

            if (_dragging && pointer != null && pointer.press.isPressed)
            {
                Vector2 at = pointer.position.ReadValue();
                float worldPerPixel = _camera.orthographicSize * 2f / Screen.height;
                Vector2 delta = -(at - _lastPointer) * worldPerPixel;
                _lastPointer = at;
                if (dt > 0f)
                    _velocity = delta / dt;
                SetPosition((Vector2)transform.position + delta);
                return;
            }

            _dragging = false;
            if (_velocity == Vector2.zero)
                return;
            // 감속을 dt 안에서 적분 — 프레임이 느려도 미끄러지는 총거리(속도 ÷ 감속)가 같다
            float decay = Mathf.Exp(-GlideDecayPerSecond * dt);
            SetPosition((Vector2)transform.position + _velocity * (1f - decay) / GlideDecayPerSecond);
            _velocity *= decay;
            if (_velocity.sqrMagnitude < 0.05f * 0.05f)
                _velocity = Vector2.zero;
        }

        // notches > 0이면 확대. 가로는 화면의 screen 자리에 있던 곳이 확대 뒤에도 그 자리에 있게, 세로는 화면 아래 끝이 가리키는 높이가 그대로이게 카메라를 옮긴다
        // (사용자 10/11: 확대해도 땅 — 싸우는 곳 — 이 화면 아래에 그대로 붙어 있게)
        private void Zoom(float notches, Vector2 screen)
        {
            float size = Mathf.Clamp(_camera.orthographicSize * Mathf.Pow(ZoomPerNotch, notches), _closestSize, Mathf.Max(_closestSize, WidestSize));
            if (size == _camera.orthographicSize)
                return;
            // 화면 가운데에서 그 자리까지(화면 높이의 절반 = 1)
            float offset = (screen.x / Screen.width - 0.5f) * 2f * _camera.aspect;
            float worldX = transform.position.x + offset * _camera.orthographicSize;
            float bottom = transform.position.y - _camera.orthographicSize;
            _camera.orthographicSize = size;
            _velocity = Vector2.zero;
            SetPosition(new Vector2(worldX - offset * size, bottom + size));
        }

        private void SetPosition(Vector2 position)
        {
            float half = HalfWidth, size = _camera.orthographicSize;
            // 위로는 가장 넓은 화면의 위 끝까지
            float top = _bottom + Mathf.Max(size, WidestSize) * 2f;
            var clamped = new Vector2(Mathf.Clamp(position.x, _minX + half, Mathf.Max(_minX + half, _maxX - half)), Mathf.Clamp(position.y, _bottom + size, top - size));
            if (clamped.x != position.x)
                _velocity.x = 0f;
            if (clamped.y != position.y)
                _velocity.y = 0f;
            transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
        }
    }
}

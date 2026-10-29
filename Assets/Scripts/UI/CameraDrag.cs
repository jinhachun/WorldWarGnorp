using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GnorpWar
{
    // 전장을 좌우로 끌어 화면을 옮기고 마우스 휠로 확대 · 축소한다(사용자 10/11 · 끌기는 좌우만 10/12). 놓으면 관성으로 미끄러지고, 맵 끝(_minX~_maxX)이 화면 밖으로 안 나간다.
    // 확대는 가로로는 마우스가 가리키는 곳, 세로로는 화면 아래 끝을 기준으로 — 가장 넓게는 전장 전체가 한 화면, 가장 가깝게는 _closestSize.
    // 화면 아래 끝(땅)은 어느 배율에서도 처음 자리 그대로다. UI 위에서는 끌기도 휠도 받지 않는다.
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
        private float _lastPointer;
        private float _velocity;
        // 화면 아래 끝의 높이(처음 화면의 아래 끝) — 끌어도 확대해도 그대로다
        private float _bottom;

        private void Start()
        {
            _bottom = transform.position.y - _camera.orthographicSize;
            // 판은 아군 기지 쪽 화면에서 시작
            SetPosition(_minX + HalfWidth);
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
                    Zoom(Mathf.Abs(scroll) >= NotchUnits * 0.5f ? scroll / NotchUnits : scroll, mouse.position.ReadValue().x);
            }

            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                _dragging = !overUi;
                _lastPointer = pointer.position.ReadValue().x;
                _velocity = 0f;
            }

            if (_dragging && pointer != null && pointer.press.isPressed)
            {
                float at = pointer.position.ReadValue().x;
                float worldPerPixel = _camera.orthographicSize * 2f / _camera.pixelHeight;
                float delta = -(at - _lastPointer) * worldPerPixel;
                _lastPointer = at;
                if (dt > 0f)
                    _velocity = delta / dt;
                SetPosition(transform.position.x + delta);
                return;
            }

            _dragging = false;
            if (_velocity == 0f)
                return;
            // 감속을 dt 안에서 적분 — 프레임이 느려도 미끄러지는 총거리(속도 ÷ 감속)가 같다
            float decay = Mathf.Exp(-GlideDecayPerSecond * dt);
            SetPosition(transform.position.x + _velocity * (1f - decay) / GlideDecayPerSecond);
            _velocity *= decay;
            if (Mathf.Abs(_velocity) < 0.05f)
                _velocity = 0f;
        }

        // notches > 0이면 확대. 가로는 화면의 screenX 자리에 있던 곳이 확대 뒤에도 그 자리에 있게, 세로는 화면 아래 끝이 가리키는 높이가 그대로이게 카메라를 옮긴다
        // (사용자 10/11: 확대해도 땅 — 싸우는 곳 — 이 화면 아래에 그대로 붙어 있게)
        private void Zoom(float notches, float screenX)
        {
            float size = Mathf.Clamp(_camera.orthographicSize * Mathf.Pow(ZoomPerNotch, notches), _closestSize, Mathf.Max(_closestSize, WidestSize));
            if (size == _camera.orthographicSize)
                return;
            // 화면 가운데에서 그 자리까지(화면 높이의 절반 = 1)
            float offset = ((screenX - _camera.pixelRect.x) / _camera.pixelWidth - 0.5f) * 2f * _camera.aspect;
            float worldX = transform.position.x + offset * _camera.orthographicSize;
            _camera.orthographicSize = size;
            _velocity = 0f;
            SetPosition(worldX - offset * size);
        }

        // 가로 자리를 맵 끝 안에 묶어 옮긴다 — 세로는 늘 화면 아래 끝이 _bottom에 오는 높이
        private void SetPosition(float x)
        {
            float half = HalfWidth;
            float clamped = Mathf.Clamp(x, _minX + half, Mathf.Max(_minX + half, _maxX - half));
            if (clamped != x)
                _velocity = 0f;
            transform.position = new Vector3(clamped, _bottom + _camera.orthographicSize, transform.position.z);
        }
    }
}

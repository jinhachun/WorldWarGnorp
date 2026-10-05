using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GnorpWar
{
    // 전장을 좌우로 끌어 화면을 옮긴다. 놓으면 관성으로 미끄러지고, 맵 끝(_minX~_maxX)이 화면 밖으로 안 나간다.
    // 카메라의 부모(CameraRig)에 붙인다 — 흔들림(CameraShake)은 자식 카메라의 localPosition만 건드리므로 서로 안 겹친다.
    public class CameraDrag : MonoBehaviour
    {
        private const float GlideDecayPerSecond = 4f;

        [SerializeField] private Camera _camera;
        [SerializeField] private float _minX = -72f;
        [SerializeField] private float _maxX = 72f;

        private bool _dragging;
        private float _lastPointerX;
        private float _velocity;

        private void Start()
        {
            // 판은 아군 기지 쪽 화면에서 시작
            SetX(_minX + HalfWidth);
        }

        private float HalfWidth => _camera.orthographicSize * _camera.aspect;

        private void LateUpdate()
        {
            Pointer pointer = Pointer.current;
            float dt = Time.unscaledDeltaTime; // 히트스톱·배속 중에도 손에 붙게

            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                _dragging = !overUi;
                _lastPointerX = pointer.position.ReadValue().x;
                _velocity = 0f;
            }

            if (_dragging && pointer != null && pointer.press.isPressed)
            {
                float pointerX = pointer.position.ReadValue().x;
                float worldPerPixel = _camera.orthographicSize * 2f / Screen.height;
                float dx = -(pointerX - _lastPointerX) * worldPerPixel;
                _lastPointerX = pointerX;
                if (dt > 0f)
                    _velocity = dx / dt;
                SetX(transform.position.x + dx);
                return;
            }

            _dragging = false;
            if (_velocity == 0f)
                return;
            // 감속을 dt 안에서 적분 — 프레임이 느려도 미끄러지는 총거리(속도 ÷ 감속)가 같다
            float decay = Mathf.Exp(-GlideDecayPerSecond * dt);
            SetX(transform.position.x + _velocity * (1f - decay) / GlideDecayPerSecond);
            _velocity *= decay;
            if (Mathf.Abs(_velocity) < 0.05f)
                _velocity = 0f;
        }

        private void SetX(float x)
        {
            float half = HalfWidth;
            float clamped = Mathf.Clamp(x, _minX + half, _maxX - half);
            if (clamped != x)
                _velocity = 0f;
            Vector3 p = transform.position;
            transform.position = new Vector3(clamped, p.y, p.z);
        }
    }
}

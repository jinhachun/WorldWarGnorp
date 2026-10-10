using UnityEngine;

namespace GnorpWar
{
    // 뒷배경 한 겹(Ground가 만든다) — 카메라가 움직인 만큼의 Factor 배를 따라가 멀리 있는 것처럼 느리게 흐른다.
    // 카메라 끌기(CameraDrag.LateUpdate)보다 늦게 돌아야 한 프레임 밀리지 않는다
    [DefaultExecutionOrder(200)]
    public class BackdropParallax : MonoBehaviour
    {
        public float Factor;

        private Transform _camera;

        private void Start() => _camera = Camera.main.transform;

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            p.x = _camera.position.x * Factor;
            transform.position = p;
        }
    }
}

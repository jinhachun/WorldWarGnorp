using UnityEngine;

namespace GnorpWar
{
    // 충격(trauma)이 쌓였다가 잦아드는 화면 흔들림. 흔들림 크기는 충격의 제곱이라 약한 충격은 은은하다
    public class CameraShake : MonoBehaviour
    {
        private const float MaxOffset = 0.6f;
        private const float DecayPerSecond = 2.5f;

        private Vector3 _basePosition;
        private float _trauma;

        private void Awake()
        {
            _basePosition = transform.localPosition;
        }

        public void Add(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f)
                return;

            // 히트스톱 중에도 흔들리도록 실제 시간 기준
            _trauma = Mathf.Max(0f, _trauma - DecayPerSecond * Time.unscaledDeltaTime);
            float strength = _trauma * _trauma * MaxOffset;
            transform.localPosition = _basePosition + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * strength;
        }
    }
}

using UnityEngine;

namespace GnorpWar
{
    // 충격(trauma)이 쌓였다가 잦아드는 화면 흔들림. 흔들림 크기는 충격의 제곱이라 약한 충격은 은은하다
    public class CameraShake : MonoBehaviour
    {
        private const float MaxOffset = 0.6f;
        private const float DecayPerSecond = 2.5f;
        // 쌓일 수 있는 충격의 상한 — 긴 전투·배속에서 사망이 쉴 새 없이 쌓여도 이 이상 흔들리지 않는다(최대 흔들림 = 상한² × MaxOffset)
        private const float MaxTrauma = 0.4f;

        private Vector3 _basePosition;
        private float _trauma;

        private void Awake()
        {
            _basePosition = transform.localPosition;
        }

        public void Add(float amount)
        {
            _trauma = Mathf.Min(_trauma + amount, MaxTrauma);
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

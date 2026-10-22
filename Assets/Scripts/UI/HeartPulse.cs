using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 국가 위기(목숨 0)의 빈 하트 — 심장 박동처럼 두 번 콩닥 커졌다 줄고, 뛸 때마다 빨갛게 물든다(사용자 10/23).
    // 전투 결과 창은 timeScale 0이라 실제 시간으로 돈다. 꺼지면 원래 크기 · 색(Rest)으로 돌아간다
    public class HeartPulse : MonoBehaviour
    {
        // 한 번 뛰는 주기(초) · 두 번째 박동이 오는 때(초) · 박동 하나의 길이(초) · 가장 클 때 배율
        private const float Period = 1.1f;
        private const float SecondBeat = 0.25f;
        private const float BeatLength = 0.18f;
        private const float PeakScale = 1.2f;

        public Color Rest;
        public Color Beat;
        private Image _image;

        private void Awake() => _image = GetComponent<Image>();

        private void Update()
        {
            float t = Time.unscaledTime % Period;
            float k = Mathf.Max(Envelope(t), Envelope(t - SecondBeat));
            transform.localScale = Vector3.one * Mathf.Lerp(1f, PeakScale, k);
            _image.color = Color.Lerp(Rest, Beat, k);
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
            if (_image != null)
                _image.color = Rest;
        }

        // 박동 하나 — 시작에 1로 솟았다가 BeatLength 동안 0으로
        private static float Envelope(float t) => t < 0f || t > BeatLength ? 0f : 1f - t / BeatLength;
    }
}

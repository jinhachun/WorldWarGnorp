using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 화면 위 가운데 — 전투 시간과 가속(공격력 · 생산 수). 오를 때마다 잠깐 커졌다 돌아온다
    [RequireComponent(typeof(Text))]
    public class RampLabel : MonoBehaviour
    {
        private const float PulseScale = 1.4f;
        private const float PulseDuration = 0.3f;

        private Text _text;
        private float _lastMultiplier = 1f;
        private float _pulse = PulseDuration;

        private void Awake() => _text = GetComponent<Text>();

        private void Update()
        {
            if (!BattleManager.Fighting)
            {
                _text.text = "";
                return;
            }

            float elapsed = BattleManager.Elapsed;
            float multiplier = BattleManager.DamageMultiplier;
            if (multiplier > _lastMultiplier)
                _pulse = 0f;
            _lastMultiplier = multiplier;

            string time = $"{(int)elapsed / 60}:{(int)elapsed % 60:00}";
            string extra = BattleManager.ExtraUnits > 0 ? $"   생산 +{BattleManager.ExtraUnits}기" : "";
            _text.text = multiplier > 1f
                ? $"{time}   공격력 +{Mathf.RoundToInt((multiplier - 1f) * 100f)}%{extra}"
                : time;

            _pulse += Time.deltaTime;
            float t = Mathf.Clamp01(_pulse / PulseDuration);
            transform.localScale = Vector3.one * Mathf.Lerp(PulseScale, 1f, t);
        }
    }
}

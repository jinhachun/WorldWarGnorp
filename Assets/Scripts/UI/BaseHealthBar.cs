using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 본진 체력바(손맛 J1 — Docs/폴리싱_계획.md). 맞으면 채움이 즉시 줄고 깎인 구간은 흰 잔상으로 남았다가 잠깐 뒤 감속하며 따라 준다 ·
    // 그 순간 채움이 흰색으로 번쩍 · 바가 UI 픽셀 칸 단위로 흔들림 · 숫자가 튀며 굴러감. 체력이 낮으면 채움이 붉게 숨쉰다.
    // 회복은 거꾸로 — 초록 잔상이 먼저 차고 채움이 따라 찬다. 본진 체력을 매 프레임 읽어 변화로 판단한다(게임 코드는 이걸 모른다).
    // 실제 시간(Juice.RealDeltaTime)으로 움직인다 — 히트스톱 중에도 바는 반응한다. 세기 값은 BattleConfig 「손맛」
    public class BaseHealthBar : MonoBehaviour
    {
        [SerializeField] private Base _base;
        [Tooltip("채움 층. Image.type = Filled 여야 fillAmount가 먹는다")]
        [SerializeField] private Image _fill;
        [Tooltip("현재 체력 숫자")]
        [SerializeField] private Text _hpText;

        private Image _ghost;
        private RectTransform _rect;
        private Vector2 _home;
        private Color _lordColor;

        private bool _started;
        private float _value;       // 지난 프레임의 실제 체력 몫
        private float _fillShown;
        private float _ghostShown;
        private bool _healing;      // 회복 중이면 채움이 따라가고, 아니면 잔상이 따라간다
        private float _lagFrom;
        private float _lagAge;
        private float _hitAge = float.MaxValue;
        private int _rollFrom, _rollTo, _shownHp = -1;
        private float _rollAge = float.MaxValue;
        private float _clock;       // 실제 시간(Juice.RealDeltaTime을 쌓은 것) — 숨쉬기 주기

        // 채움은 그 진영 영주의 색(사용자 10/13) — 전장의 탑 지붕 · 깃발과 같은 색(RunState.LordColor)
        private void Start()
        {
            _lordColor = RunState.LordColor(_base.Team);
            _fill.color = _lordColor;
            // 잔상 층 = 채움을 복제해 바로 뒤에 둔다(같은 그림 · 같은 Filled 설정 — 새 그림 없음)
            _ghost = Instantiate(_fill, _fill.transform.parent, false);
            _ghost.name = "FillGhost";
            _ghost.transform.SetSiblingIndex(_fill.transform.GetSiblingIndex());
            _rect = (RectTransform)transform;
            _home = _rect.anchoredPosition;
        }

        private void Update()
        {
            BattleConfig config = BattleManager.Config;
            float dt = Juice.RealDeltaTime;
            _clock += dt;
            float hp01 = _base.Hp01;
            if (!_started)
            {
                _started = true;
                _value = _fillShown = _ghostShown = hp01;
            }

            if (hp01 < _value)
            {
                // 맞음 — 채움은 즉시, 잔상은 지금 보이는 자리(이전 값 이상)에서 다시 기다린다
                _ghostShown = _healing ? _fillShown : Mathf.Max(_ghostShown, _value);
                _healing = false;
                _lagFrom = _ghostShown;
                _lagAge = 0f;
                _hitAge = 0f;
                // 소리 자리(J6): 본진 피격
            }
            else if (hp01 > _value)
            {
                // 회복 — 초록 잔상이 즉시 새 값, 채움이 지금 자리에서 기다렸다 따라 찬다
                _healing = true;
                _lagFrom = _fillShown;
                _lagAge = 0f;
            }
            _value = hp01;

            // 따라가는 층 — 기다렸다가 감속하며 실제 값에 닿는다
            _lagAge += dt;
            float lag = Mathf.Lerp(_lagFrom, _value, Juice.OutQuad((_lagAge - config.HpGhostDelay) / config.HpGhostDrop));
            if (_healing)
            {
                _fillShown = lag;
                _ghostShown = _value;
            }
            else
            {
                _fillShown = _value;
                _ghostShown = lag;
            }
            _fill.fillAmount = _fillShown;
            _ghost.fillAmount = _ghostShown;
            _ghost.color = _healing ? (Color)DamageNumbers.Healed : Color.white;

            // 채움 색 — 맞은 순간 흰 번쩍 · 낮은 체력이면 영주 색 ↔ 빨강으로 숨쉼
            _hitAge += dt;
            Color fillColor = _lordColor;
            if (_value > 0f && _value < config.LowHpRatio)
                fillColor = Color.Lerp(_lordColor, DamageNumbers.AllyHurt, 0.5f - 0.5f * Mathf.Cos(_clock * 2f * Mathf.PI / config.LowHpPulseSeconds));
            if (_hitAge < config.BaseHitFlash)
                fillColor = Color.white;
            _fill.color = fillColor;

            // 흔들림 — 칸(3px) 단위로 좌우 · 상하가 엇갈리며 잦아든다
            Vector2 offset = Vector2.zero;
            float shakeT = _hitAge / config.BaseHitShakeSeconds;
            if (shakeT < 1f)
            {
                int amp = Mathf.RoundToInt(config.BaseHitShake * config.JuiceIntensity * (1f - shakeT));
                int step = (int)(_hitAge * 40f);
                offset = new Vector2(step % 2 == 0 ? amp : -amp, (step / 2) % 2 == 0 ? amp : -amp) * UiHitRings.Pixel;
            }
            _rect.anchoredPosition = _home + offset;

            // 숫자 — 새 값까지 굴러가고, 맞으면 튄다(글자 픽셀 단위로 끊는다). 살아 있으면 1 이상으로 보이게 올림. 죽으면 음수 대신 0
            int hp = Mathf.Max(0, Mathf.CeilToInt(_base.Hp));
            if (hp != _rollTo || _shownHp < 0)
            {
                _rollFrom = _shownHp < 0 ? hp : _shownHp;
                _rollTo = hp;
                _rollAge = 0f;
            }
            _rollAge += dt;
            int rolled = _rollAge >= config.HpRollSeconds ? _rollTo : Juice.Roll(_rollFrom, _rollTo, _rollAge / config.HpRollSeconds);
            if (rolled != _shownHp)
            {
                _shownHp = rolled;
                _hpText.text = rolled.ToString();
            }
            float punch = Juice.Punch(_hitAge, config.HpNumberPunch * config.JuiceIntensity, config.HpNumberPunchHold, config.HpNumberPunchSeconds);
            _hpText.rectTransform.localScale = Vector3.one * Juice.SnapScale(punch, _hpText.fontSize);
        }
    }
}

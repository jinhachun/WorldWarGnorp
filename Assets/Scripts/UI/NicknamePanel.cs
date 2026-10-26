using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GnorpWar
{
    // 닉네임 묻기 판 — 게임을 켜고 국가 고르기(UI/NationCard) 직전에 한 번(새 런마다 한 번 · 지난 값을 미리 채움, 사용자 10/23).
    // 값은 RunState.PlayerName에 넣는다 — 내 런 기록이 남의 판에 유령 영주로 나갈 때의 이름. 이 기기에서 다음에 미리 채울 값은 PlayerPrefs.
    // 이 오브젝트 = 판 전체(검은 바탕 · 켜고 끄는 단위)
    public class NicknamePanel : MonoBehaviour
    {
        // 길이 상한 — 남의 판 왼쪽 패널의 영주 이름(도트 24 · 칸 폭 230)에 한글 9자가 216px로 들어간다(10자 = 240이면 넘친다)
        private const int MaxLength = 9;
        private const string PrefsKey = "PlayerName";
        // 움직임(실제 시간 · 스타일 가이드 §5) — 열림: 위에서 16px 떨어지며 3단 · 빈 값 확정: 입력 줄이 좌우로 흔들림 · 확정: 3단으로 사라짐
        private static readonly float[] OpenDrop = { 16f, 8f, 0f };
        private static readonly float[] Shake = { 8f, -8f, 4f, -4f, 0f };
        private const float StepTime = 0.04f;
        private const float ShakeStep = 0.03f;
        private const float FadeTime = 0.12f;

        [Tooltip("가운데 판(흰 막대 테두리) — 열릴 때 떨어지는 단위")]
        [SerializeField] private RectTransform _box;
        [Tooltip("이름 칸 — 빈 값으로 확정하면 흔들리는 단위는 이 칸의 줄(부모)")]
        [SerializeField] private InputField _input;
        [Tooltip("빈 값으로 확정했을 때만 켜지는 안내 글")]
        [SerializeField] private Text _error;
        [SerializeField] private Button _confirm;
        [SerializeField] private CanvasGroup _group;
        // 나타나고 사라지는 것은 가운데 판만 — 검은 바탕(_group)까지 옅어지면 뒤의 지도가 비쳤다가 국가 고르기가 뜬다(사용자 10/27 「어색하다」)
        private CanvasGroup _boxGroup;

        // 이번 런에서 이미 물었다 — 국가 고르기(NationCard.Open)는 런마다 국가를 고르기 전에만 열리고, 런 사이에는 World 씬을 다시 불러 이 값이 비므로 새 런마다 한 번 묻는다(사용자 10/23)
        private bool _asked;

        private System.Action _done;
        private Vector2 _boxHome, _rowHome;
        private RectTransform _row;
        // 열린 뒤 · 흔들기 시작 뒤 · 확정 뒤 흐른 실제 시간(음수 = 안 움직임)
        private float _openClock = -1f;
        private float _shakeClock = -1f;
        private float _exitClock = -1f;
        // 같은 프레임에 Enter가 두 길(입력 칸의 onSubmit · 키보드)로 들어와도 한 번만
        private int _confirmFrame = -1;

        private void Awake()
        {
            _row = (RectTransform)_input.transform.parent;
            _boxHome = _box.anchoredPosition;
            _boxGroup = _box.GetComponent<CanvasGroup>();
            if (_boxGroup == null)
                _boxGroup = _box.gameObject.AddComponent<CanvasGroup>();
            _rowHome = _row.anchoredPosition;
            _input.characterLimit = MaxLength;
            _input.lineType = InputField.LineType.SingleLine;
            _input.onSubmit.AddListener(_ => Confirm());
            _input.onValueChanged.AddListener(_ => _error.gameObject.SetActive(false));
            _confirm.onClick.AddListener(Confirm);
        }

        // 이번 런에서 아직 안 물었으면 판을 열고 true — 확정하면 done을 부른다. 이미 물었으면 false(부르는 쪽이 바로 이어 간다)
        public bool Ask(System.Action done)
        {
            if (_asked)
                return false;
            _done = done;
            gameObject.SetActive(true);
            _input.text = PlayerPrefs.GetString(PrefsKey, "");
            _error.gameObject.SetActive(false);
            _group.alpha = 1f;
            _boxGroup.alpha = 0f;
            _exitClock = _shakeClock = -1f;
            _openClock = 0f;
            _input.ActivateInputField();
            _input.MoveTextEnd(false);
            return true;
        }

        private void Update()
        {
            if (_exitClock >= 0f)
            {
                _exitClock += Time.unscaledDeltaTime;
                float fade = _exitClock / FadeTime;
                _boxGroup.alpha = fade < 1f / 3f ? 2f / 3f : fade < 2f / 3f ? 1f / 3f : 0f;
                if (fade >= 1f)
                    Finish();
                return;
            }
            if (_openClock >= 0f)
            {
                _openClock += Time.unscaledDeltaTime;
                int step = Mathf.Min(Mathf.FloorToInt(_openClock / StepTime), OpenDrop.Length - 1);
                _box.anchoredPosition = _boxHome + new Vector2(0f, OpenDrop[step]);
                _boxGroup.alpha = (step + 1f) / OpenDrop.Length;
                if (step == OpenDrop.Length - 1)
                    _openClock = -1f;
            }
            if (_shakeClock >= 0f)
            {
                _shakeClock += Time.unscaledDeltaTime;
                int step = Mathf.Min(Mathf.FloorToInt(_shakeClock / ShakeStep), Shake.Length - 1);
                _row.anchoredPosition = _rowHome + new Vector2(Shake[step], 0f);
                if (step == Shake.Length - 1)
                    _shakeClock = -1f;
            }
            // 칸 밖을 눌러 칸이 꺼진 뒤의 Enter — 칸이 켜져 있으면 칸의 onSubmit이 받는다
            Keyboard keys = Keyboard.current;
            if (keys != null && !_input.isFocused && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame))
                Confirm();
        }

        private void Confirm()
        {
            if (_exitClock >= 0f || _confirmFrame == Time.frameCount)
                return;
            _confirmFrame = Time.frameCount;
            string name = _input.text.Trim();
            if (name.Length == 0)
            {
                _input.text = "";
                _error.gameObject.SetActive(true);
                _shakeClock = 0f;
                _input.ActivateInputField();
                return;
            }
            RunState.PlayerName = name;
            PlayerPrefs.SetString(PrefsKey, name);
            PlayerPrefs.Save();
            _asked = true;
            _input.DeactivateInputField();
            _exitClock = 0f;
        }

        private void Finish()
        {
            _exitClock = -1f;
            _box.anchoredPosition = _boxHome;
            _row.anchoredPosition = _rowHome;
            gameObject.SetActive(false);
            System.Action done = _done;
            _done = null;
            done?.Invoke();
        }
    }
}

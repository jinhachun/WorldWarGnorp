using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 시작 배너(사용자 10/20) — 판이 열리면 화면 가운데에 이번 맵 이름과 효과 글(MapDefinition.Description — 수치에서 코드가 만든다)을 크게 띄웠다가 사라진다.
    // 숫자는 노랑(BuildingTooltip.ColorNumbers). 도트 폰트라 굵게(키워드 강조)는 쓰지 않는다(CLAUDE.md §5-2 11번). 클릭을 막지 않는다
    [RequireComponent(typeof(CanvasGroup))]
    public class TerrainBanner : MonoBehaviour
    {
        // 실제 시간(초) — 배속과 상관없이 같은 길이. 다 보인 채 · 흐려지는 동안
        private const float HoldSeconds = 1.6f;
        private const float FadeSeconds = 0.4f;

        [SerializeField] private Text _title;
        [SerializeField] private Text _body;

        private CanvasGroup _group;
        private bool _wasFighting;
        // 뜬 뒤 흐른 실제 시간(Juice.RealDeltaTime — 녹화 중에도 녹화 시간대로)
        private float _age = 1e6f;
        private RectTransform _rect;
        private Vector2 _home;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _home = _rect.anchoredPosition;
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void Update()
        {
            bool fighting = BattleManager.Fighting;
            if (fighting && !_wasFighting && RunState.Map != null)
            {
                _title.text = RunState.Map.DisplayName;
                _body.text = BuildingTooltip.ColorNumbers(RunState.Map.Description);
                _age = 0f;
                // 소리 자리(J6): 팝업 열림(지형 배너)
            }
            _wasFighting = fighting;
            _age += Mathf.Min(Juice.RealDeltaTime, 1f / 30f);
            float t = _age;
            // 나타남 — 팝업 열림 문법(UI/PopupMotion): 위에서 3계단 내려앉으며 3계단으로 짙어진다
            _rect.anchoredPosition = _home + new Vector2(0f, PopupMotion.OpenOffset(t));
            _group.alpha = t < HoldSeconds ? PopupMotion.OpenAlpha(t) : 1f - Mathf.Clamp01((t - HoldSeconds) / FadeSeconds);
        }
    }
}

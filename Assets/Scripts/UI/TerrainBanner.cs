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
        private float _shownAt = float.NegativeInfinity;

        private void Awake()
        {
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
                _shownAt = Time.unscaledTime;
            }
            _wasFighting = fighting;
            float t = Time.unscaledTime - _shownAt;
            _group.alpha = t < HoldSeconds ? 1f : 1f - Mathf.Clamp01((t - HoldSeconds) / FadeSeconds);
        }
    }
}

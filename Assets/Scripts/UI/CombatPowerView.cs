using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 체력 바 아래 — 진영별 유닛 수 + 초당 생산 수, 가운데 줄다리기 막대(유닛 수 비율, 아군 몫 = 내 영주 색 채움 · 나머지 = 상대 영주 색).
    // 전투 중에만 보인다
    public class CombatPowerView : MonoBehaviour
    {
        private const float Follow = 6f;   // 막대가 실제 비율을 따라가는 빠르기 (읽기용 — 0.5초쯤이면 다 따라간다)
        private const float IconGap = 6f;  // 유닛 얼굴 아이콘과 숫자 사이
        private const string RateColor = "#9A9AA8";
        private const int RateSize = 24;   // 도트 폰트(12px)의 정수배

        [Tooltip("켜고 끄는 묶음 (막대·숫자 전부)")]
        [SerializeField] private GameObject _content;
        [Tooltip("아군 몫 채움. Image.type = Filled 여야 fillAmount가 먹는다")]
        [SerializeField] private Image _allyFill;
        [Tooltip("적 몫 채움(막대 바탕) — 비우면 색을 안 바꾼다")]
        [SerializeField] private Image _enemyFill;
        [SerializeField] private Text _allyText;
        [SerializeField] private RectTransform _allyIcon;
        [SerializeField] private Text _enemyText;

        private float _shownShare = 0.5f;
        private bool _colored;

        private void Update()
        {
            bool fighting = BattleManager.Fighting;
            if (_content.activeSelf != fighting)
                _content.SetActive(fighting);
            if (!fighting)
            {
                _colored = false;
                _shownShare = 0.5f; // 다음 전투는 가운데에서 시작
                return;
            }

            // 진영 색 하나로(사용자 10/20 U10) — 체력 바와 같은 영주 색(RunState.LordColor). 상대는 전투가 시작돼야 정해진다
            if (!_colored)
            {
                _colored = true;
                _allyFill.color = RunState.LordColor(Team.Ally);
                if (_enemyFill != null)
                    _enemyFill.color = RunState.LordColor(Team.Enemy);
            }
            int ally = CombatPower.Count(Team.Ally);
            int enemy = CombatPower.Count(Team.Enemy);
            float share = ally + enemy > 0 ? (float)ally / (ally + enemy) : 0.5f;
            _shownShare = Mathf.Lerp(_shownShare, share, 1f - Mathf.Exp(-Follow * Time.unscaledDeltaTime));
            _allyFill.fillAmount = _shownShare;

            // 아군 줄은 오른쪽 끝이 막대에 붙어 글 폭이 바뀌면 얼굴을 옮긴다 · 적 줄은 얼굴이 막대 옆에 고정, 글이 그 오른쪽
            _allyText.text = Line(ally, CombatPower.SpawnPerSecond(Team.Ally));
            _allyIcon.anchoredPosition = new Vector2(-_allyText.preferredWidth - IconGap - _allyIcon.sizeDelta.x / 2f, 0f);
            _enemyText.text = Line(enemy, CombatPower.SpawnPerSecond(Team.Enemy));
        }

        // 「23기」 크게 + 작은 회색 「초당 0.6기」 — 생산 수는 소수 한 자리, 10 이상이면 정수
        private static string Line(int count, float perSecond) =>
            $"{count}기<size={RateSize}><color={RateColor}> 초당 "
            + perSecond.ToString(perSecond >= 10f ? "0" : "0.0", CultureInfo.InvariantCulture) + "기</color></size>";
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 목숨 하트 한 줄 — 시작 목숨만큼의 하트(원본 크기)를 parent의 왼쪽 가운데에서 오른쪽으로 놓고, 남은 만큼은 하트 색 · 나머지는 같은 그림을 어둡게.
    // 국가 위기(목숨 0)면 빈 하트 하나가 빨갛게 두근거린다(UI/HeartPulse). 위 줄(UI/WorldSidePanel.ShowStatus)과 전투 결과 창(UI/ResultStats)이 같이 쓴다
    public class HeartRow
    {
        public static readonly Color HeartColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.25f);
        // 하트 사이(px)
        private const float Gap = 4f;

        private readonly Image[] _hearts;
        private readonly HeartPulse _crisis;
        // 지난번에 보인 목숨 — 줄면 잃은 하트가 깨진다(UI/HeartBreak · 손맛 J3)
        private int _shownLives;

        // heart = 하트 한 칸의 프리팹(Parts/UI_Heart — 원본 크기, 왼쪽 가운데 기준 · 두근거릴 때 제자리에서 커지게 피벗은 가운데).
        // livesBefore = 처음 그릴 때 이미 잃은 것으로 보고 깨뜨릴 목숨의 기준(결과 창 — 전투 전 목숨). 음수면 지금 목숨(깨짐 없음)
        public HeartRow(RectTransform parent, Image heart, int livesBefore = -1)
        {
            _shownLives = livesBefore >= 0 ? livesBefore : RunState.Lives;
            _hearts = new Image[RunState.LivesMax];
            for (int i = 0; i < _hearts.Length; i++)
            {
                _hearts[i] = Object.Instantiate(heart, parent);
                _hearts[i].name = "Heart";
            }
            _crisis = _hearts[0].gameObject.AddComponent<HeartPulse>();
            _crisis.Rest = EmptyColor;
            _crisis.Beat = ColorUtility.TryParseHtmlString(BuildingTooltip.EnemyColor, out Color red) ? red : Color.red;
            _crisis.enabled = false;
        }

        // x부터 놓고 지금 목숨만큼 채운다(국가 위기면 두근거리는 빈 하트 하나) — 마지막 하트 오른쪽 끝의 x
        public float Show(float x)
        {
            bool crisis = RunState.InCrisis;
            for (int i = 0; i < _hearts.Length; i++)
            {
                Image heart = _hearts[i];
                bool shown = !crisis || i == 0;
                heart.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                float width = heart.rectTransform.sizeDelta.x;
                heart.rectTransform.anchoredPosition = new Vector2(x + width / 2f, 0f);
                x += width + Gap;
                // 위기 하트의 색은 HeartPulse가 칠한다(위 줄은 매 프레임 Show를 부른다)
                if (!crisis)
                    heart.color = i < RunState.Lives ? HeartColor : EmptyColor;
            }
            // 색을 칠한 뒤에 켜고 끈다 — 끌 때 크기 · 색을 되돌린다
            _crisis.enabled = crisis;
            // 잃은 하트가 그 자리에서 깨져 떨어진다(자리를 놓은 뒤라 지금 자리에서) — 칸은 이미 빈 하트 색
            for (int i = RunState.Lives; i < _shownLives && i < _hearts.Length; i++)
                if (_hearts[i].gameObject.activeInHierarchy)
                    HeartBreak.Play(_hearts[i], HeartColor);
            _shownLives = RunState.Lives;
            return x - Gap;
        }
    }
}

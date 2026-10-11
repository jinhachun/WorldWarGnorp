using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 행복도 하트 한 줄 — 최대 행복도만큼의 하트(원본 크기)를 parent의 왼쪽 가운데에서 오른쪽으로 놓고, 찬 만큼은 하트 색 · 나머지는 같은 그림을 어둡게.
    // 위 줄(UI/WorldSidePanel.ShowStatus)과 전투 결과 창(UI/ResultStats)이 같이 쓴다
    public class HeartRow
    {
        public static readonly Color HeartColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.25f);
        // 하트 사이(px)
        private const float Gap = 4f;

        private readonly Image[] _hearts;

        public HeartRow(RectTransform parent, Sprite heart)
        {
            _hearts = new Image[RunState.HappinessMax];
            for (int i = 0; i < _hearts.Length; i++)
            {
                _hearts[i] = new GameObject("Heart", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _hearts[i].transform.SetParent(parent, false);
                _hearts[i].raycastTarget = false;
                _hearts[i].preserveAspect = true;
                _hearts[i].sprite = heart;
                _hearts[i].SetNativeSize();
                RectTransform rect = _hearts[i].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            }
        }

        // x부터 놓고 지금 행복도만큼 채운다 — 마지막 하트 오른쪽 끝의 x
        public float Show(float x)
        {
            foreach (Image heart in _hearts)
            {
                heart.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += heart.rectTransform.sizeDelta.x + Gap;
            }
            for (int i = 0; i < _hearts.Length; i++)
                _hearts[i].color = i < RunState.Happiness ? HeartColor : EmptyColor;
            return x - Gap;
        }
    }
}

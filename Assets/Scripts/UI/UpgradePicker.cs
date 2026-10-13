using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 합치기 보상 업그레이드 고르기 창(아웃게임기획 §4, 사용자 10/13) — 합치기 연출이 끝난 직후 상점 위에 카드 셋이 뜨고, 하나를 누르면 닫힌다.
    // 연쇄로 합쳐져 대기가 여럿이면 하나 고를 때마다 다음 셋으로 바뀐다. 떠 있는 동안에는 화면 전체를 덮는 판이 상점 조작을 막는다.
    // 카드 = 등급 색 외곽선(기물 등급 테두리와 같은 색 · 같은 강조 — BuildingRarity.FrameColor, UI/ButtonHover) · 아이콘(원본 크기) · 이름 · 지금 등급의 효과 한 줄.
    // 언제 뜨는지는 UI/ShopPanel이 정한다(Show)
    public class UpgradePicker : MonoBehaviour
    {
        [System.Serializable]
        private class Card
        {
            [Tooltip("카드의 판 — 그림 없는 단색 판이고, 이 색이 외곽선(등급 색)이다. 안쪽의 검은 판이 그 위를 덮는다")]
            public Button Button;
            public ButtonHover Hover;
            public Image Icon;
            [Tooltip("이름 — 도트 폰트")]
            public Text Name;
            [Tooltip("효과 한 줄 — 깔끔 폰트")]
            public Text Body;
        }

        [Tooltip("화면 전체를 덮는 어두운 판 — 뒤의 상점을 못 누르게 막는다")]
        [SerializeField] private Image _dim;
        [SerializeField] private RectTransform _cardRow;
        [SerializeField] private Card[] _cards;

        // 카드 셋이 바뀐 직후 이만큼(초)은 누르기를 받지 않는다 — 연쇄 합치기에서 더블클릭이 다음 셋까지 고르지 않게
        private const float ClickGuard = 0.2f;

        private float _dimAlpha;
        private RunState.UpgradeOffer _shown;
        private float _shownAt;

        private void Awake()
        {
            _dimAlpha = _dim.color.a;
            for (int i = 0; i < _cards.Length; i++)
            {
                int choice = i;
                _cards[i].Button.onClick.AddListener(() =>
                {
                    if (Time.unscaledTime - _shownAt < ClickGuard)
                        return;
                    _shownAt = Time.unscaledTime;
                    RunState.ChooseUpgrade(choice);
                });
            }
        }

        // block = 고를 것이 남았다(상점 조작을 막는다) · cards = 카드를 보인다(합치기 연출이 끝났다). 막기만 하는 동안에는 판이 투명하다
        public void Show(bool block, bool cards)
        {
            if (gameObject.activeSelf != block)
                gameObject.SetActive(block);
            if (!block)
            {
                _shown = null;
                return;
            }
            _dim.color = new Color(_dim.color.r, _dim.color.g, _dim.color.b, cards ? _dimAlpha : 0f);
            if (_cardRow.gameObject.activeSelf != cards)
                _cardRow.gameObject.SetActive(cards);
            RunState.UpgradeOffer offer = RunState.PendingUpgrades[0];
            if (!cards || offer == _shown)
                return;
            _shown = offer;
            _shownAt = Time.unscaledTime;
            for (int i = 0; i < _cards.Length; i++)
            {
                Card card = _cards[i];
                bool used = i < offer.Choices.Length;
                card.Button.gameObject.SetActive(used);
                if (!used)
                    continue;
                UpgradeDefinition choice = offer.Choices[i];
                card.Hover.Base = offer.Rarity.FrameColor();
                card.Icon.sprite = choice.Icon;
                card.Icon.SetNativeSize();
                card.Name.text = choice.DisplayName;
                card.Body.text = BuildingTooltip.Highlight(choice.Describe(choice.ValueAt(offer.Rarity)));
            }
        }
    }
}

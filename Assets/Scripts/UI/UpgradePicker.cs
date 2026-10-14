using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 업그레이드 고르기 창(아웃게임기획 §4, 사용자 10/13) — 고를 것(RunState.PendingUpgrades)이 있으면 지도 씬 어디서든 카드가 뜨고, 하나를 누르면 닫힌다(대기가 남았으면 다음 것).
    //   합치기 보상: 상점에서 합치기 연출이 끝난 직후, 카드 셋.
    //   지역 특수 업그레이드(신화): 국가를 고른 직후(시작) · 영주를 멸망시키고 지도로 돌아와 AI 턴 재생이 끝난 뒤, 카드 1~3장 + 머리말.
    //   공화정(이미 셋을 모두 얻었다 — UpgradeOffer.Granted, 사용자 10/14): 합치기 보상처럼 카드 셋 + 공화정 머리말. 고르는 게 아니라 카드 · 판 어디를 눌러도 닫히고, 카드 강조가 없다.
    // 떠 있는 동안에는 화면 전체를 덮는 판이 지도 · 상점 조작을 막는다.
    // 카드 = 등급 색 외곽선(기물 등급 테두리와 같은 색 · 같은 강조 — BuildingRarity.FrameColor, UI/ButtonHover) · 아이콘(원본 크기) · 이름 · 그 등급의 효과 한 줄.
    // 카드들은 외곽선 두께만큼 겹쳐 붙어 있다(틈으로 뒤가 비치지 않게 · 붙은 자리의 선은 한 줄)
    public class UpgradePicker : MonoBehaviour, IPointerClickHandler
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

        [Tooltip("화면 전체를 덮는 어두운 판 — 뒤의 지도 · 상점을 못 누르게 막는다. 카드와 머리말은 이 판의 자식이다")]
        [SerializeField] private Image _dim;
        [SerializeField] private RectTransform _cardRow;
        [SerializeField] private Card[] _cards;
        [Tooltip("머리말 — 지역 특수 업그레이드를 고를 때만 보인다(도트 폰트)")]
        [SerializeField] private Text _header;
        [Tooltip("상점 — 합치기 연출이 도는 동안에는 막기만 하고 카드는 연출이 끝난 뒤에 보인다")]
        [SerializeField] private ShopPanel _shop;
        [Tooltip("AI 턴 재생 동안 켜져 있는 판(SkipReplay) — 그동안에는 뜨지 않는다(재생이 끝난 뒤에 고른다)")]
        [SerializeField] private GameObject _replay;

        // 카드 셋이 바뀐 직후 이만큼(초)은 누르기를 받지 않는다 — 더블클릭이 다음 것까지 고르지 않게
        private const float ClickGuard = 0.2f;
        // 카드 외곽선의 두께(px) — 이만큼 겹쳐 놓는다
        private const float Outline = 4f;
        private const string RepublicHeader = "공화정 — 모두 얻었습니다";

        private float _dimAlpha;
        private string _specialHeader;
        private RunState.UpgradeOffer _shown;
        private float _shownAt;

        private void Awake()
        {
            _dimAlpha = _dim.color.a;
            _specialHeader = _header.text;
            for (int i = 0; i < _cards.Length; i++)
            {
                int choice = i;
                _cards[i].Button.onClick.AddListener(() => Choose(choice));
            }
        }

        // 판(어두운 판 — 이 오브젝트의 자식)을 누름 — 공화정 창이면 닫는다. 카드 누르기는 버튼이 먼저 받는다
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_shown != null && _shown.Granted && _cardRow.gameObject.activeSelf)
                Choose(0);
        }

        // 공화정 창이면 choice와 상관없이 닫기만 한다(RunState.ChooseUpgrade가 다시 얻지 않는다)
        private void Choose(int choice)
        {
            if (Time.unscaledTime - _shownAt < ClickGuard)
                return;
            _shownAt = Time.unscaledTime;
            RunState.ChooseUpgrade(choice);
        }

        private void Update()
        {
            bool block = RunState.PendingUpgrades.Count > 0 && !_replay.activeInHierarchy;
            if (_dim.gameObject.activeSelf != block)
                _dim.gameObject.SetActive(block);
            if (!block)
            {
                _shown = null;
                return;
            }
            // 막기만 하는 동안(합치기 연출)에는 판이 투명하다
            bool cards = !_shop.MergePlaying;
            _dim.color = new Color(_dim.color.r, _dim.color.g, _dim.color.b, cards ? _dimAlpha : 0f);
            if (_cardRow.gameObject.activeSelf != cards)
                _cardRow.gameObject.SetActive(cards);
            if (!cards)
            {
                // 머리말은 카드와 함께 뜬다(공화정 항목은 합치기 연출 동안 이미 대기열에 있다)
                _header.enabled = false;
                _shown = null;
                return;
            }
            // 올린 카드를 맨 위에 그린다 — 겹친 변까지 그 카드의 외곽선이 네 변 다 제 색으로 보이게
            foreach (Card card in _cards)
                if (card.Hover.Hovered)
                    card.Button.transform.SetAsLastSibling();
            RunState.UpgradeOffer offer = RunState.PendingUpgrades[0];
            if (offer == _shown)
                return;
            _shown = offer;
            _shownAt = Time.unscaledTime;
            _header.enabled = offer.Rarity == BuildingRarity.Mythic || offer.Granted;
            _header.text = offer.Granted ? RepublicHeader : _specialHeader;
            int count = offer.Choices.Length;
            for (int i = 0; i < _cards.Length; i++)
            {
                Card card = _cards[i];
                bool used = i < count;
                card.Button.gameObject.SetActive(used);
                if (!used)
                    continue;
                // 후보 수만큼만, 묶음째 가운데로
                var rect = (RectTransform)card.Button.transform;
                rect.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * (rect.sizeDelta.x - Outline), 0f);
                UpgradeDefinition choice = offer.Choices[i];
                card.Hover.Base = offer.Rarity.FrameColor();
                // 공화정 창은 고르는 게 아니라 강조를 끄고 셋 다 제 밝기로
                card.Hover.enabled = !offer.Granted;
                if (offer.Granted)
                    card.Button.image.color = card.Hover.Base;
                card.Icon.sprite = choice.Icon;
                card.Icon.SetNativeSize();
                card.Name.text = choice.DisplayName;
                card.Body.text = BuildingTooltip.Highlight(choice.Describe(choice.ValueAt(offer.Rarity)));
            }
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 업그레이드 고르기 창(아웃게임기획 §4, 사용자 10/13) — 고를 것(RunState.PendingUpgrades)이 있으면 지도 씬 어디서든 카드가 뜨고, 하나를 누르면 닫힌다(대기가 남았으면 다음 것).
    //   합치기 보상: 상점에서 합치기 연출이 끝난 직후, 카드 셋.
    //   지역 특수 업그레이드(신화): 국가를 고른 직후(시작) · 영주를 멸망시키고 지도로 돌아와 AI 턴 재생이 끝난 뒤, 카드 1~3장 + 머리말 + 카드 아래 「건너뛰기 +N골드」 버튼.
    //   국가 위기(신화 — 목숨이 0이 된 전투 뒤 지도로 돌아와 AI 턴 재생이 끝난 뒤, 사용자 10/23): 카드 1~3장 + 「국가 위기」 머리말 + 그 아래 읽는 글 한 줄. 건너뛰기 없음.
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
            // 국가 전용 업그레이드의 국가 기호(카드 오른쪽 위 구석 — 총괄 10/21). 카드마다 실행 중 하나 만든다(Awake)
            [System.NonSerialized] public Image Nation;
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
        [Tooltip("지역 특수(신화) 고르기에만 보이는 「건너뛰기 +N골드」 버튼 — 고르지 않고 골드를 받는다(RunState.SkipUpgrade, 사용자 10/15)")]
        [SerializeField] private Button _skip;
        [SerializeField] private Text _skipLabel;

        // 카드 셋이 바뀐 직후 이만큼(초)은 누르기를 받지 않는다 — 더블클릭이 다음 것까지 고르지 않게
        private const float ClickGuard = 0.2f;
        // 카드 외곽선의 두께(px) — 이만큼 겹쳐 놓는다
        private const float Outline = 4f;
        private const string RepublicHeader = "공화정 — 모두 얻었습니다";
        // 국가 위기(RunState.UpgradeOffer.Crisis, 사용자 10/23) — 머리말(도트) 아래 읽는 글 한 줄(깔끔 — 카드 효과 글의 복제). 머리말은 그 한 줄만큼 위로 올린다
        private const string CrisisHeader = "국가 위기";
        private const string CrisisBody = "백성들이 새로운 국가 구조를 요구합니다. 하나를 고르십시오.";

        private float _dimAlpha;
        private string _specialHeader;
        private Vector2 _headerHome;
        private Text _crisisBody;
        private RunState.UpgradeOffer _shown;
        private float _shownAt;

        private void Awake()
        {
            _dimAlpha = _dim.color.a;
            _specialHeader = _header.text;
            _headerHome = _header.rectTransform.anchoredPosition;
            _crisisBody = CrisisLine();
            for (int i = 0; i < _cards.Length; i++)
            {
                int choice = i;
                _cards[i].Button.onClick.AddListener(() => Choose(choice));
                _cards[i].Nation = NationMark((RectTransform)_cards[i].Button.transform);
            }
            _skip.onClick.AddListener(Skip);
        }

        // 카드 오른쪽 위 구석, 종족 테두리(NationFrame)의 모서리 「+」 장식 안쪽 — 원본 크기(Simple + Set Native Size, CLAUDE.md §5-1). 누르기를 가리지 않는다
        private const float NationInset = 40f;

        private static Image NationMark(RectTransform card)
        {
            var mark = new GameObject("NationMark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = mark.rectTransform;
            rect.SetParent(card, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-NationInset, -NationInset);
            mark.type = Image.Type.Simple;
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            mark.enabled = false;
            return mark;
        }

        // 위기 글 한 줄 — 머리말의 원래 자리(같은 틀 · 가운데 정렬)에 놓는다. 글꼴 · 크기 · 색은 카드 효과 글 그대로
        private Text CrisisLine()
        {
            Text line = Instantiate(_cards[0].Body, _header.transform.parent);
            line.name = "CrisisBody";
            RectTransform rect = line.rectTransform, header = _header.rectTransform;
            rect.anchorMin = header.anchorMin;
            rect.anchorMax = header.anchorMax;
            rect.pivot = header.pivot;
            rect.sizeDelta = header.sizeDelta;
            rect.anchoredPosition = _headerHome;
            line.alignment = TextAnchor.MiddleCenter;
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.text = CrisisBody;
            line.enabled = false;
            return line;
        }

        private void Skip()
        {
            if (Time.unscaledTime - _shownAt < ClickGuard)
                return;
            _shownAt = Time.unscaledTime;
            RunState.SkipUpgrade();
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
                _crisisBody.enabled = false;
                // 건너뛰기도 카드와 함께 — 안 끄면 지난 지역 특수 고르기의 버튼이 합치기 연출 동안 남아 보인다
                _skip.gameObject.SetActive(false);
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
            _header.text = offer.Granted ? RepublicHeader : offer.Crisis ? CrisisHeader : _specialHeader;
            _crisisBody.enabled = offer.Crisis;
            _header.rectTransform.anchoredPosition = _headerHome + (offer.Crisis ? new Vector2(0f, _header.rectTransform.sizeDelta.y) : Vector2.zero);
            _skip.gameObject.SetActive(RunState.CanSkipUpgrade);
            _skipLabel.text = $"건너뛰기 <color={BuildingTooltip.NumberColor}>+{RunState.UpgradeSkipGold}</color>골드";
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
                Sprite nation = choice.Nation != BuildingNation.Common && RunState.Config != null && RunState.Config.World != null
                    ? RunState.Config.World.NationIcon(choice.Nation) : null;
                card.Nation.enabled = nation != null;
                if (nation != null)
                {
                    card.Nation.sprite = nation;
                    card.Nation.SetNativeSize();
                    card.Nation.transform.SetAsLastSibling();   // 카드 안쪽 판 위에
                }
                card.Name.text = choice.DisplayName;
                card.Body.text = BuildingTooltip.Highlight(choice.Describe(choice.ValueAt(offer.Rarity)));
            }
        }
    }
}

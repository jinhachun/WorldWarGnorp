using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 상점 진열 한 칸 — 탑에 끼울 층 조각(벽·창·아이콘) · 이름 · 가격. 마우스를 올리면 설명 창(BuildingTooltip).
    // 누르면 빈 칸에 사고, 끌어서 원하는 칸에 놓아 살 수도 있다(ShopPanel)
    public class OfferCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("탑에 끼울 층 조각 — 전장 탑과 같은 층 그림(사용자 10/9). 빈 칸이면 통째로 숨김")]
        [SerializeField] private FloorLook _floor;
        [SerializeField] private Image _icon;
        [Tooltip("등급 색 창틀(시작 등급) — 층 조각의 창틀")]
        [SerializeField] private Image _rarityFrame;
        [Tooltip("가격 숫자 — 앞에 동전 아이콘(_coin)")]
        [SerializeField] private Text _price;
        [SerializeField] private Image _coin;
        [SerializeField] private Text _name;
        [Tooltip("진열이 잠겼을 때 카드 위에 뜨는 글")]
        [SerializeField] private Text _locked;
        [SerializeField] private Button _button;
        [Tooltip("층 조각 뒤 빛(사용자 10/9) — 이미 가진 기물이면 은은한 흰 숨쉬기(RANK UP은 글자만 — 10/20)")]
        [SerializeField] private Image _glow;
        [Tooltip("사면 바로 등급이 오르는 카드 위에서 통통 튀는 「RANK UP!」")]
        [SerializeField] private Text _rankUp;
        [Tooltip("살 수 없는 카드(RunState.CanBuy가 거짓 — 골드 부족 등)는 카드 전체를 흐리게(사용자 10/10)")]
        [SerializeField] private CanvasGroup _group;
        private const float UnbuyableAlpha = 0.4f;
        [Tooltip("팔린(빈) 진열 칸의 자리표시 — 회색 점선 칸(사용자 10/20 U9). 층 조각 자리에 원본 크기로")]
        [SerializeField] private Image _sold;
        private const string SoldMark = "—";
        private static readonly Color SoldColor = new Color(0.43f, 0.43f, 0.46f);
        private Color? _nameColor;

        // 빛 숨쉬기 · 글자 튐 — 작게·짧게(연출 과투자 금지)
        private static readonly Color OwnedGlow = Color.white;
        private const float OwnedAlphaMin = 0.2f, OwnedAlphaMax = 0.55f, OwnedPeriod = 2.4f;
        private const float RankUpHop = 8f, RankUpHopPeriod = 0.6f;

        private ShopPanel _shop;
        private float _rankUpY = float.NaN;
        private int _index;
        private bool _hovered;

        public Button Button => _button;

        // 이 칸의 기물이 시작 등급에서 오른 횟수(중립외교 — 보통 0). 창틀 색 · 설명의 수치 · RANK UP 판정이 오른 등급을 따른다
        private int Upgrades => RunState.OfferUpgrades[_index];

        private void OnDisable()
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
        }

        public void Bind(ShopPanel shop, int index)
        {
            _shop = shop;
            _index = index;
        }

        public void Show(BuildingDefinition building, bool canBuy, bool locked)
        {
            bool empty = building == null;
            _locked.enabled = locked && !empty;
            if (_floor.gameObject.activeSelf == empty)
                _floor.gameObject.SetActive(!empty);
            if (!empty)
            {
                _floor.Show(building);
                _icon.sprite = building.Icon;
                _rarityFrame.color = SlotView.Highlight((building.Rarity + Upgrades).FrameColor(), _hovered);
            }
            _price.text = empty ? "" : $"{RunState.PriceOf(building)}";
            _coin.enabled = !empty;
            // 팔린 칸은 자리가 남는다 — 점선 칸 + 이름 자리에 「—」(이번 진열에서 몇 개 샀는지 보이게)
            if (_sold != null)
                _sold.enabled = empty;
            _name.text = empty ? (_sold != null ? SoldMark : "") : building.DisplayName;
            _nameColor ??= _name.color;
            _name.color = empty ? SoldColor : _nameColor.Value;
            _button.interactable = canBuy;
            _group.alpha = empty || canBuy ? 1f : UnbuyableAlpha;   // 빛·「RANK UP!」도 같이 약해진다
            ShowOwnership(building);

            if (!_hovered)
                return;
            if (empty || _shop.Dragging)
                BuildingTooltip.Hide(this);
            else
            {
                OwnedBuilding pair = RunState.FindMergeTarget(building, Upgrades);
                // 사서 놓으면 걸릴 내 업그레이드 — 징병제가 있으면 소환 수에도 더해 보인다(아래의 업그레이드 줄과 숫자가 같은 말을 하게)
                System.Collections.Generic.List<UpgradeHolding> upgrades = RunState.UpgradesOn(building);
                int extra = 0;
                foreach (UpgradeHolding upgrade in upgrades)
                    if (upgrade.Definition.Special == SpecialKind.Conscription)
                        extra += Mathf.RoundToInt(upgrade.Value);
                string body = BuildingTooltip.Describe(building, OwnedBuilding.LevelOf(building, Upgrades), building.CooldownAt(OwnedBuilding.CooldownLevelOf(building, Upgrades)), false, extra);
                if (pair != null)
                    body += BuildingTooltip.RankUpPreview(building, pair.Level, pair.Rarity + 1, extra);
                BuildingTooltip.Show(this, BuildingTooltip.Title(building, false, building.Rarity + Upgrades), body, building.MentionedUnits, RunState.Field, false, upgrades,
                    (building.Rarity + Upgrades, building.Nation));
            }
        }

        // RANK UP = 사면 바로 합쳐져 등급이 오른다(구매와 같은 판정 RunState.FindMergeTarget). 보유 = 탑·보관함에 같은 기물이 있지만 바로 오르지는 않는다
        private void ShowOwnership(BuildingDefinition building)
        {
            bool rankUp = building != null && RunState.FindMergeTarget(building, Upgrades) != null;
            bool owned = !rankUp && building != null && Owns(building);
            _glow.enabled = owned;   // RANK UP은 글자 하나로만 알린다 — 금색 빛은 같은 말이라 뺐다(사용자 10/20 "불필요한 정보 줄이라")
            _rankUp.enabled = rankUp;
            if (float.IsNaN(_rankUpY))
                _rankUpY = _rankUp.rectTransform.anchoredPosition.y;
            float t = Time.unscaledTime;
            if (rankUp)
            {
                Vector2 p = _rankUp.rectTransform.anchoredPosition;
                _rankUp.rectTransform.anchoredPosition = new Vector2(p.x, _rankUpY + Mathf.Round(RankUpHop * Mathf.Abs(Mathf.Sin(t * Mathf.PI / RankUpHopPeriod))));
            }
            else if (owned)
                _glow.color = Pulse(OwnedGlow, OwnedAlphaMin, OwnedAlphaMax, OwnedPeriod, t);
        }

        private static Color Pulse(Color color, float min, float max, float period, float t)
            => new Color(color.r, color.g, color.b, Mathf.Lerp(min, max, 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / period)));

        private static bool Owns(BuildingDefinition building)
        {
            foreach (OwnedBuilding[] row in new[] { RunState.Field, RunState.Storage })
                foreach (OwnedBuilding b in row)
                    if (!OwnedBuilding.IsEmpty(b) && b.Definition == building)
                        return true;
            return false;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
        }

        public void OnBeginDrag(PointerEventData eventData) => _shop.BeginDragOffer(_index, eventData);
        public void OnDrag(PointerEventData eventData) => _shop.Drag(eventData);
        public void OnEndDrag(PointerEventData eventData) => _shop.EndDrag();
    }
}

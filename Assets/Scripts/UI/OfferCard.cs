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

        // ── 짐꾼(사용자 10/23 「짐꾼 행렬」) — 그 기물 국가의 유닛 하나가 층 조각을 머리에 이고 땅 선 위에 선다. 잠기면 짐을 땅에 내려놓고 옆에 선다.
        // 걷기는 짐꾼 · 층 조각 · 빛 · RANK UP을 묶은 판(_carry)만 옮긴다 — 누를 자리(카드 판)는 제자리라 걷는 중에도 사고 · 끌고 · 툴팁이 된다.
        // 나가는 짐꾼(리롤 · 구매)은 그 판의 복제 껍데기가 걷다 사라진다
        // 카드 가운데 기준(px) — 땅 선의 위끝(= 짐꾼 발) · 짐꾼 배율(그림 1px = 화면 6px) · 층 조각 반 크기 · RANK UP은 층 조각 가운데 위로
        public const float GroundY = -28f;
        private const int PorterScale = 6;
        private const float FloorHalf = 60f;
        private const float RankUpAbove = 78f;
        // 잠김 — 층 조각을 왼쪽으로 내려놓고 짐꾼이 그 오른쪽에 선다(사이 8px)
        private const float DropShift = -24f;
        private const float DropGap = 8f;
        // 걷기(실제 시간 · 국가 선택과 같은 걸음) — 들어오고 나가는 거리 · 시간 · 칸마다 늦는 시간
        private const float WalkDistance = 130f;
        public const float WalkTime = 0.3f;
        private const float WalkStagger = 0.04f;
        // 눌러서 산 짐꾼(ShopPanel이 Jump로 알린다)은 층 조각을 던지며 뛰었다(그림 2 · 4 · 5 · 6 · 6 · 5 · 4 · 2px — 사용자 10/23 더 높게) 내려온 뒤 걸어 나간다
        private const float JumpTime = 0.28f;
        private static readonly float[] JumpHops = { PorterScale * 2, PorterScale * 4, PorterScale * 5, PorterScale * 6, PorterScale * 6, PorterScale * 5, PorterScale * 4, PorterScale * 2 };
        private bool _jumpNext;

        // 다음 구매가 던지기다(진열 클릭 — ShopPanel.BuyThrown이 RunState.Buy 직전에 부른다)
        public void Jump() => _jumpNext = true;

        private class Ghost
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 From;
            public float Direction, Clock;
        }

        private RectTransform _carry, _porter;
        private UnitDefinition _porterUnit;
        private Vector2 _porterSize;
        private BuildingDefinition _last;
        private float _enterClock = -1f, _enterDelay;
        // 리롤 뒤 새 짐꾼 — 자기 걸음이 시작될 때까지 안 보인다(나가는 줄이 다 나간 뒤에 들어온다)
        private bool _waitHidden;
        private readonly System.Collections.Generic.List<Ghost> _ghosts = new System.Collections.Generic.List<Ghost>();

        // 층 조각의 화면 자리 — 합치기 연출(UI/MergeFx)이 여기서 출발한다
        public Vector3 LoadPosition => _floor.transform.position;

        private void Awake()
        {
            _carry = (RectTransform)new GameObject("Carry", typeof(RectTransform)).transform;
            _carry.SetParent(transform, false);
            _carry.SetSiblingIndex(0);
            _carry.sizeDelta = Vector2.zero;
            foreach (Transform part in new Transform[] { _glow.transform, _floor.transform, _rankUp.transform })
                part.SetParent(_carry, false);
            _porter = (RectTransform)new GameObject("Porter", typeof(RectTransform)).transform;
            _porter.SetParent(_carry, false);
            _porter.pivot = new Vector2(0.5f, 0f);
            // 팔린 칸의 점선 칸은 땅 위 빈자리에
            if (_sold != null)
                _sold.rectTransform.anchoredPosition = new Vector2(0f, GroundY + FloorHalf);
            // 이름 · 가격 · 「잠김」 — 짐꾼이 제자리에 도착하면 나타난다(Labels)
            _labels = (RectTransform)new GameObject("Labels", typeof(RectTransform)).transform;
            _labels.SetParent(transform, false);
            _labels.sizeDelta = Vector2.zero;
            foreach (Transform part in new Transform[] { _name.transform, _price.transform.parent, _locked.transform })
                part.SetParent(_labels, false);
        }

        // 글 — 짐꾼이 나가는 순간 사라지고, 새 짐꾼이 도착하면 8px 아래에서 2단으로 올라온다(스타일 가이드 §5 · 0.1초)
        private const float LabelRise = 8f;
        private const float LabelRiseTime = 0.1f;
        private RectTransform _labels;
        // 나타난 뒤 흐른 시간(음수 = 제자리)
        private float _labelClock = -1f;

        private void ShowLabels(bool empty)
        {
            // 팔린 칸의 「—」는 바로 · 짐이 있으면 짐꾼이 도착한 뒤에
            bool hidden = !empty && (_enterClock >= 0f || _waitHidden);
            if (_labels.gameObject.activeSelf == hidden)
                _labels.gameObject.SetActive(!hidden);
            if (hidden)
            {
                _labelClock = 0f;
                return;
            }
            float rise = 0f;
            if (_labelClock >= 0f)
            {
                rise = _labelClock < LabelRiseTime * 0.5f ? LabelRise : _labelClock < LabelRiseTime ? LabelRise * 0.5f : 0f;
                _labelClock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                if (rise == 0f)
                    _labelClock = -1f;
            }
            _labels.anchoredPosition = new Vector2(0f, -rise);
        }

        private void OnEnable()
        {
            // 상점을 열 때마다 걸어 들어온다(잠겨 있으면 이미 서 있다)
            _last = null;
        }

        // 이 칸의 기물이 시작 등급에서 오른 횟수(중립외교 — 보통 0). 창틀 색 · 설명의 수치 · RANK UP 판정이 오른 등급을 따른다
        private int Upgrades => RunState.OfferUpgrades[_index];

        private void OnDisable()
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
            foreach (Ghost ghost in _ghosts)
                Destroy(ghost.Rect.gameObject);
            _ghosts.Clear();
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
            if (building != _last)
            {
                if (_last != null && empty)
                    Leave(-1f, false, _jumpNext);   // 팔렸다 — 짐을 건네고(눌러 샀으면 던지고) 빈손으로 탑 쪽(왼쪽)으로 걸어 나간다
                else if (!empty)
                {
                    _enterClock = locked ? -1f : 0f;
                    _enterDelay = _index * WalkStagger;
                }
                _last = building;
                _jumpNext = false;
            }
            if (!empty)
                DrawPorter(_shop.PorterOf(building.Nation));
            if (_porter.gameObject.activeSelf == empty)
                _porter.gameObject.SetActive(!empty);
            Pose(locked);
            Walk();
            ShowLabels(empty);
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

        // 짐꾼 그림 — 국가가 바뀔 때만 다시 그린다. 파란 자리는 흰색(유닛은 흰색 — 영주 색을 입히지 않는다), 말 같은 원래 색은 그대로
        private void DrawPorter(UnitDefinition unit)
        {
            if (unit == _porterUnit)
                return;
            _porterUnit = unit;
            for (int i = _porter.childCount - 1; i >= 0; i--)
                Destroy(_porter.GetChild(i).gameObject);
            _porterSize = unit != null ? UnitFigure.Draw(unit.PrefabFor(Team.Ally), _porter, PorterScale, Color.white) : Vector2.zero;
            _porter.sizeDelta = _porterSize;
        }

        // 이고 섰나 · 내려놓고 섰나(잠김)
        private void Pose(bool locked)
        {
            var floorAt = locked ? new Vector2(DropShift, GroundY + FloorHalf) : new Vector2(0f, GroundY + _porterSize.y + FloorHalf);
            ((RectTransform)_floor.transform).anchoredPosition = floorAt;
            _glow.rectTransform.anchoredPosition = floorAt;
            _rankUpY = floorAt.y + RankUpAbove;
            _rankUp.rectTransform.anchoredPosition = new Vector2(floorAt.x, _rankUpY);
            _porter.anchoredPosition = locked ? new Vector2(DropShift + FloorHalf + DropGap + _porterSize.x * 0.5f, GroundY) : new Vector2(0f, GroundY);
            // 내려놓은 짐 쪽(왼쪽)을 본다
            _porter.localScale = new Vector3(locked ? -1f : 1f, 1f, 1f);
        }

        // 들어오는 걸음(왼쪽에서 제자리로 — 칸마다 조금씩 늦게) · 나가는 껍데기들
        private void Walk()
        {
            // 한 프레임이 길어도(상점을 처음 열 때의 멈칫) 걸음이 건너뛰지 않게 프레임당 진행을 묶는다(UI/WorldLords와 같다)
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            _carry.anchoredPosition = Vector2.zero;
            if (_enterClock >= 0f)
            {
                // 팝업이 내려오는 동안은 출발점(왼쪽)에서 · 리롤 퇴장 중에는 안 보이게 기다린다
                if (_shop.Settled && _shop.ExitLeft <= 0f)
                    _enterClock += dt;
                float p = Mathf.Clamp01((_enterClock - _enterDelay) / WalkTime);
                _carry.anchoredPosition = UnitFigure.Step(Mathf.Lerp(-WalkDistance, 0f, p), PorterScale, p > 0f && p < 1f);
                if (p > 0f)
                    _waitHidden = false;
                if (p >= 1f)
                    _enterClock = -1f;
            }
            else
                _waitHidden = false;
            if (_carry.gameObject.activeSelf == _waitHidden)
                _carry.gameObject.SetActive(!_waitHidden);
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = _ghosts[i];
                ghost.Clock += dt;
                if (ghost.Clock < 0f)
                {
                    // 던지는 점프 — 제자리에서 계단으로 솟았다 내려온다(시계가 음수인 동안)
                    int hop = Mathf.Clamp(Mathf.FloorToInt((ghost.Clock + JumpTime) / JumpTime * JumpHops.Length), 0, JumpHops.Length - 1);
                    ghost.Rect.anchoredPosition = ghost.From + new Vector2(0f, JumpHops[hop]);
                    continue;
                }
                float p = ghost.Clock / WalkTime;
                if (p >= 1f)
                {
                    Destroy(ghost.Rect.gameObject);
                    _ghosts.RemoveAt(i);
                    continue;
                }
                ghost.Rect.anchoredPosition = ghost.From + UnitFigure.Step(ghost.Direction * WalkDistance * p, PorterScale, true);
                // 걸음 뒤 절반에 3단으로 사라진다
                ghost.Group.alpha = p < 0.5f ? 1f : p < 0.75f ? 2f / 3f : 1f / 3f;
            }
        }

        // 지금 짐꾼을 복제 껍데기로 떼어 direction 쪽(1 = 오른쪽 · -1 = 왼쪽)으로 걸어 나가게 한다 — withLoad면 층 조각을 인 채로. 안 보이는 짐꾼이면 내보내지 않고 거짓
        private bool Leave(float direction, bool withLoad, bool jump = false)
        {
            if (!_porter.gameObject.activeSelf || !_carry.gameObject.activeSelf)
                return false;
            RectTransform shell = Instantiate(_carry, transform);
            shell.SetSiblingIndex(_carry.GetSiblingIndex() + 1);
            Destroy(shell.Find(_rankUp.name).gameObject);
            Destroy(shell.Find(_glow.name).gameObject);
            if (!withLoad)
                Destroy(shell.Find(_floor.name).gameObject);
            shell.Find(_porter.name).localScale = new Vector3(direction < 0f ? -1f : 1f, 1f, 1f);
            var group = shell.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            _ghosts.Add(new Ghost { Rect = shell, Group = group, From = _carry.anchoredPosition, Direction = direction, Clock = jump ? -JumpTime : 0f });
            return true;
        }

        // 리롤 직전(ShopPanel) — 보이는 짐꾼(들어오던 중이어도)은 짐을 인 채 오른쪽으로 나가고, 새 진열은 같은 기물이어도
        // 퇴장이 끝난 뒤(ShopPanel.ExitLeft) 왼쪽에서 걸어 들어온다. 아직 안 보이는(기다리던) 짐꾼은 껍데기를 남기지 않는다 — 연타해도 쌓이지 않는다.
        // 돌려주는 값 = 껍데기를 내보냈나
        public bool LeaveForReroll()
        {
            bool left = _last != null && Leave(1f, true);
            _last = null;
            _waitHidden = true;
            // 옛 글은 누르는 그 프레임에 사라진다(새 진열 글이 먼저 비치지 않게)
            _labels.gameObject.SetActive(false);
            return left;
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

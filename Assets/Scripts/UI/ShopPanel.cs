using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 상점 — 지도 씬에서 지도 영역을 덮는 팝업(사용자 10/11 · 여닫기는 WorldManager): 내 탑 · 진열 · 보관함 · 거래 버튼 · 큰 「지도로」.
    // 왼쪽 패널과 위 줄은 지도의 것이 그대로 보인다. 상대의 탑은 여기서 보이지 않는다(지도 패널에서 본다).
    // 끌어서 옮긴다: 칸 → 칸(빈 칸이면 이동, 차 있으면 맞바꿈) · 진열 → 칸(그 칸에 구매) · 칸 → 판매 버튼(판매).
    // 누르면: 칸은 선택(설명·판매 버튼), 진열은 빈 칸에 구매
    public class ShopPanel : MonoBehaviour
    {
        [SerializeField] private OfferCard _offerTemplate;
        [SerializeField] private float _offerStep = 250f;
        [SerializeField] private Button _reroll;
        [SerializeField] private Text _rerollLabel;
        [Tooltip("지금 라운드의 진열 등급 확률(RunState.OfferChance) — 등급 색 숫자 다섯 개(일반 · 고급 · 희귀 · 영웅 · 전설 순)")]
        [SerializeField] private Text _chances;
        [Tooltip("진열 잠금 — 잠그면 다음 라운드에 같은 진열이 나온다(RunState.ShopLocked)")]
        [SerializeField] private Button _lock;
        [SerializeField] private Text _lockLabel;
        [Tooltip("층 올리기 — 골드를 내고 탑 맨 위에 빈 층을 하나 더한다(RunState.RaiseFloor)")]
        [SerializeField] private Button _raise;
        [SerializeField] private Text _raiseLabel;
        [SerializeField] private Button _sell;
        [SerializeField] private Text _sellLabel;
        [Tooltip("「지도로」 — 지도 씬으로 돌아간다(침략할 땅은 지도에서 고른다)")]
        [SerializeField] private Button _start;
        [Tooltip("끄는 동안 포인터를 따라다니는 그림 — 레이캐스트를 막지 않아야 아래 칸이 놓기를 받는다")]
        [SerializeField] private RectTransform _dragGhost;
        [SerializeField] private Image _dragGhostIcon;
        [Tooltip("고스트도 탑 층 조각 — 끄는 기물의 벽·창(사용자 10/9) · 창틀은 그 등급 색")]
        [SerializeField] private FloorLook _dragGhostFloor;
        [SerializeField] private Image _dragGhostWindow;
        [Tooltip("끄는 동안 탑 층 경계에 뜨는 끼워 넣기 선(가운데 기준) — 레이캐스트를 막지 않아야 한다")]
        [SerializeField] private RectTransform _insertLine;
        [Tooltip("상점에서 설명 창이 서는 자리(보이지 않는 칸) — 진열 · 확률 · 보관함을 덮지 않는 빈 곳(사용자 10/20 U5). 비우면 칸 옆에 뜬다")]
        [SerializeField] private RectTransform _tooltipDock;
        [Tooltip("진열 기물을 이고 선 짐꾼 — 기물 국가마다 유닛 하나(공용 · 킹덤 · 티크톡 · 트라이브 순, 사용자 10/23). 무기는 빼고 그린다")]
        [SerializeField] private UnitDefinition[] _porters = new UnitDefinition[0];

        // 진열 무대(사용자 10/23) — 땅 선(위끝 = 짐꾼 발) · 먼 산(내 국가의 국가 선택 무대 그림을 원본 크기로 · 국가 선택과 같은 회색). 팝업 좌표(왼쪽 위 기준, px)
        private const float StageLeft = 420f, StageRight = 1906f, StageTop = 67f;
        private const float GroundPad = 16f, GroundHeight = 4f;
        private static readonly Color BackdropTint = new Color(0.14f, 0.14f, 0.14f);

        // 팝업이 다 내려와 누를 수 있게 됐나(WorldManager가 미끄러지는 동안 레이캐스트를 끈다) — 짐꾼은 그 뒤에 걸어 들어온다
        private CanvasGroup _group;
        public bool Settled => _group == null || _group.blocksRaycasts;

        // 리롤 퇴장이 끝나기까지 남은 시간(초) — 새 진열의 짐꾼은 이게 0이 된 뒤에 들어온다
        public float ExitLeft { get; private set; }

        public UnitDefinition PorterOf(BuildingNation nation) => (int)nation < _porters.Length && _porters[(int)nation] != null ? _porters[(int)nation] : _porters.Length > 0 ? _porters[0] : null;

        // 「지도로」를 눌렀다 — 상점을 닫는 일은 이 판을 띄운 쪽이 한다
        public event System.Action MapRequested;

        private OfferCard[] _offers;
        private ButtonHover _sellHover;
        private bool _hasSelection;
        private BoardView.Source _selectedSource;
        private int _selectedIndex;

        // 합치기 재생(UI/MergeFx) · 방금 산 조각이 출발한 화면 자리(진열 카드나 놓은 자리) · 자리를 묻는 두 줄
        private MergeFx _merge;
        private Vector3 _buyFrom;
        private BoardView _fieldView, _storageView;

        private bool _dragging;
        private bool _dragFromOffer;
        private BoardView.Source _dragSource;
        private int _dragIndex;
        // 끄는 속도(화면 px/초) — 보관함 상자에 놓으면 이 속도로 던져진다
        private Vector2 _dragVelocity;
        private float _dragTime;

        private void Start()
        {
            _group = GetComponent<CanvasGroup>();
            BuildOffers();
            BuildStage(((RectTransform)_offerTemplate.transform).anchoredPosition.y + OfferCard.GroundY);
            _reroll.onClick.AddListener(() =>
            {
                if (!RunState.CanReroll)
                    return;
                // 짐을 든 짐꾼이 하나라도 나가면 다 나갈 때까지 새 줄은 기다린다 — 나가는 중에 또 누르면 그 퇴장을 그대로 이어 간다
                bool leaving = false;
                foreach (OfferCard card in _offers)
                    leaving |= card.LeaveForReroll();
                if (leaving)
                    ExitLeft = OfferCard.WalkTime;
                RunState.Reroll();
            });
            _lock.onClick.AddListener(RunState.ToggleShopLock);
            _raise.onClick.AddListener(RunState.RaiseFloor);
            _sell.onClick.AddListener(SellSelected);
            _start.onClick.AddListener(() => MapRequested?.Invoke());
            _dragGhost.gameObject.SetActive(false);
            _sellHover = _sell.GetComponent<ButtonHover>();
            _merge = gameObject.AddComponent<MergeFx>();
            foreach (BoardView view in GetComponentsInChildren<BoardView>(true))
                if (view.Kind == BoardView.Source.Storage)
                    _storageView = view;
                else
                    _fieldView = view;
            // 합치기 후광은 내 탑(필드 칸들) 바로 뒤에 그린다
            _merge.Init(_dragGhost, (RectTransform)_fieldView.transform);
            if (_towerLayer != null)
            {
                var field = (RectTransform)_fieldView.transform;
                field.SetParent(_towerLayer, false);
                field.anchoredPosition += new Vector2(_towerX - _fieldView.TemplateX, -FrameBars.Floor - _fieldView.TemplateBottom);   // 1층 아래 끝 = 바닥 막대 위(UI/FrameBars)
                // 끄는 조각과 끼울 자리 선도 그 층으로 — 팝업 안에 두면 왼쪽 패널 뒤에 그려진다
                _insertLine.SetParent(_towerLayer, false);
                _dragGhost.SetParent(_towerLayer, false);
            }
        }

        // 툴팁을 띄운 진열 카드 · 보관함 기물(없으면 null) — 내 탑이 자리 무관 대상에 꺾쇠를 띄운다(BoardView.ShowTargets, 사용자 10/29)
        public OwnedBuilding HoveredUnplaced()
        {
            if (_offers != null)
                foreach (OfferCard card in _offers)
                    if (card.TooltipBuilding != null)
                        return card.TooltipBuilding;
            return _storageView != null ? _storageView.TooltipBuilding() : null;
        }

        // 진열 짐꾼 줄 — 진열 칸 수만큼(나라 소식 「이주민 행렬」로 칸이 늘면 Update가 다시 세운다)
        private void BuildOffers()
        {
            if (_offers != null)
                foreach (OfferCard old in _offers)
                    Destroy(old.gameObject);
            _offers = new OfferCard[RunState.Offers.Length];
            // 짐꾼 줄의 가운데 = 무대(땅 선 · 먼 산)의 가운데(사용자 10/23 — 오른쪽으로 치우쳐 보였다)
            float first = Mathf.Round((StageLeft + StageRight) * 0.5f - (_offers.Length - 1) * 0.5f * _offerStep);
            for (int i = 0; i < _offers.Length; i++)
            {
                OfferCard card = Instantiate(_offerTemplate, _offerTemplate.transform.parent);
                var rect = (RectTransform)card.transform;
                rect.anchoredPosition = new Vector2(first + i * _offerStep, ((RectTransform)_offerTemplate.transform).anchoredPosition.y);
                int index = i;
                card.Bind(this, index);
                card.Button.onClick.AddListener(() => BuyThrown(card, index));
                card.gameObject.SetActive(true);
                _offers[i] = card;
            }
        }

        // 진열 뒤 무대 — 판(Back) 바로 위에 먼 산(가려 자르는 판 안에 이어 붙임)과 땅 선. groundTop = 땅 선 위끝의 y(팝업 좌표)
        private void BuildStage(float groundTop)
        {
            var stage = (RectTransform)new GameObject("Stage", typeof(RectTransform), typeof(RectMask2D)).transform;
            stage.SetParent(transform, false);
            stage.SetSiblingIndex(1);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0f, 1f);
            stage.anchoredPosition = new Vector2(StageLeft, -StageTop);
            stage.sizeDelta = new Vector2(StageRight - StageLeft, -StageTop - groundTop);
            NationShowcase show = RunState.Config.World.Showcase(RunState.NationOf(Team.Ally));
            Sprite hill = show != null ? show.Backdrop : null;
            if (hill != null)
                for (float x = 0f; x < stage.sizeDelta.x; x += hill.rect.width)
                {
                    var image = new GameObject("Hill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.rectTransform.SetParent(stage, false);
                    image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = Vector2.zero;
                    image.rectTransform.anchoredPosition = new Vector2(x, 0f);
                    image.rectTransform.sizeDelta = hill.rect.size;
                    image.sprite = hill;
                    image.color = BackdropTint;
                    image.raycastTarget = false;
                }
            var ground = new GameObject("Ground", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            ground.rectTransform.SetParent(transform, false);
            ground.rectTransform.SetSiblingIndex(2);
            ground.rectTransform.anchorMin = ground.rectTransform.anchorMax = ground.rectTransform.pivot = new Vector2(0f, 1f);
            ground.rectTransform.anchoredPosition = new Vector2(StageLeft + GroundPad, groundTop);
            ground.rectTransform.sizeDelta = new Vector2(StageRight - StageLeft - GroundPad * 2f, GroundHeight);
            ground.raycastTarget = false;
        }

        // 내 탑을 팝업 밖에 세운다(사용자 10/12: 상점이 열리면 왼쪽 패널의 탑 기둥에 탑 하나만) — layer = 탑이 설 판(화면 전체 크기), x = 1층 가운데의 가로 자리.
        // 판은 부르는 쪽(WorldManager)이 팝업과 같이 켜고 끈다. 팝업이 처음 켜지기 전에 부른다
        private RectTransform _towerLayer;
        private float _towerX;

        public void PlaceTower(RectTransform layer, float x)
        {
            _towerLayer = layer;
            _towerX = x;
        }

        private void OnEnable()
        {
            RunState.Merged += OnMerged;
            BuildingTooltip.Dock = _tooltipDock;
        }

        // 합치기가 일어났다(상태는 이미 끝났다) — 한 단계씩 재생한다. 단계의 자리는 합치기 전 모습 기준이고, 칸 그림은 아직 그 모습이다
        private void OnMerged(System.Collections.Generic.IReadOnlyList<RunState.MergeStep> steps)
        {
            var targets = new Vector3[steps.Count];
            for (int i = 0; i < targets.Length; i++)
                targets[i] = (steps[i].Storage ? _storageView : _fieldView).SlotPosition(steps[i].Index);
            OwnedBuilding result = steps[steps.Count - 1].Into;
            _merged = true;
            _merge.Play(result.Definition, _buyFrom, steps, targets, () => PlaceOf(result), _throwing);
        }

        // 진열을 눌러 산다 — 짐꾼이 뛰어오르며 층 조각을 그 기물이 들어간 칸으로 던진다(사용자 10/23). 합치기면 그 던지기가 합치기 재생의 첫 비행이다.
        // 끌어서 사는 건 손으로 옮긴 것이라 던지지 않는다
        private bool _throwing;

        private void BuyThrown(OfferCard card, int index)
        {
            if (!RunState.CanBuy(index))
                return;
            _buyFrom = card.LoadPosition;
            var before = new System.Collections.Generic.HashSet<OwnedBuilding>(RunState.Field);
            before.UnionWith(RunState.Storage);
            card.Jump();
            _throwing = true;
            _merged = false;
            RunState.Buy(index);
            _throwing = false;
            if (_merged)
                return;
            // 합쳐지지 않았으면 새로 생긴 기물이 들어간 칸으로
            foreach (OwnedBuilding[] row in new[] { RunState.Field, RunState.Storage })
                foreach (OwnedBuilding building in row)
                    if (!OwnedBuilding.IsEmpty(building) && !before.Contains(building))
                    {
                        _merge.Throw(building, _buyFrom, () => PlaceOf(building));
                        return;
                    }
        }

        private bool _merged;

        // 그 기물이 지금 놓인 칸의 화면 자리(탑 · 보관함)
        private Vector3 PlaceOf(OwnedBuilding building)
        {
            int index = System.Array.IndexOf(RunState.Field, building);
            return index >= 0 ? _fieldView.SlotPosition(index) : _storageView.SlotPosition(Mathf.Max(0, System.Array.IndexOf(RunState.Storage, building)));
        }

        // 합치기 재생 중이거나 던진 조각이 나는 중이라 실제 칸을 숨길 기물인가(재생 · 비행이 끝나며 나타난다)
        public bool Concealed(OwnedBuilding building) => building != null && _merge != null && (_merge.Concealed == building || _merge.Thrown(building));

        // 합치기 재생이 도는 중인가 — 고르기 창(UI/UpgradePicker)이 재생이 끝난 뒤에 카드를 보인다
        public bool MergePlaying => _merge != null && _merge.Playing;

        private void OnDisable()
        {
            RunState.Merged -= OnMerged;
            ExitLeft = 0f;
            if (BuildingTooltip.Dock == _tooltipDock)
                BuildingTooltip.Dock = null;
            _hasSelection = false;
            _dragging = false;
            if (_dragGhost != null)
                _dragGhost.gameObject.SetActive(false);
            _insertAt = -1;
            if (_insertLine != null)
                _insertLine.gameObject.SetActive(false);
        }

        public bool Dragging => _dragging;
        public bool IsSelected(BoardView.Source source, int index) => _hasSelection && _selectedSource == source && _selectedIndex == index;
        public bool IsDragging(BoardView.Source source, int index) => _dragging && !_dragFromOffer && _dragSource == source && _dragIndex == index;

        public void OnSlotClicked(BoardView.Source source, int index)
        {
            if (BattleManager.Fighting || IsSelected(source, index) || OwnedBuilding.IsEmpty(BuildingAt(source, index)))
            {
                _hasSelection = false;
                return;
            }
            _hasSelection = true;
            _selectedSource = source;
            _selectedIndex = index;
        }

        // --- 끌기 ---

        public void BeginDragSlot(BoardView.Source source, int index, PointerEventData eventData)
        {
            // 재생 중에 조각을 집으면 재생을 바로 끝낸다 — 칸이 끝 상태와 어긋난 채로 집히지 않게(나는 던진 조각도)
            _merge.Finish();
            _merge.FinishThrows();
            if (BattleManager.Fighting || source == BoardView.Source.EnemyField)
                return;
            OwnedBuilding building = BuildingAt(source, index);
            if (OwnedBuilding.IsEmpty(building))
                return;
            StartDrag(false, source, index, building.Definition, building.Rarity, eventData);
        }

        public void BeginDragOffer(int index, PointerEventData eventData)
        {
            _merge.Finish();
            BuildingDefinition offer = RunState.Offers[index];
            if (BattleManager.Fighting || offer == null)
                return;
            // 중립외교로 오른 칸이면 오른 등급의 창틀
            StartDrag(true, default, index, offer, offer.Rarity + RunState.OfferUpgrades[index], eventData);
        }

        private void StartDrag(bool fromOffer, BoardView.Source source, int index, BuildingDefinition definition, BuildingRarity rarity, PointerEventData eventData)
        {
            _dragging = true;
            _dragFromOffer = fromOffer;
            _dragSource = source;
            _dragIndex = index;
            _hasSelection = false;
            _dragGhostIcon.sprite = definition.Icon;
            _dragGhostFloor.Show(definition);
            _dragGhostWindow.color = rarity.FrameColor();
            _dragGhost.gameObject.SetActive(true);
            _dragVelocity = Vector2.zero;
            _dragTime = Time.unscaledTime;
            Drag(eventData);
        }

        public void Drag(PointerEventData eventData)
        {
            if (!_dragging)
                return;
            float dt = Time.unscaledTime - _dragTime;
            if (dt > 0f)
            {
                _dragVelocity = Vector2.Lerp(_dragVelocity, eventData.delta / dt, 0.5f);
                _dragTime = Time.unscaledTime;
            }
            _dragGhost.position = eventData.position;   // Overlay 캔버스라 화면 좌표 = 월드 좌표
            FindInsert(eventData);
        }

        // 놓기(OnDrop)가 끝난 뒤에 불린다 — 아무 데도 안 놓았으면 그냥 제자리
        public void EndDrag()
        {
            _dragging = false;
            _dragGhost.gameObject.SetActive(false);
            _insertAt = -1;
            _insertLine.gameObject.SetActive(false);
        }

        // --- 층 사이 끼워 넣기(사용자 10/8: 1 2 3 4 5의 1·2층 사이에 A → 1 A 2 3 4 5) ---
        // 탑 층의 위·아래 1/4에 놓으면 그 경계에 끼워 넣는다. 가운데에 놓으면 예전대로(맞바꿈·합치기). 끄는 동안 경계선을 보여 준다
        private const float InsertZone = 0.25f;
        private int _insertAt = -1;

        private void FindInsert(PointerEventData eventData)
        {
            _insertAt = -1;
            GameObject hovered = eventData.pointerCurrentRaycast.gameObject;
            SlotView slot = hovered != null ? hovered.GetComponentInParent<SlotView>() : null;
            if (slot != null && slot.Source == BoardView.Source.AllyField)
            {
                var rect = (RectTransform)slot.transform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, null, out Vector2 local);
                float t = Mathf.InverseLerp(rect.rect.yMin, rect.rect.yMax, local.y);
                bool above = t > 1f - InsertZone;
                int at = above ? slot.Index + 1 : t < InsertZone ? slot.Index : -1;
                if (at >= 0 && CanInsert(at))
                {
                    _insertAt = at;
                    _insertLine.position = rect.TransformPoint(new Vector3(0f, above ? rect.rect.yMax : rect.rect.yMin, 0f));
                }
            }
            if (_insertLine.gameObject.activeSelf != _insertAt >= 0)
                _insertLine.gameObject.SetActive(_insertAt >= 0);
        }

        // 기물이 있는 층 사이와 맨 위 기물 위까지만 — 그 위 빈 층 경계는 결국 맨 위로 떨어지므로 보여 주지 않는다.
        // 탑 안에서 옮길 때 제자리 양옆 경계는 아무 일도 안 하므로 뺀다
        private bool CanInsert(int at)
        {
            int filled = 0;
            while (filled < RunState.Field.Length && !OwnedBuilding.IsEmpty(RunState.Field[filled]))
                filled++;
            if (at > filled)
                return false;
            if (_dragFromOffer)
                return RunState.CanBuyInsert(_dragIndex, at);
            bool fromStorage = _dragSource == BoardView.Source.Storage;
            if (!fromStorage && (at == _dragIndex || at == _dragIndex + 1))
                return false;
            return RunState.CanMoveInsert(fromStorage, _dragIndex, at);
        }

        public void DropOnSlot(BoardView.Source source, int index)
        {
            if (!_dragging || source == BoardView.Source.EnemyField)
                return;
            // 놓은 기물 — 옮기면 끈 그 기물, 사면 새로 생긴 기물(놓은 뒤 그 칸이 탁 앉는다)
            OwnedBuilding moved = _dragFromOffer ? null : BuildingAt(_dragSource, _dragIndex);
            System.Collections.Generic.HashSet<OwnedBuilding> before = _dragFromOffer ? Owned() : null;
            _merged = false;
            if (_insertAt >= 0 && source == BoardView.Source.AllyField)
            {
                if (_dragFromOffer)
                    RunState.BuyInsert(_dragIndex, _insertAt);
                else
                    RunState.MoveInsert(_dragSource == BoardView.Source.Storage, _dragIndex, _insertAt);
            }
            else
            {
                bool storage = source == BoardView.Source.Storage;
                if (_dragFromOffer)
                {
                    _buyFrom = _dragGhost.position;
                    RunState.BuyInto(_dragIndex, storage, index);
                }
                else
                    RunState.Swap(_dragSource == BoardView.Source.Storage, _dragIndex, storage, index);
            }
            ThumpWhere(_dragFromOffer ? NewBuilding(before) : moved);
        }

        // 보관함 상자의 빈 자리에 놓았다 — 보관함 빈 칸에 넣고 그 자리에서 떨어뜨린다. 꽉 찼으면 안 받는다.
        // 상자 안 조각끼리는 자리만 옮긴다(다시 던지기). 조각 위에 놓으면 DropOnSlot(맞바꿈·합치기)이 받는다
        public void DropOnBox(StorageBox box, Vector2 local)
        {
            if (!_dragging)
                return;
            Vector2 size = _dragGhost.rect.size;
            // 멈췄다 놓으면(끌기 이벤트는 움직일 때만 온다) 그냥 떨어진다
            Vector2 velocity = Time.unscaledTime - _dragTime > 0.1f ? Vector2.zero : _dragVelocity;
            if (!_dragFromOffer && _dragSource == BoardView.Source.Storage)
            {
                box.Throw(_dragIndex, local, size, velocity);
                return;
            }
            int index = StorageBox.FirstEmpty();
            if (_dragFromOffer)
            {
                // 합쳐질 짝이 있으면 상자가 꽉 차도 산다(어디 놓든 합쳐지는 규칙 그대로)
                if (!RunState.CanBuyInto(_dragIndex, true, Mathf.Max(index, 0)))
                    return;
                _buyFrom = _dragGhost.position;
                RunState.BuyInto(_dragIndex, true, Mathf.Max(index, 0));
            }
            else if (index >= 0)
                RunState.Swap(false, _dragIndex, true, index);
            if (index >= 0)
                box.Throw(index, local, size, velocity);
        }

        public void DropOnSell()
        {
            if (_dragging && !_dragFromOffer)
                SellWithFx(_dragSource == BoardView.Source.Storage, _dragIndex, _dragGhost.position);
        }

        // 판매 + 손맛(J3) — 판 기물이 at(화면 자리)에서 작아져 사라지고, 받은 골드만큼 동전이 위 줄 골드로 날아간다(닿을 때 숫자가 오른다)
        private WorldSidePanel _side;

        private void SellWithFx(bool storage, int index, Vector3 at)
        {
            OwnedBuilding building = storage ? RunState.Storage[index] : RunState.Field[index];
            int gold = RunState.Gold;
            RunState.Sell(storage, index);
            if (OwnedBuilding.IsEmpty(building) || System.Array.IndexOf(storage ? RunState.Storage : RunState.Field, building) >= 0)
                return;   // 안 팔렸다
            BattleConfig config = RunState.Config;
            _merge.SellAway(building, at, config.SellShrinkSeconds);
            if (_side == null)
                _side = FindAnyObjectByType<WorldSidePanel>();
            if (_side != null)
                _side.GainCoins(at, RunState.Gold - gold, config.SellShrinkSeconds);
        }

        // 끌어 놓은 칸이 탁 앉는다(손맛 J3) — 놓은 뒤 그 기물이 있는 칸(탑 · 보관함). 합치기면 합치기 재생이 맡는다
        private void ThumpWhere(OwnedBuilding building)
        {
            if (OwnedBuilding.IsEmpty(building) || _merged)
                return;
            int index = System.Array.IndexOf(RunState.Field, building);
            if (index >= 0)
                _fieldView.Thump(index);
            else if ((index = System.Array.IndexOf(RunState.Storage, building)) >= 0)
                _storageView.Thump(index);
            // 소리 자리(J6): 칸에 놓음
        }

        // 방금 산 기물 — before에 없던 탑 · 보관함 기물(없으면 null)
        private static OwnedBuilding NewBuilding(System.Collections.Generic.HashSet<OwnedBuilding> before)
        {
            foreach (OwnedBuilding[] row in new[] { RunState.Field, RunState.Storage })
                foreach (OwnedBuilding building in row)
                    if (!OwnedBuilding.IsEmpty(building) && !before.Contains(building))
                        return building;
            return null;
        }

        private static System.Collections.Generic.HashSet<OwnedBuilding> Owned()
        {
            var owned = new System.Collections.Generic.HashSet<OwnedBuilding>(RunState.Field);
            owned.UnionWith(RunState.Storage);
            return owned;
        }

        // --- 표시 ---

        private static OwnedBuilding BuildingAt(BoardView.Source source, int index)
        {
            switch (source)
            {
                case BoardView.Source.Storage: return RunState.Storage[index];
                case BoardView.Source.AllyField: return RunState.Field[index];
                default: return BoardRunner.For(Team.Enemy).Slots[index].Building;
            }
        }

        private bool SelectionSellable => _hasSelection && _selectedSource != BoardView.Source.EnemyField;

        private void SellSelected()
        {
            if (!SelectionSellable)
                return;
            bool storage = _selectedSource == BoardView.Source.Storage;
            SellWithFx(storage, _selectedIndex, (storage ? _storageView : _fieldView).SlotPosition(_selectedIndex));
            _hasSelection = false;
        }

        private void Update()
        {
            if (_offers.Length != RunState.Offers.Length)
                BuildOffers();
            for (int i = 0; i < _offers.Length; i++)
                _offers[i].Show(RunState.Offers[i], RunState.CanBuy(i), RunState.ShopLocked);
            // 짐꾼 걸음과 같은 시계(프레임당 1/30초까지)
            ExitLeft = Mathf.Max(0f, ExitLeft - Mathf.Min(Time.unscaledDeltaTime, 1f / 30f));

            _reroll.interactable = RunState.CanReroll;
            _rerollLabel.text = $"리롤 {RunState.RerollCost}G";
            _chances.text = ChanceLine();
            _lockLabel.text = RunState.ShopLocked ? "잠금 해제" : "잠금";
            _raise.interactable = RunState.CanRaiseFloor;
            _raiseLabel.text = RunState.Floors < RunState.MaxFloors ? $"층 올리기 {RunState.NextFloorCost}G" : "최고층";

            OwnedBuilding selected = _hasSelection ? BuildingAt(_selectedSource, _selectedIndex) : null;
            if (OwnedBuilding.IsEmpty(selected))
            {
                _hasSelection = false;
                selected = null;
            }
            // 끌어서 팔 수 있게 끄는 동안엔 판매 버튼이 늘 받는다
            bool dragSellable = _dragging && !_dragFromOffer;
            _sell.interactable = dragSellable || (SelectionSellable && selected != null);
            if (dragSellable)
                _sellLabel.text = $"판매 +{RunState.SellValue(BuildingAt(_dragSource, _dragIndex))}G";
            else
                _sellLabel.text = SelectionSellable && selected != null ? $"판매 +{RunState.SellValue(selected)}G" : "끌어서 판매";
            // 끄는 동안 판매 칸이 노랗게 — 놓는 자리임이 보이게
            _sellHover.Base = dragSellable ? DropColor : Color.white;

            // 못 누르는 버튼은 글도 회색
            Grey(_reroll, _rerollLabel);
            Grey(_raise, _raiseLabel);
            Grey(_sell, _sellLabel);
        }

        // 회색 이름표 「등장 확률」 + 등급 이름과 숫자를 등급 색(기물 테두리 색)으로 — 색만으로 등급을 가르지 않게 이름을 붙인다(사용자 10/20 U12).
        // 안 나오는 등급(0%)도 자리는 두고 흐리게
        private static string ChanceLine()
        {
            var parts = new string[5];
            for (int i = 0; i < parts.Length; i++)
            {
                var rarity = (BuildingRarity)i;
                int percent = Mathf.RoundToInt(RunState.OfferChance(rarity) * 100f);
                string color = ColorUtility.ToHtmlStringRGB(rarity.FrameColor()) + (percent == 0 ? ZeroChanceAlpha : "FF");
                parts[i] = $"<color=#{color}>{rarity.DisplayName()} {percent}%</color>";
            }
            return $"<color={BuildingTooltip.NoteColor}>등장 확률</color>  " + string.Join("  ", parts);
        }

        private const string ZeroChanceAlpha = "59";   // 35%
        private static readonly Color DropColor =new Color(1f, 0.85f, 0.35f);
        private static readonly Color DisabledText = new Color(0.6f, 0.6f, 0.66f);

        private static void Grey(Button button, Text label) => label.color = button.interactable ? Color.white : DisabledText;
    }
}

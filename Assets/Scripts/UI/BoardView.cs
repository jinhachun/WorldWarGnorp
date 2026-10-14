using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 건물 칸 한 줄 — 아군 필드 · 적 필드 · 보관함. 틀 칸(꺼져 있음)을 칸 수만큼 복제해 늘어놓는다.
    // 필드 줄은 전장 탑처럼 세로로 쌓는다(사용자 10/8) — 칸 0(1층)이 맨 아래, 맨 위 층 위에 진영색 지붕.
    // 층 수는 상점에서 늘 수 있으므로(층 올리기) 최대 층 수만큼 만들어 두고 지금 층 수만큼만 보인다
    public class BoardView : MonoBehaviour
    {
        public enum Source { AllyField, EnemyField, Storage }

        [SerializeField] private Source _source;
        [SerializeField] private SlotView _template;
        [Tooltip("칸 간격(px) — 가로면 음수일 때 왼쪽으로 늘어선다. 세로면 위아래 칸 사이 거리")]
        [SerializeField] private float _step = 130f;
        [Tooltip("세로로 쌓는다 — 틀 칸 자리가 1층(바닥 고정), 위로 갈수록 높은 층")]
        [SerializeField] private bool _stack;
        [Tooltip("세로일 때 맨 위 층 위에 얹는 지붕(아래 가운데 기준) — 층 수에 따라 따라 올라간다")]
        [SerializeField] private RectTransform _roof;
        [SerializeField] private ShopPanel _shop;
        [Tooltip("전투 중에만 보인다 — 상점에선 가운데 필드 줄이 대신 보여 준다")]
        [SerializeField] private bool _battleOnly;
        [Tooltip("보관함만 — 칸들을 상자에 던져 놓는다(빈 칸 숨김 · 자리는 상자가 정함). 비우면 가로 줄")]
        [SerializeField] private StorageBox _box;

        private SlotView[] _views;
        // 칸마다 지난 프레임의 남은 가속 — 늘어나면 방금 가속받은 것
        private float[] _haste;
        private BoardRunner _subscribed;
        private Vector2 _origin;
        // 탑(세로 줄)만 — 층 → 층 버프 선
        private UiBuffLinkLines _links;

        private void Start()
        {
            // 지도 씬(상점 팝업)에는 돌고 있는 보드가 없다 — 내 탑은 런 상태에서 읽는다
            BoardRunner runner = _source == Source.Storage ? null : Runner;
            int capacity = _source == Source.Storage ? RunState.Storage.Length : Mathf.Max(RunState.MaxFloors, runner != null ? runner.Slots.Length : 0);
            _views = new SlotView[capacity];
            _haste = new float[capacity];
            _origin = ((RectTransform)_template.transform).anchoredPosition;
            if (runner != null)
            {
                _subscribed = runner;
                _subscribed.Triggered += OnTriggered;
            }
            OwnedBuilding.EffectFired += OnEffectFired;
            if (_stack)
                BuffLinks.Raised += OnBuffLink;
            for (int i = 0; i < capacity; i++)
            {
                SlotView view = Instantiate(_template, _template.transform.parent);
                if (!_stack)
                    ((RectTransform)view.transform).anchoredPosition = _origin + new Vector2(i * _step, 0f);
                int index = i;
                view.Bind(_shop, _source, index);
                if (_shop != null)
                    view.Button.onClick.AddListener(() => _shop.OnSlotClicked(_source, index));
                view.gameObject.SetActive(false);
                _views[i] = view;
            }
            if (_stack)
            {
                // 버프 선 — 층들 뒤에 그린다(첫 형제) — 칸 옆 효과 표시(가속 초 · 쿨다운 배율) 글자를 선이 덮지 않게.
                // 자리는 1층 틀 칸에 맞춰, 선 좌표를 "1층 기준"으로 쓴다
                RectTransform slot = (RectTransform)_template.transform;
                var lines = new GameObject("BuffLinkLines", typeof(RectTransform));
                var rect = (RectTransform)lines.transform;
                rect.SetParent(slot.parent, false);
                rect.SetAsFirstSibling();
                rect.anchorMin = slot.anchorMin;
                rect.anchorMax = slot.anchorMax;
                rect.pivot = slot.pivot;
                rect.anchoredPosition = _origin;
                rect.sizeDelta = Vector2.zero;
                _links = lines.AddComponent<UiBuffLinkLines>();
                _links.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed != null)
                _subscribed.Triggered -= OnTriggered;
            OwnedBuilding.EffectFired -= OnEffectFired;
            BuffLinks.Raised -= OnBuffLink;
        }

        // 기물이 다른 기물에게 준 효과 — 이 탑의 준 층 → 받은 층으로 선을 긋는다(유닛·타워 대상은 전장에서만 그린다).
        // 탑이 숨어 있으면(전장 탑이 화면에 보일 때 · 꺼진 줄) 안 그린다
        private void OnBuffLink(BuffLink link)
        {
            if (_links == null || link.To < 0 || link.To == link.From
                || link.Team != (_source == Source.EnemyField ? Team.Enemy : Team.Ally)
                || !gameObject.activeInHierarchy)
                return;
            // 칸이 켜져 있는지·Fighting이 아니라 상점이 열렸는지로 본다 — 전투 시작 시 효과는 Fighting이 서기 전, 칸이 켜지기(Update) 전에 온다
            if (InShop ? _battleOnly : TowerOnScreen())
                return;
            int count = Count;
            if (link.To >= count || link.From >= count)
                return;
            RectTransform slot = (RectTransform)_template.transform;
            // 화면 안쪽으로 불룩하게 — 왼쪽 탑은 오른쪽으로, 오른쪽 탑은 왼쪽으로
            float side = slot.position.x < _links.canvas.rootCanvas.transform.position.x ? 1f : -1f;
            Vector2 edge = slot.rect.center + new Vector2(side * slot.rect.width * 0.5f, 0f);
            // 맵 효과(준 기물 없음)는 탑 옆에 뜬 출발점 그림(얼음)에서, 그 그림이 없으면 탑 꼭대기 위에서 내려온다
            Vector2 from = link.From < 0 && _marker != null && _marker.enabled ? edge + MarkerOffset(side) : edge + new Vector2(0f, (link.From >= 0 ? link.From : count + 0.5f) * _step);
            Vector2 to = edge + new Vector2(0f, link.To * _step);
            _links.Add(from, to, side, link.Kind, link.Weak, ((link.From + 1) * 4096 + link.To * 64 + (int)link.Kind) * 2 + (link.Weak ? 1 : 0));
        }

        // ── 맵 효과의 출발점 그림(얼음 맵의 얼음 — 전장의 Fx/MapMarker와 같은 그림) — 전투 화면의 UI 탑 옆, 화면 안쪽에 살짝 떠 있다.
        // 탑이 숨으면 같이 숨고, 그 맵 효과가 없는 전투에는 없다. 그림은 도트를 정수배로(전장의 얼음과 비슷한 크기)
        private const int MarkerPixelScale = 3;
        // 탑 옆으로 띄우는 거리(px) · 1층 가운데에서 위로(층) · 흔들리는 폭(px) · 한 번 오르내리는 시간(초)
        private const float MarkerAside = 36f;
        private const float MarkerFloors = 1.5f;
        private const float MarkerBob = 4f;
        private const float MarkerBobSeconds = 2.4f;

        private Image _marker;

        // 층 옆 가장자리(edge)에서 출발점 그림까지
        private Vector2 MarkerOffset(float side) => new Vector2(side * MarkerAside, MarkerFloors * _step);

        private void ShowMarker(bool visible)
        {
            Sprite sprite = _stack && BattleManager.Fighting && RunState.Map != null && RunState.Map.FireAllInterval > 0f ? RunState.Map.FireAllMarker : null;
            if (sprite == null || !visible)
            {
                if (_marker != null)
                    _marker.enabled = false;
                return;
            }
            RectTransform slot = (RectTransform)_template.transform;
            if (_marker == null)
            {
                _marker = new GameObject("MapMarker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _marker.raycastTarget = false;
                _marker.preserveAspect = true;
                RectTransform rect = _marker.rectTransform;
                rect.SetParent(slot.parent, false);
                rect.anchorMin = slot.anchorMin;
                rect.anchorMax = slot.anchorMax;
            }
            _marker.enabled = true;
            _marker.sprite = sprite;
            _marker.rectTransform.sizeDelta = sprite.rect.size * MarkerPixelScale;
            float side = slot.position.x < _marker.canvas.rootCanvas.transform.position.x ? 1f : -1f;
            float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / MarkerBobSeconds) * MarkerBob);
            _marker.rectTransform.anchoredPosition = _origin + new Vector2(side * slot.rect.width * 0.5f, bob) + MarkerOffset(side);
        }

        // 칸이 발동한 순간 — 그 칸이 튄다
        private void OnTriggered(int index)
        {
            if (index < _views.Length && _views[index].gameObject.activeSelf)   // 숨은 칸은 나중에 켜질 때 늦게 튀지 않게
                _views[index].Bounce();
        }

        // 쿨다운 밖의 효과(「구매할 때마다,」·전투 시작 시·리롤·골드)를 내거나 받은 기물 — 그 기물이 보이는 줄의 칸이 튄다.
        // 꺼진 줄(전투 중 상점 줄)과 상점 동안의 전투 전용 줄은 받지 않는다 — 나중에 켜질 때 늦게 튀지 않게
        private void OnEffectFired(OwnedBuilding fired)
        {
            if (_views == null || !gameObject.activeInHierarchy || (_battleOnly && InShop))
                return;
            int count = Count;
            for (int i = 0; i < count; i++)
                if (BuildingAt(i) == fired && _views[i].gameObject.activeSelf)
                    _views[i].Bounce();
        }

        // 지금 칸 수 — 아군 필드는 상점에서 층을 올리면 늘어난다
        private int Count
        {
            get
            {
                int count = _source == Source.Storage ? RunState.Storage.Length
                    : _source == Source.AllyField && !BattleManager.Fighting ? RunState.Field.Length
                    : Runner.Slots.Length;
                return Mathf.Min(count, _views.Length);
            }
        }

        // 아군 필드는 상점에서 바뀌므로 전투 전엔 RunState를, 전투 중엔 돌고 있는 보드를 본다
        private OwnedBuilding BuildingAt(int i)
        {
            if (_source == Source.Storage)
                return RunState.Storage[i];
            if (_source == Source.AllyField && !BattleManager.Fighting)
                return RunState.Field[i];
            return Runner.Slots[i].Building;
        }

        // 이 줄이 상점 판에 놓였고 그 판이 열려 있나 — 전투 화면의 줄에는 상점 판이 없다
        private bool InShop => _shop != null && _shop.gameObject.activeInHierarchy;

        public Source Kind => _source;
        // 틀 칸(1층)의 가로 자리 — 탑을 다른 기둥으로 옮겨 세울 때 얼마나 옮길지 잰다(UI/ShopPanel.PlaceTower)
        public float TemplateX => ((RectTransform)_template.transform).anchoredPosition.x;
        // index칸 그림의 화면 자리(지금 그려진 모습 기준)
        public Vector3 SlotPosition(int index) => _views[index].transform.position;

        // 지금 보이는 칸들과 지붕을 감싸는 화면 사각형(Overlay 캔버스 — 화면 픽셀). 하나도 안 보이면 false (UI/BaseUpgradeRow가 겹침을 피한다)
        public bool ScreenBounds(out Rect bounds)
        {
            bounds = default;
            bool any = false;
            var corners = new Vector3[4];
            for (int i = 0; _views != null && i <= _views.Length; i++)
            {
                RectTransform rect = i < _views.Length ? (RectTransform)_views[i].transform : _roof;
                if (rect == null || !rect.gameObject.activeInHierarchy)
                    continue;
                rect.GetWorldCorners(corners);
                Rect r = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                bounds = any ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin), Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax)) : r;
                any = true;
            }
            return any;
        }

        private BoardRunner Runner => BoardRunner.For(_source == Source.EnemyField ? Team.Enemy : Team.Ally);

        // 전투 중 필드 줄(아군·적)은 그 진영 성 위 탑이 화면에 보이면 숨는다 — 탑이 같은 기물을 보여 주므로(사용자 10/8).
        // 보인다 = 탑 가로 가운데가 카메라 화면 가로 범위 안. 경계에 여유를 둬 화면 흔들림에 깜빡이지 않게
        private const float TowerEdgeMargin = 0.25f;
        private bool _towerOnScreen;

        private bool TowerOnScreen()
        {
            Tower tower = Tower.For(_source == Source.EnemyField ? Team.Enemy : Team.Ally);
            Camera cam = Camera.main;
            if (tower == null || cam == null)
                return false;
            float dx = Mathf.Abs(tower.transform.position.x - cam.transform.position.x);
            float half = cam.orthographicSize * cam.aspect + (_towerOnScreen ? TowerEdgeMargin : -TowerEdgeMargin);
            _towerOnScreen = dx <= half;
            return _towerOnScreen;
        }

        private void Update()
        {
            bool visible = BattleManager.Fighting
                ? _source == Source.Storage || !TowerOnScreen()
                : !_battleOnly;
            int count = Count;
            ShowMarker(visible);
            if (_links != null && !visible)
                _links.Clear();
            if (_roof != null)
            {
                if (_roof.gameObject.activeSelf != visible)
                    _roof.gameObject.SetActive(visible);
                // 지붕은 맨 위 층 바로 위를 따라간다
                float slotHalf = ((RectTransform)_template.transform).rect.height * 0.5f;
                _roof.anchoredPosition = _origin + new Vector2(0f, (count - 1) * _step + slotHalf);
                // 지붕은 그 탑 영주의 색(사용자 10/13) — 전장의 탑 지붕과 같은 색
                _roof.GetComponent<Image>().color = RunState.LordColor(_source == Source.EnemyField ? Team.Enemy : Team.Ally);
            }
            for (int i = 0; i < _views.Length; i++)
            {
                // 보관함 상자는 빈 칸을 보이지 않는다 — 들어 있는 조각만 상자 안에 쌓인다
                // 합치기 재생 중인 결과 기물은 빈 칸으로 보인다 — 재생이 끝나며 그 자리에 나타난다(UI/MergeFx)
                OwnedBuilding building = i < count ? BuildingAt(i) : null;
                if (InShop && _shop.Concealed(building))
                    building = null;
                bool shown = visible && i < count && (_box == null || !OwnedBuilding.IsEmpty(building));
                if (_views[i].gameObject.activeSelf != shown)
                    _views[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                // 1층이 틀 칸 자리(바닥 고정) — 층이 늘면 위로 자란다. 아군·적 탑의 높이를 바닥부터 견줘 보게(사용자 10/8)
                if (_stack)
                    ((RectTransform)_views[i].transform).anchoredPosition = _origin + new Vector2(0f, i * _step);

                BoardRunner.Slot running = _source != Source.Storage && BattleManager.Fighting ? Runner.Slots[i] : null;
                // 가속받은 칸도 튄다 — 쿨다운 없는 칸은 가속이 아무 일도 안 하므로 빼고
                float haste = running != null && running.Cooldown > 0f ? running.Haste : 0f;
                if (haste > _haste[i])
                    _views[i].Bounce();
                _haste[i] = haste;
                _views[i].Show(building, running, InShop && _shop.IsSelected(_source, i), InShop && _shop.IsDragging(_source, i));
            }
            if (_box != null && visible)
                _box.Place(_views);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 성 안에서 솟은 탑 — 필드 칸 하나가 한 층(칸 0 = 1층, 성벽 윗면 바로 위). 그 진영 보드(BoardRunner.Field)의 길이만큼 층을 쌓는다.
    // 층마다 양쪽 벽이 위아래로 이어지고, 층 바닥엔 살짝 튀어나온 처마선, 가운데엔 창문 하나 — 창 안에 그 기물 아이콘(사용자 10/9).
    // 벽 재질은 그 층 기물의 국가: 킹덤 = 돌벽돌 + 아치 창 · 티크톡 = 리벳 박힌 철판 + 톱니 테 둥근 창 · 공용·빈 층 = 민 벽 + 아치 창.
    // 창 불빛이 쿨다운 게이지 — 전투 중 쿨다운이 차는 만큼 창이 아래부터 밝아지고, 발동하면 아이콘이 튀고 창이 번쩍인다.
    // 쿨다운 없는 기물·상점에서는 창이 은은히 켜져 있고, 빈 층은 창이 어둡다.
    // 맨 위 층 위엔 진영색 지붕과 깃발. 기물이 소환한 유닛은 그 층 높이, 탑 앞쪽 가장자리 밖에서 나와 뛰어내린다(BoardRunner.Summon).
    // 양 진영 같은 코드 — 앞쪽 방향만 진영에서 읽는다
    [RequireComponent(typeof(Base))]
    public class Tower : MonoBehaviour
    {
        private const int PixelsPerUnit = 8;
        private const float Pixel = 1f / PixelsPerUnit;
        private const float DeckThickness = 0.1f;
        // 발동한 층 — 아이콘이 잠깐 커졌다 돌아오고(SlotView와 같은 값) 창이 하얗게 번쩍였다 사라진다. 배속과 상관없이 같은 길이로
        private const float BounceScale = 1.2f;
        private const float BounceDuration = 0.2f;
        private const float FlashAlpha = 0.6f;
        // 창 불빛 — 쿨다운 없는 기물·상점에서는 이 알파로 은은히
        private static readonly Color LightColor = new Color(1f, 0.7f, 0.2f);
        private const float DimLight = 0.4f;
        // 벽 무늬(벽돌 이음·철판 이음) — 흰 선을 낮춘 회색이라 테두리·창보다 뒤로 물러난다
        private static readonly Color PatternColor = new Color(0.35f, 0.35f, 0.35f);

        // 창 자리(층 그림 픽셀) — 아래에서 5번째 줄부터 위에서 4번째 줄까지. 아치 창은 안쪽 폭 10px(위가 반지름 5 반원), 둥근 창은 그 높이가 지름
        private const int WindowBottom = 5;
        private const int WindowTopMargin = 4;
        private const float ArchRadius = 5f;

        [Tooltip("층 폭(칸) — 성 두 망루 사이 가운데에서 솟는다")]
        [SerializeField] private float _floorWidth = 6f;
        [Tooltip("층 높이(칸)")]
        [SerializeField] private float _floorHeight = 2.5f;
        [Tooltip("성 그림 아래 끝에서 가운데 성벽 윗면까지(칸) — 1층 바닥")]
        [SerializeField] private float _wallTop = 4.375f;
        [Tooltip("아이콘 크기 — 창 안쪽 높이의 이 비율")]
        [SerializeField] private float _iconScale = 0.8f;
        [Tooltip("지붕 높이(칸)")]
        [SerializeField] private float _roofHeight = 2f;
        [Tooltip("진영색 깃발(성의 자식) — 지붕 꼭대기로 옮긴다. 지붕 색도 이 깃발 색")]
        [SerializeField] private SpriteRenderer _flag;
        [Tooltip("깃발 그림에서 깃대 아래 끝 자리(깃발 로컬, 칸)")]
        [SerializeField] private Vector2 _flagPole = new Vector2(-0.9375f, -2f);
        [Tooltip("층이 받는 효과 표시 아이콘(UI 탑과 같은 그림) — 가속 · 쿨다운")]
        [SerializeField] private Sprite _hasteIcon;
        [SerializeField] private Sprite _cooldownIcon;
        [Tooltip("데스매치 동안 오버드라이브 층의 번개 — 가속 표시 자리에, 숫자 없이")]
        [SerializeField] private Sprite _overdriveIcon;
        [Tooltip("층 가운데에서 효과 표시 가운데까지(칸) — 창과 벽 사이 빈 벽(15.5px)")]
        [SerializeField] private float _badgeOffset = 1.9375f;

        private enum Look { Plain, Brick, Iron }

        private class FloorView
        {
            public TowerFloor Floor;
            public SpriteRenderer Wall;
            public SpriteRenderer Light;
            public SpriteRenderer Icon;
            public SpriteRenderer Flash;
            // 이 층이 받고 있는 효과 — 가속(남은 초)은 앞쪽 벽, 쿨다운(곱해진 값)은 뒤쪽 벽
            public TowerBadge Haste;
            public TowerBadge Cooldown;
            // 데스매치 동안 오버드라이브 층의 번개(Fx/OverdriveMark) — 가속 표시와 같은 자리, 숫자 없이
            public TowerBadge Overdrive;
            public float Bounce = BounceDuration;
        }

        private static readonly List<Tower> All = new List<Tower>();

        private Base _base;
        private SpriteRenderer _castle;
        // [Look] = 그 재질의 층 판(벽·처마선·창틀)
        private Sprite[] _walls;
        // [k] = 창이 아래에서 k줄까지 켜진 불빛(아치 창 · 둥근 창). 마지막 칸 = 다 켜짐(번쩍임도 이것)
        private Sprite[] _archLight;
        private Sprite[] _roundLight;
        private Sprite[] _badgeFont;
        private SpriteRenderer _roof;
        private BoardRunner _subscribed;
        private readonly List<FloorView> _floors = new List<FloorView>();

        public static Tower For(Team team)
        {
            foreach (Tower t in All)
                if (t._base.Team == team)
                    return t;
            return null;
        }

        public Team Team => _base.Team;
        public int FloorCount => _floors.Count;
        public TowerFloor Floor(int index) => _floors[index].Floor;
        private float Forward => _base.Team == Team.Ally ? 1f : -1f;
        // 가운데 성벽 윗면 = 1층 바닥
        private float Bottom => _castle.bounds.min.y + _wallTop;
        public float FloorBottom(int index) => Bottom + index * _floorHeight;

        // 버프 선(Fx/BuffLinkFx)이 붙는 자리 — 그 층 앞쪽 벽 가운데 높이 · 맵이 내는 효과는 지붕 꼭대기에서
        public Vector2 FloorFront(int index) => new Vector2(transform.position.x + Forward * _floorWidth * 0.5f, FloorBottom(index) + _floorHeight * 0.5f);
        public Vector2 RoofTop => new Vector2(transform.position.x, _roof.transform.position.y + _roofHeight);

        // 층 그림 크기(픽셀) — 처마선이 양쪽 벽보다 1px씩 튀어나와 층 폭보다 2px 넓다
        private int SpriteWidth => Mathf.RoundToInt(_floorWidth * PixelsPerUnit) + 2;
        private int SpriteHeight => Mathf.RoundToInt(_floorHeight * PixelsPerUnit);
        private int WindowTop => SpriteHeight - 1 - WindowTopMargin;
        private int WindowRows => WindowTop - WindowBottom + 1;

        // 그 층에서 나온 유닛이 서는 자리(발) — 층 가운데부터 뒤쪽으로 한 마리씩 늘어선다(order = 한 번에 나온 순서).
        // 유닛은 층 바닥을 딛고 앞쪽 가장자리로 걸어 나가 떨어진다(사용자 10/8)
        public Vector2 SpawnFeet(int index, int order, float unitWidth)
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt(_floorWidth * 0.5f / unitWidth));
            return new Vector2(transform.position.x - Forward * (order % perRow) * unitWidth, FloorBottom(index));
        }

        private void Awake()
        {
            _base = GetComponent<Base>();
            _castle = GetComponent<SpriteRenderer>();
            _walls = new[] { WallSprite(Look.Plain), WallSprite(Look.Brick), WallSprite(Look.Iron) };
            _archLight = LightSprites(InArch);
            _roundLight = LightSprites(InRound);
            _badgeFont = TowerBadge.BuildFont();
            _roof = MakeRoof();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            _subscribed = BoardRunner.For(_base.Team);
            if (_subscribed != null)
                _subscribed.Triggered += OnTriggered;
            OwnedBuilding.EffectFired += OnEffectFired;
        }

        private void OnDestroy()
        {
            if (_subscribed != null)
                _subscribed.Triggered -= OnTriggered;
            OwnedBuilding.EffectFired -= OnEffectFired;
        }

        private void OnTriggered(int index)
        {
            if (index < _floors.Count)
                _floors[index].Bounce = 0f;
        }

        // 쿨다운 밖의 효과(「구매할 때마다,」·전투 시작 시 등)를 내거나 받은 기물의 층도 튄다
        private void OnEffectFired(OwnedBuilding fired)
        {
            OwnedBuilding[] field = _subscribed != null ? _subscribed.Field : null;
            if (field == null)
                return;
            for (int i = 0; i < field.Length && i < _floors.Count; i++)
                if (field[i] == fired)
                    _floors[i].Bounce = 0f;
        }

        private void LateUpdate()
        {
            BoardRunner board = BoardRunner.For(_base.Team);
            OwnedBuilding[] field = board != null ? board.Field : null;
            int count = field != null ? field.Length : 0;
            while (_floors.Count < count)
                AddFloor(_floors.Count);
            // 한 프레임이 길어도(전투 시작 프레임 등) 튐이 건너뛰어지지 않게 프레임당 진행을 묶는다
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            for (int i = 0; i < _floors.Count; i++)
            {
                bool shown = i < count;
                FloorView view = _floors[i];
                if (view.Floor.gameObject.activeSelf != shown)
                    view.Floor.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                OwnedBuilding building = OwnedBuilding.IsEmpty(field[i]) ? null : field[i];
                BoardRunner.Slot running = BattleManager.Fighting && i < board.Slots.Length ? board.Slots[i] : null;
                ShowFloor(view, building, running, dt);
            }
            PlaceRoof(count);
        }

        private void ShowFloor(FloorView view, OwnedBuilding building, BoardRunner.Slot running, float dt)
        {
            Look look = LookOf(building);
            Sprite wall = _walls[(int)look];
            if (view.Wall.sprite != wall)
                view.Wall.sprite = wall;
            Sprite[] lights = look == Look.Iron ? _roundLight : _archLight;
            ShowIcon(view.Icon, building != null ? building.Definition.Icon : null);

            // 창 불빛 — 빈 층은 꺼짐, 전투 중 쿨다운 기물은 찬 만큼 아래부터, 나머지는 은은히 다 켜짐
            int rows = 0;
            float alpha = DimLight;
            if (building != null && running != null && running.Cooldown > 0f)
            {
                rows = Mathf.FloorToInt(WindowRows * Mathf.Clamp01(running.Timer / running.Cooldown));
                alpha = 1f;
            }
            else if (building != null)
                rows = WindowRows;
            Sprite light = lights[rows];
            if (view.Light.sprite != light)
                view.Light.sprite = light;
            view.Light.color = new Color(LightColor.r, LightColor.g, LightColor.b, alpha);
            if (view.Flash.sprite != lights[WindowRows])
                view.Flash.sprite = lights[WindowRows];

            view.Bounce += dt;
            float t = Mathf.Clamp01(view.Bounce / BounceDuration);
            view.Icon.transform.localScale = Vector3.one * (IconSize(view.Icon.sprite) * Mathf.Lerp(BounceScale, 1f, t));
            view.Flash.color = new Color(1f, 1f, 1f, FlashAlpha * (1f - t));

            view.Haste.ShowSeconds(running != null ? running.HasteLeft : 0f);
            view.Cooldown.ShowScale(running != null ? running.CooldownScale : 1f);
            view.Overdrive.ShowIcon(OverdriveMark.On(building, _base.Team));
        }

        private static Look LookOf(OwnedBuilding building)
        {
            if (building == null)
                return Look.Plain;
            switch (building.Definition.Nation)
            {
                case BuildingNation.Kingdom: return Look.Brick;
                case BuildingNation.TickTock: return Look.Iron;
                default: return Look.Plain;
            }
        }

        private void AddFloor(int index)
        {
            var go = new GameObject($"Floor{index + 1}");
            go.transform.SetParent(transform, false);
            // 성의 스케일·뒤집기와 상관없이 월드 크기로 놓는다
            go.transform.position = new Vector3(transform.position.x, FloorBottom(index) + _floorHeight * 0.5f, transform.position.z - 0.05f);   // 성·깃발과 같은 정렬 순서 — 카메라 쪽으로 살짝 당겨 그 앞에 그린다
            go.transform.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f);

            var wall = go.AddComponent<SpriteRenderer>();
            wall.sprite = _walls[(int)Look.Plain];
            wall.sortingLayerID = _castle.sortingLayerID;
            wall.sortingOrder = _castle.sortingOrder;   // 유닛(0)보다 뒤 — 층 바닥을 걷는 유닛이 검은 속에 가려지지 않게

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(_floorWidth, _floorHeight);

            // 층 바닥 — 이 진영 유닛만 위에서 딛는다(한쪽 방향 판: 아래에서는 지나간다 — 큰 유닛 머리가 위층 바닥에 걸리지 않게).
            // 적 유닛·화살은 부딪히지 않는다(이펙터 마스크 · Projectile이 이펙터 콜라이더를 건너뛴다)
            var deck = new GameObject("Deck");
            deck.transform.SetParent(go.transform, false);
            deck.transform.position = new Vector3(transform.position.x, FloorBottom(index) - DeckThickness * 0.5f, transform.position.z);
            var deckBox = deck.AddComponent<BoxCollider2D>();
            deckBox.size = new Vector2(_floorWidth, DeckThickness);
            deckBox.usedByEffector = true;
            var effector = deck.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useColliderMask = true;
            effector.colliderMask = (1 << LayerMask.NameToLayer(_base.Team == Team.Ally ? "AllyUnit" : "EnemyUnit")) | (1 << Base.BuilderLayer(_base.Team));

            var floor = go.AddComponent<TowerFloor>();
            floor.Init(_base.Team, index);

            var view = new FloorView { Floor = floor, Wall = wall };

            // 창 불빛 · 번쩍임 — 층 판과 같은 크기 그림이라 같은 자리에 겹친다
            view.Light = Child(go.transform, "Light", Vector2.zero, -0.01f).gameObject.AddComponent<SpriteRenderer>();
            Sort(view.Light);
            view.Flash = Child(go.transform, "Flash", Vector2.zero, -0.02f).gameObject.AddComponent<SpriteRenderer>();
            Sort(view.Flash);
            view.Flash.color = Color.clear;

            // 아이콘 — 창 한가운데
            float windowCenter = ((WindowBottom + WindowTop + 1) * 0.5f - SpriteHeight * 0.5f) * Pixel;
            view.Icon = Child(go.transform, "Icon", new Vector2(0f, windowCenter), -0.05f).gameObject.AddComponent<SpriteRenderer>();
            Sort(view.Icon);

            // 받는 효과 표시 — 색은 버프 선의 가속 색
            Color badgeColor = BuffLinkFx.ColorOf(BuffKind.Haste);
            view.Haste = new TowerBadge(go.transform, "Haste", Forward * _badgeOffset, _hasteIcon, _badgeFont, badgeColor, wall);
            view.Cooldown = new TowerBadge(go.transform, "Cooldown", -Forward * _badgeOffset, _cooldownIcon, _badgeFont, badgeColor, wall);

            view.Overdrive = new TowerBadge(go.transform, "Overdrive", Forward * _badgeOffset, _overdriveIcon, _badgeFont, OverdriveMark.Color, wall);

            _floors.Add(view);
        }

        private static Transform Child(Transform parent, string name, Vector2 localPosition, float z)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = new Vector3(localPosition.x, localPosition.y, z);
            return t;
        }

        private void Sort(Renderer r)
        {
            r.sortingLayerID = _castle.sortingLayerID;
            r.sortingOrder = _castle.sortingOrder;
        }

        // 아이콘은 원본 비율 그대로, 창 안쪽 높이에 맞춰 줄인다(크기는 ShowFloor가 튐과 함께 IconSize로). 적 탑은 거울처럼 뒤집지 않는다(글자가 아니라 그림이므로 그대로 둔다)
        private static void ShowIcon(SpriteRenderer icon, Sprite sprite)
        {
            if (icon.sprite != sprite)
                icon.sprite = sprite;
        }

        private float IconSize(Sprite sprite)
            => sprite == null ? 1f : WindowRows * Pixel * _iconScale / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);

        // 지붕은 맨 위 층 위(층이 없으면 성벽 위), 깃발은 지붕 꼭대기에 깃대를 세운다
        private void PlaceRoof(int floors)
        {
            float roofBottom = FloorBottom(floors);
            _roof.transform.position = new Vector3(transform.position.x, roofBottom, transform.position.z - 0.05f);
            if (_flag == null)
                return;
            Vector3 scale = _flag.transform.lossyScale;
            Vector2 apex = new Vector2(transform.position.x, roofBottom + _roofHeight - Pixel);
            _flag.transform.position = new Vector3(apex.x - _flagPole.x * scale.x, apex.y - _flagPole.y * scale.y, _flag.transform.position.z);
        }

        private SpriteRenderer MakeRoof()
        {
            var roof = new GameObject("Roof").AddComponent<SpriteRenderer>();
            roof.transform.SetParent(transform, false);
            roof.transform.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f);
            roof.sprite = RoofSprite();
            roof.color = _flag != null ? _flag.color : Color.white;
            Sort(roof);
            return roof;
        }

        // 층보다 양옆 2px 넓은 계단식 삼각 지붕(흰색 — 진영색으로 칠한다). 피벗 = 아래 가운데
        private Sprite RoofSprite()
        {
            int w = Mathf.RoundToInt(_floorWidth * PixelsPerUnit) + 4, h = Mathf.RoundToInt(_roofHeight * PixelsPerUnit);
            var texture = NewTexture(w, h);
            for (int y = 0; y < h; y++)
            {
                int inset = Mathf.FloorToInt((w * 0.5f - 1f) * y / h);
                for (int x = 0; x < w; x++)
                    texture.SetPixel(x, y, x >= inset && x < w - inset ? Color.white : Color.clear);
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), PixelsPerUnit);
        }

        // ── 층 그림(코드로 그린다 — 흰 선 · 검은 속, 무늬는 회색) ──

        // 창 안쪽인가 — 아치 창: 아래는 네모, 위는 반원
        private bool InArch(int x, int y)
        {
            if (y < WindowBottom || y > WindowTop)
                return false;
            float dx = x + 0.5f - SpriteWidth * 0.5f;
            if (Mathf.Abs(dx) > ArchRadius)
                return false;
            float archCenter = WindowTop + 1 - ArchRadius;
            float dy = y + 0.5f - archCenter;
            return dy <= 0f || dx * dx + dy * dy <= ArchRadius * ArchRadius;
        }

        private float RoundRadius => WindowRows * 0.5f;
        private float WindowCenterY => (WindowBottom + WindowTop + 1) * 0.5f;

        // 둥근 창
        private bool InRound(int x, int y)
        {
            float dx = x + 0.5f - SpriteWidth * 0.5f;
            float dy = y + 0.5f - WindowCenterY;
            return dx * dx + dy * dy <= RoundRadius * RoundRadius;
        }

        // 둥근 창 둘레 톱니 — 창틀 바깥에 2×2 이빨 8개(45°마다)
        private bool GearTooth(int x, int y)
        {
            float px = x + 0.5f - SpriteWidth * 0.5f, py = y + 0.5f - WindowCenterY;
            for (int k = 0; k < 8; k++)
            {
                float angle = k * Mathf.PI / 4f, d = RoundRadius + 2f;
                if (Mathf.Abs(px - Mathf.Cos(angle) * d) < 1f && Mathf.Abs(py - Mathf.Sin(angle) * d) < 1f)
                    return true;
            }
            return false;
        }

        // 가까이(n px 안)에 창 안쪽이 있나
        private static bool Near(System.Func<int, int, bool> inside, int x, int y, int n)
        {
            for (int oy = -n; oy <= n; oy++)
                for (int ox = -n; ox <= n; ox++)
                    if (inside(x + ox, y + oy))
                        return true;
            return false;
        }

        // 층 판 — 아래 2줄 처마선(아랫줄은 벽 폭, 윗줄은 양쪽으로 1px 튀어나옴) · 양쪽 벽 1px · 가운데 창틀 · 재질 무늬
        private Sprite WallSprite(Look look)
        {
            int w = SpriteWidth, h = SpriteHeight;
            System.Func<int, int, bool> inside = look == Look.Iron ? (System.Func<int, int, bool>)InRound : InArch;
            var texture = NewTexture(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (y == 1)
                        c = Color.white;
                    else if (y == 0 || x == 1 || x == w - 2)
                        c = x >= 1 && x <= w - 2 ? Color.white : Color.clear;
                    else if (x == 0 || x == w - 1)
                        c = Color.clear;
                    else if (inside(x, y))
                        c = Color.black;
                    else if (Near(inside, x, y, 1) || look == Look.Iron && GearTooth(x, y))
                        c = Color.white;
                    else if (Near(inside, x, y, look == Look.Iron ? 4 : 2))
                        c = Color.black;   // 창 둘레는 무늬를 비워 창이 또렷하게
                    else
                        c = Pattern(look, x, y, w);
                    texture.SetPixel(x, y, c);
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        // 재질 무늬 — 벽돌: 4줄마다 가로 이음, 단마다 엇갈린 세로 이음 · 철판: 양쪽 세로 이음 + 리벳 점(흰색)
        private static Color Pattern(Look look, int x, int y, int w)
        {
            switch (look)
            {
                case Look.Brick:
                {
                    int row = y - 2;
                    if (row % 4 == 3)
                        return PatternColor;
                    return (x + (row / 4 % 2) * 4) % 8 == 0 ? PatternColor : Color.black;
                }
                case Look.Iron:
                {
                    bool rivetRow = (y - 4) % 5 == 0;
                    if (rivetRow && (x == 3 || x == w - 4 || x == 9 || x == w - 10))
                        return Color.white;
                    return x == 11 || x == w - 12 ? PatternColor : Color.black;
                }
                default:
                    return Color.black;
            }
        }

        // 창 불빛 단계들 — 한 텍스처에 (줄 수 + 1)칸을 옆으로 이어 그리고 칸마다 스프라이트(층 판과 같은 크기 · 가운데 피벗)
        private Sprite[] LightSprites(System.Func<int, int, bool> inside)
        {
            int w = SpriteWidth, h = SpriteHeight, steps = WindowRows + 1;
            var texture = NewTexture(w * steps, h);
            var sprites = new Sprite[steps];
            for (int k = 0; k < steps; k++)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        texture.SetPixel(k * w + x, y, inside(x, y) && y < WindowBottom + k ? Color.white : Color.clear);
                sprites[k] = Sprite.Create(texture, new Rect(k * w, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }
            texture.Apply();
            return sprites;
        }

        private static Texture2D NewTexture(int w, int h)
            => new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도 위를 돌아다니는 영주들(사용자 10/11) — 보이는 것만이다(규칙 · 판도는 건드리지 않고, 난수도 연출용을 따로 쓴다).
    // 살아 있는 영주마다(나 포함) 자기 색 유닛 그림 하나가 자기 땅 안을 걷다 서다 한다 — 지도 한 칸씩 자기 땅만 밟고, 표시 자리(아이콘 판 · 번호) 위에는 서지 않는다.
    // AI 턴 재생 때는 침략하는 영주가 그 땅으로 걸어 들어간다(March) — 이겼으면 거기 남고, 졌으면 제자리로 돌아온다.
    // 지도(UI/WorldMapView)의 자식이라 확대 · 끌기를 같이 따라간다. 클릭은 받지 않는다
    public class WorldLords : MonoBehaviour
    {
        // 침략하러 걸어 들어가는 시간 · 져서 돌아오는 시간(초) — 지도의 AI 턴 재생(WorldManager)이 이 길이로 일정을 짠다
        public const float MarchSeconds = 0.45f;
        public const float ReturnSeconds = 0.3f;
        // 걸음이 끝난 뒤 「자기 땅 밖에 서 있나」 검사를 쉬는 시간(초)
        private const float ArriveGrace = 0.25f;

        // 어슬렁 — 걷는 속도(지도 칸/초) · 쉬는 시간(초) · 한 번에 가는 거리(칸)
        private const float WanderSpeed = 2.5f;
        private const float RestMin = 0.8f, RestMax = 2.6f;
        private const int WanderReach = 14;
        // 유닛 · 깃발 그림을 화면에 몇 배로(사용자 10/11: 24px)
        private const int PixelScale = 3;
        // 머리 위 닉네임 — 글자 크기(도트 폰트는 12의 정수배) · 몸 한가운데에서 글 한가운데까지(px, 깃발 끝 위)
        private const int NameSize = 12;
        private const float NameY = 30f;
        // 깃발 그림의 왼쪽 아래 자리(몸 그림의 왼쪽 아래에서, 원본 px) — 깃발 그림을 좌우로 뒤집어 든다(사용자 10/11): 깃대가 몸 바로 뒤에 붙고 천이 몸 바깥(뒤쪽)으로 펄럭인다
        private static readonly Vector2 FlagAt = new Vector2(-7f, 3f);
        // 표시 자리(기준점)에서 좌우 · 위 · 아래로 비우는 칸 — 아이콘 판 둘과 번호에, 깃발을 든 그림과 머리 위 닉네임의 크기(깃발 쪽 8칸 · 위로 10칸 · 아래로 3칸)를 더한 것
        private const int MarkHalfWidth = 17, MarkAbove = 11, MarkBelow = 19;

        private class Walker
        {
            // 걷는 땅의 주인 — 보통 그림의 영주 자신, 연합에 든 영주의 그림이면 연합(바탕 영주)
            public int Lord;
            public RectTransform Rect;
            // 머리 위 닉네임(없으면 null) — 그림이 뒤집힐 때 글자는 뒤집히지 않게 같이 뒤집어 되돌린다
            public RectTransform Label;
            // 지도 칸 좌표(위 왼쪽이 0, 0)
            public Vector2 Cell, Target, From;
            public bool HasTarget;
            public float Rest;
            // 침략 걸음 — Marching이면 From → Target, Returning이면 돌아오는 중. Clock = 그 걸음이 시작된 뒤 흐른 시간
            public bool Marching, Returning, Won;
            public float Clock;
            // 걸음이 끝난 뒤 제자리 검사를 쉬는 시간(초)
            public float Grace;
            // 곧 맞아 날아갈 영주 — 침략자가 달려드는 동안 제자리에 선다
            public bool Doomed;
            // 걸음이 끝나는 순간 할 일(내가 쓰러뜨린 영주를 치는 걸음 — 없으면 null)
            public System.Action Arrived;
        }

        private int[] _cells;
        private int _width, _height;
        private float _scale;
        private Vector2Int[] _anchors, _spots;
        private Sprite _body, _flag;
        private Font _font;
        private int[] _owner;
        private int _coalition = -1;
        private int[] _members;
        private readonly Dictionary<int, Walker> _walkers = new Dictionary<int, Walker>();
        private readonly List<int> _gone = new List<int>();
        // 연출용 난수 — 규칙의 난수(UnityEngine.Random · 런의 System.Random)와 섞지 않는다
        private readonly System.Random _random = new System.Random(20261011);

        public int Count => _walkers.Count;

        // cells = [y × width + x] 땅 번호(바다는 음수), scale = 지도 한 칸의 크기(px), anchors = 땅마다 표시 자리, body = 유닛 몸 그림(영주 색으로 칠한다), flag = 드는 깃발(흰 그림 — 영주 색을 입힌다), font = 닉네임 글(도트 폰트)
        public void Init(int[] cells, int width, int height, float scale, Vector2Int[] anchors, Sprite body, Sprite flag, Font font)
        {
            _cells = cells;
            _width = width;
            _height = height;
            _scale = scale;
            _anchors = anchors;
            _body = body;
            _flag = flag;
            _font = font;
            _spots = new Vector2Int[anchors.Length];
            for (int region = 0; region < anchors.Length; region++)
                _spots[region] = SpotOf(region);
        }

        // 땅 안에서 서 있기 좋은 칸 — 표시 자리 둘레에서 먼저 찾고, 없으면 그 땅의 아무 칸
        private Vector2Int SpotOf(int region)
        {
            Vector2Int anchor = _anchors[region];
            Vector2Int[] around = { new Vector2Int(0, 22), new Vector2Int(-21, 2), new Vector2Int(21, 2), new Vector2Int(0, -14), new Vector2Int(-19, 21), new Vector2Int(19, 21), new Vector2Int(-19, -13), new Vector2Int(19, -13) };
            foreach (Vector2Int offset in around)
                if (Roomy(region, anchor + offset))
                    return anchor + offset;
            for (int i = 0; i < _cells.Length; i++)
                if (_cells[i] == region && Roomy(region, new Vector2Int(i % _width, i / _width)))
                    return new Vector2Int(i % _width, i / _width);
            return anchor;
        }

        // 국경에서 두 칸은 떨어지고 표시 자리 밖인 칸인가
        private bool Roomy(int region, Vector2Int cell)
        {
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                    if (RegionAt(cell.x + x, cell.y + y) != region)
                        return false;
            return !OnMark(cell.x, cell.y);
        }

        private int RegionAt(int x, int y) => x < 0 || y < 0 || x >= _width || y >= _height ? -1 : _cells[y * _width + x];

        private bool OnMark(int x, int y)
        {
            int region = RegionAt(x, y);
            if (region < 0)
                return false;
            Vector2Int anchor = _anchors[region];
            return Mathf.Abs(x - anchor.x) <= MarkHalfWidth && y >= anchor.y - MarkAbove && y <= anchor.y + MarkBelow;
        }

        // 그 칸이 lord의 땅인가
        private bool Owns(int lord, Vector2 cell)
        {
            int region = RegionAt(Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y));
            return region >= 0 && _owner[region] == lord;
        }

        // 최후의 연합(사용자 10/11) — coalition = 연합(바탕 영주)의 번호(없으면 음수), members = 연합에 든 나머지 영주들.
        // 연합의 땅에는 바탕 영주와 든 영주들의 그림이 각자 제 색 깃발을 들고 다 같이 돌아다닌다
        public void Group(int coalition, int[] members)
        {
            _coalition = coalition;
            _members = members;
        }

        // 그 그림이 걷는 땅의 주인 — 제 땅이 있으면 자신, 없는데 연합에 든 영주면 연합, 둘 다 아니면 음수(그림을 치운다)
        private int HomeOf(int lord)
        {
            if (System.Array.IndexOf(_owner, lord) >= 0)
                return lord;
            bool member = _members != null && System.Array.IndexOf(_members, lord) >= 0;
            return member && System.Array.IndexOf(_owner, _coalition) >= 0 ? _coalition : -1;
        }

        // lord의 땅 가운데 아무 곳(없으면 음수) — 여럿이 한꺼번에 나타나도 한자리에 겹치지 않게 고른다
        private int RandomRegion(int lord)
        {
            int picked = -1, seen = 0;
            for (int region = 0; region < _owner.Length; region++)
                if (_owner[region] == lord && _random.Next(++seen) == 0)
                    picked = region;
            return picked;
        }

        // region 땅의 설 자리 둘레에서 lord의 땅인 칸 하나 — 여럿이 같은 땅에 나타날 때 한 점에 포개지지 않게 흩는다(못 찾으면 설 자리 그대로)
        private Vector2 Scatter(int lord, int region)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                Vector2 cell = _spots[region] + new Vector2(_random.Next(-WanderReach, WanderReach + 1), _random.Next(-WanderReach, WanderReach + 1));
                if (Owns(lord, cell) && !OnMark((int)cell.x, (int)cell.y))
                    return cell;
            }
            return _spots[region];
        }

        // 지금 보이는 판도(owner = [땅] 주인)에 맞춘다 — 땅이 있는 영주마다 그림 하나(연합이면 든 영주마다 하나씩 더), 걸을 땅이 없어진 그림은 치운다.
        // 연합이 뭉치는 순간에는 든 영주들의 그림이 있던 자리에서 그대로 연합의 땅을 이어 걷는다
        public void Sync(int[] owner, System.Func<int, Color> colorOf, int mine)
        {
            _owner = owner;
            _gone.Clear();
            foreach (KeyValuePair<int, Walker> pair in _walkers)
            {
                int home = HomeOf(pair.Key);
                if (home < 0)
                    _gone.Add(pair.Key);
                else
                    pair.Value.Lord = home;
            }
            foreach (int lord in _gone)
            {
                // 땅을 다 잃은 영주는 전투의 시체처럼 날아간다 — 내 영주(게임 오버)는 그냥 사라진다
                Walker fallen = _walkers[lord];
                if (lord == mine)
                    Destroy(fallen.Rect.gameObject);
                else
                    // 침략자가 달려들어 친 영주(Doomed)는 친 쪽의 반대로, 그 밖(반란 · 재생 건너뛰기)은 아무 쪽으로
                    Throw(fallen.Rect, fallen.Doomed && _threats.TryGetValue(lord, out Vector2 from) ? fallen.Cell.x - from.x : 0f, fallen.Doomed);
                _walkers.Remove(lord);
            }
            for (int region = 0; region < owner.Length; region++)
                if (!_walkers.ContainsKey(owner[region]))
                    _walkers[owner[region]] = NewWalker(owner[region], owner[region], region, colorOf(owner[region]));
            if (_members == null || System.Array.IndexOf(owner, _coalition) < 0)
                return;
            foreach (int member in _members)
                if (!_walkers.ContainsKey(member))
                    _walkers[member] = NewWalker(member, _coalition, RandomRegion(_coalition), colorOf(member));
        }

        // lord = 그림의 영주(깃발 색), home = 걷는 땅의 주인, region = 처음 서는 땅
        private Walker NewWalker(int lord, int home, int region, Color color)
        {
            // 자리 = 몸의 한가운데. 좌우 뒤집기는 이 뿌리의 localScale로 — 깃발도 같이 뒤집힌다
            var rect = (RectTransform)new GameObject("Lord" + lord, typeof(RectTransform)).transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.sizeDelta = Vector2.zero;
            Figure(rect, _body, _flag, color, PixelScale);
            var walker = new Walker { Lord = home, Rect = rect, Label = NameLabel(rect, NameOf?.Invoke(lord), lord == _pointed), Cell = Scatter(home, region), Rest = RestMin + (float)_random.NextDouble() * (RestMax - RestMin) };
            Place(walker, 1f);
            return walker;
        }

        // 이름을 띄울 영주(없으면 음수) — 마우스가 가리킨 땅의 주인 한 명만(사용자 10/12: 늘 다 띄우면 겹친다)
        private int _pointed = -1;

        public void Point(int lord)
        {
            if (lord == _pointed)
                return;
            foreach (int id in new[] { _pointed, lord })
                if (_walkers.TryGetValue(id, out Walker walker) && walker.Label != null)
                    walker.Label.gameObject.SetActive(id == lord);
            _pointed = lord;
        }

        // 영주 번호 → 머리 위에 띄울 닉네임(null이면 안 띄운다 — 나). 지도를 만든 쪽(WorldManager)이 넣는다
        public System.Func<int, string> NameOf;

        // 머리 위 닉네임 — 도트 폰트 12, 검은 테두리(어느 채움 색 위에서도 읽히게). name이 null이면 만들지 않는다. 가리킨 영주의 것만 보인다(Point)
        private RectTransform NameLabel(RectTransform parent, string name, bool shown)
        {
            if (name == null)
                return null;
            var label = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = _font;
            label.fontSize = NameSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = name;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.rectTransform.anchoredPosition = new Vector2(0f, NameY);
            var outline = label.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, 1f);
            label.gameObject.SetActive(shown);
            return label.rectTransform;
        }

        // 깃발 든 영주 그림 한 벌을 parent 아래에 그린다 — parent의 자리가 몸의 한가운데, scale = 원본 1px이 화면 몇 px인지(정수).
        // 깃발은 영주 색으로 통째로 칠하고, 몸은 파란 자리만 영주 색(Core/LordPaint). 지도의 그림과 왼쪽 패널의 초상화(UI/WorldSidePanel)가 같이 쓴다
        public static void Figure(RectTransform parent, Sprite body, Sprite flag, Color color, int scale)
        {
            // 몸 그림의 왼쪽 아래를 (0, 0)으로 깃발의 왼쪽 아래가 FlagAt
            Vector2 flagAt = (FlagAt + flag.rect.size / 2f - body.rect.size / 2f) * scale;
            Sprite painted = LordPaint.Of(body, color);
            // 어느 채움 색 위에서도(내 땅의 밝은 파랑 포함) 묻히지 않게 검은 테두리 — 깃발과 몸을 한 덩어리로 두르도록 검은 그림자 둘을 먼저 깔고 그 위에 그린다
            Part(parent, flag, flagAt, Color.black, true, true, scale);
            Part(parent, painted, Vector2.zero, Color.black, true, false, scale);
            Part(parent, flag, flagAt, color, false, true, scale);
            Part(parent, painted, Vector2.zero, Color.white, false, false, scale);
        }

        // 그림 한 겹 — 원본 크기의 정수배(scale). outline이면 검은 테두리를 두르고, mirror면 좌우로 뒤집어 그린다
        private static void Part(RectTransform parent, Sprite sprite, Vector2 position, Color color, bool outline, bool mirror, int scale)
        {
            var image = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.raycastTarget = false;
            image.sprite = sprite;
            image.color = color;
            // 좌우 뒤집기가 localScale을 쓴다 — Simple + Preserve Aspect(CLAUDE.md §5-1)
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = sprite.rect.size * scale;
            image.rectTransform.anchoredPosition = position;
            if (mirror)
                image.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            if (!outline)
                return;
            // 테두리는 가로 · 세로 두 번 — Outline 하나는 대각선 네 방향만 덮어서 깃발 끝 같은 뾰족한 픽셀의 옆이 빈다. 둘을 겹치면 둘레 여덟 방향이 다 덮인다
            foreach (Vector2 distance in new[] { new Vector2(scale, 0f), new Vector2(0f, scale) })
            {
                var edge = image.gameObject.AddComponent<Outline>();
                edge.effectColor = Color.black;
                edge.effectDistance = distance;
            }
        }

        // ── 죽은 영주(땅을 다 잃음)는 전투의 시체(Fx/CorpseFx)처럼 날아간다(사용자 10/12) — 친 쪽의 반대로 비스듬히 위로, 빙글 돌며 포물선으로.
        // 이긴 영주가 진 영주 바로 옆까지 달려들어 치고(침략 걸음의 도착이 곧 타격), 맞는 순간 날아간다. 날아가는 길에는 구름 조각이 남았다가 작아지며 사라진다.
        // 값은 전투의 것(BattleConfig의 시체 값)을 그대로 쓴다: 전투의 1칸 = 영주 몸 그림의 높이. 날아가는 그림은 걷는 그림들과 따로 살다 사라진다(재생을 기다리게 하지 않는다)
        private class Flyer
        {
            public RectTransform Rect;
            public Vector2 Origin, Velocity;
            public float Spin, Age;
            // 궤적을 따라가는 빠르기(1 = 전투 값 그대로) — 지도 가장자리에서 맞으면 느려진다(FellMinSeconds)
            public float Slow;
            // 다음 구름 조각까지 남은 시간
            public float Puff;
        }

        // 구름 조각 한 개 — 제자리에서 작아지며 흐려진다
        private class Cloud
        {
            public Image Image;
            public float Age, Size;
        }

        // 죽이는 침략에서 진 영주의 어느 쪽 옆에 서서 치나(칸) — 두 그림이 맞닿는 거리
        private const float StrikeReach = 5f;
        // 날아가는 그림이 지도 밖(옆 · 위)으로 나가기까지 적어도 이만큼(초) 보인다 — 가장자리에서 맞은 그림은 같은 궤적을 그만큼 느리게 난다. 가운데에서 맞으면 전투 값 그대로
        private const float FellMinSeconds = 0.5f;
        // 구름 — 남기는 간격(초) · 수명(초) · 조각 크기(구름 그림 1px이 화면 몇 px인지 — 작은 것 · 큰 것) · 맞는 순간 터지는 조각 수 · 동시에 떠 있는 상한
        private const float CloudInterval = 0.025f;
        private const float CloudSeconds = 0.6f;
        private const int CloudSmall = 3, CloudLarge = 6;
        private const int CloudBurst = 7;
        private const int CloudMax = 120;

        private readonly List<Flyer> _flyers = new List<Flyer>();
        private readonly List<Cloud> _clouds = new List<Cloud>();
        private Sprite _cloud;
        // [영주] = 그 영주의 땅으로 마지막에 쳐들어온 침략자가 출발한 칸
        private readonly Dictionary<int, Vector2> _threats = new Dictionary<int, Vector2>();
        // 지도를 열기 전에 이미 죽은 영주(내가 전투에서 쓰러뜨렸다) — 첫 그림을 그린 뒤에 세워 두고, 친 영주가 달려들어 치면 날아간다
        private readonly List<System.Action<List<RectTransform>>> _fallen = new List<System.Action<List<RectTransform>>>();
        private int _striker = -1;

        // 전투의 1칸이 지도 그림에서 몇 px인가
        private float UnitPixels => _body.rect.height * PixelScale;

        // rect = 날릴 그림(걷던 그림 그대로), away = 날아갈 가로 방향(부호만 본다 — 0이면 아무 쪽). struck = 맞아서 날아가나(맞은 자리에 구름이 터진다)
        private void Throw(RectTransform rect, float away, bool struck)
        {
            Transform label = rect.Find("Name");
            if (label != null)
                Destroy(label.gameObject);
            BattleConfig config = RunState.Config;
            float side = away > 0f ? 1f : away < 0f ? -1f : _random.Next(2) == 0 ? -1f : 1f;
            float angle = (config.CorpseAngle + Range(-config.CorpseAngleSpread, config.CorpseAngleSpread)) * Mathf.Deg2Rad;
            float speed = Range(config.CorpseSpeedMin, config.CorpseSpeedMax) * UnitPixels;
            Vector2 velocity = new Vector2(side * Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            // 날아가는 쪽 옆 끝 · 위 끝까지의 거리(px) — 전투 값으로 FellMinSeconds 안에 넘어가면 넘어가지 않을 만큼 느리게
            Vector2 at = rect.anchoredPosition;
            float aside = side > 0f ? _width * _scale - at.x : at.x, above = -at.y;
            _flyers.Add(new Flyer
            {
                Rect = rect,
                Origin = at,
                Velocity = velocity,
                Spin = -side * Range(config.CorpseSpinMin, config.CorpseSpinMax),
                Slow = Mathf.Min(1f, aside / (Mathf.Abs(velocity.x) * FellMinSeconds), above / (velocity.y * FellMinSeconds)),
            });
            if (!struck)
                return;
            for (int i = 0; i < CloudBurst; i++)
                Puff(rect.anchoredPosition + new Vector2(Range(-1f, 1f), Range(-1f, 1f)) * UnitPixels * 0.6f, CloudLarge);
            rect.SetAsLastSibling();
        }

        private float Range(float min, float max) => min + (max - min) * (float)_random.NextDouble();

        // 구름 조각 하나를 position(지도 그림 안, px)에 남긴다 — 상한을 넘으면 가장 오래된 것을 돌려쓴다
        private void Puff(Vector2 position, int size)
        {
            if (_cloud == null)
                _cloud = CloudSprite();
            Cloud cloud;
            if (_clouds.Count >= CloudMax)
            {
                cloud = _clouds[0];
                _clouds.RemoveAt(0);
            }
            else
            {
                cloud = new Cloud { Image = new GameObject("Cloud", typeof(RectTransform), typeof(Image)).GetComponent<Image>() };
                cloud.Image.raycastTarget = false;
                cloud.Image.sprite = _cloud;
                // 작아지는 연출이 localScale을 쓴다 — Simple + Preserve Aspect(CLAUDE.md §5-1)
                cloud.Image.preserveAspect = true;
                cloud.Image.rectTransform.SetParent(transform, false);
                cloud.Image.rectTransform.anchorMin = cloud.Image.rectTransform.anchorMax = new Vector2(0f, 1f);
            }
            cloud.Age = 0f;
            cloud.Size = size;
            cloud.Image.rectTransform.sizeDelta = _cloud.rect.size * size;
            cloud.Image.rectTransform.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
            cloud.Image.rectTransform.localScale = Vector3.one;
            cloud.Image.color = Color.white;
            cloud.Image.rectTransform.SetAsLastSibling();
            _clouds.Add(cloud);
        }

        // 구름 조각 그림 — 동그란 흰 도트(5×5). 연출용 임시 그림이라 코드로 만든다
        private static Sprite CloudSprite()
        {
            string[] rows = { ".###.", "#####", "#####", "#####", ".###." };
            var texture = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    texture.SetPixel(x, y, rows[y][x] == '#' ? Color.white : Color.clear);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 5f, 5f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }

        // 지도를 열기 전에 죽은 영주(color = 그 영주의 색)를 region 땅에 세워 둔다 — striker 영주(나)가 달려들어 치면 날아간다
        public void Fell(int region, int striker, Color color)
        {
            _striker = striker;
            _fallen.Add(standing =>
            {
                var rect = (RectTransform)new GameObject("Fallen", typeof(RectTransform)).transform;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.sizeDelta = Vector2.zero;
                Figure(rect, _body, _flag, color, PixelScale);
                Vector2 cell = _spots[region] + new Vector2(_random.Next(-6, 7), _random.Next(-6, 7));
                rect.anchoredPosition = new Vector2(Mathf.Round(cell.x * _scale), -Mathf.Round(cell.y * _scale));
                standing.Add(rect);
            });
        }

        // 세워 둔 그림들을 친 영주가 달려들어 친다 — 첫 그림 바로 옆까지 침략 걸음으로 들어가고, 도착하는 순간 전부 날아간다. 친 영주의 그림이 없으면 바로 날아간다
        private void Strike()
        {
            var standing = new List<RectTransform>();
            foreach (System.Action<List<RectTransform>> fall in _fallen)
                fall(standing);
            _fallen.Clear();
            if (!_walkers.TryGetValue(_striker, out Walker striker))
            {
                foreach (RectTransform rect in standing)
                    Throw(rect, 0f, false);
                return;
            }
            var prey = new Vector2(standing[0].anchoredPosition.x / _scale, -standing[0].anchoredPosition.y / _scale);
            StartMarch(striker, prey + new Vector2(striker.Cell.x <= prey.x ? -StrikeReach : StrikeReach, 0f), true);
            striker.Arrived = () =>
            {
                foreach (RectTransform rect in standing)
                    Throw(rect, rect.anchoredPosition.x - striker.Rect.anchoredPosition.x, true);
            };
        }

        private void StepFlyers(float dt)
        {
            if (_fallen.Count > 0)
                Strike();
            BattleConfig config = RunState.Config;
            float gravity = config.CorpseGravity * UnitPixels;
            // 지도 그림 아래 · 옆으로 빠지면 수명 전이라도 치운다(위로 나간 것은 다시 떨어져 들어온다)
            float floor = -_height * _scale - UnitPixels * 2f;
            for (int i = _flyers.Count - 1; i >= 0; i--)
            {
                Flyer flyer = _flyers[i];
                flyer.Age += dt;
                float t = flyer.Age * flyer.Slow;
                Vector2 position = flyer.Origin + flyer.Velocity * t + new Vector2(0f, -0.5f * gravity * t * t);
                if (flyer.Age >= config.CorpseLifetime || position.y < floor || position.x < -UnitPixels * 2f || position.x > _width * _scale + UnitPixels * 2f)
                {
                    Destroy(flyer.Rect.gameObject);
                    _flyers.RemoveAt(i);
                    continue;
                }
                flyer.Rect.anchoredPosition = position;
                flyer.Rect.localRotation = Quaternion.Euler(0f, 0f, flyer.Spin * flyer.Age);
                // 지나온 길에 구름 — 크고 작은 조각을 번갈아, 날아가는 그림은 늘 구름 위에
                flyer.Puff -= dt;
                if (flyer.Puff <= 0f)
                {
                    flyer.Puff = CloudInterval;
                    Puff(position + new Vector2(Range(-1f, 1f), Range(-1f, 1f)) * UnitPixels * 0.25f, _random.Next(2) == 0 ? CloudSmall : CloudLarge);
                    flyer.Rect.SetAsLastSibling();
                }
            }
            // 구름은 날아가는 그림이 사라져도 제 수명대로 — 작아지며 흐려진다
            for (int i = _clouds.Count - 1; i >= 0; i--)
            {
                Cloud cloud = _clouds[i];
                cloud.Age += dt;
                float left = 1f - cloud.Age / CloudSeconds;
                if (left <= 0f)
                {
                    Destroy(cloud.Image.gameObject);
                    _clouds.RemoveAt(i);
                    continue;
                }
                cloud.Image.rectTransform.localScale = Vector3.one * left;
                cloud.Image.color = new Color(1f, 1f, 1f, 0.4f + 0.6f * left);
            }
        }

        // 날아가는 그림의 수 · 떠 있는 구름 조각의 수(검사용)
        public int Flying => _flyers.Count;
        public int Clouds => _clouds.Count;

        // lord가 region 땅으로 침략하러 걸어 들어간다 — 이겼으면 거기 남고, 졌으면 돌아온다
        public void March(int lord, int region, bool won)
        {
            if (!_walkers.TryGetValue(lord, out Walker walker))
                return;
            int victim = _owner[region];
            Vector2 target = _spots[region];
            if (victim != lord)
            {
                // 그 땅의 주인이 이 침략으로 죽으면 침략자가 온 쪽의 반대로 날아간다
                _threats[victim] = walker.Cell;
                // 마지막 땅을 빼앗는 침략 — 진 영주 바로 옆까지 달려들어 친다(도착이 곧 타격). 진 영주는 그때까지 제자리에 선다
                if (won && LandsOf(victim) == 1 && _walkers.TryGetValue(victim, out Walker prey) && prey.Lord == victim && !prey.Marching)
                {
                    prey.Doomed = true;
                    target = prey.Cell + new Vector2(walker.Cell.x <= prey.Cell.x ? -StrikeReach : StrikeReach, 0f);
                }
            }
            StartMarch(walker, target, won);
        }

        private void StartMarch(Walker walker, Vector2 target, bool won)
        {
            walker.Marching = true;
            walker.Returning = false;
            walker.Won = won;
            walker.Clock = 0f;
            walker.From = walker.Cell;
            walker.Target = target;
            walker.HasTarget = false;
        }

        private int LandsOf(int lord)
        {
            int lands = 0;
            foreach (int owner in _owner)
                if (owner == lord)
                    lands++;
            return lands;
        }

        // 걸음을 전부 즉시 끝낸다(재생 건너뛰기) — 이긴 걸음은 그 땅에, 진 걸음은 제자리에
        public void Snap()
        {
            foreach (Walker walker in _walkers.Values)
            {
                if (!walker.Marching)
                    continue;
                walker.Cell = walker.Returning ? walker.Target : walker.Won ? walker.Target : walker.From;
                Arrive(walker);
                walker.Marching = false;
                Place(walker, 0f);
            }
        }

        // 걷는 중이 아닌 그림 가운데 자기 땅 밖에 서 있는 것의 수(검사용 — 늘 0이어야 한다)
        public int Strays
        {
            get
            {
                int strays = 0;
                foreach (Walker walker in _walkers.Values)
                    if (!walker.Marching && !Owns(walker.Lord, walker.Cell))
                        strays++;
                return strays;
            }
        }

        // lord의 그림이 침략 걸음 중이면 그 자리(지도 그림 안, px) — 확대한 지도가 내 영주를 따라간다(UI/WorldMapView.Show)
        public bool MarchingAt(int lord, out Vector2 at)
        {
            bool marching = _walkers.TryGetValue(lord, out Walker walker) && walker.Marching;
            at = marching ? walker.Rect.anchoredPosition : Vector2.zero;
            return marching;
        }

        public bool AnyMarching
        {
            get
            {
                foreach (Walker walker in _walkers.Values)
                    if (walker.Marching)
                        return true;
                return false;
            }
        }

        private void Update()
        {
            if (_owner == null)
                return;
            // 한 프레임이 길어도 걸음이 건너뛰지 않게 프레임당 진행을 묶는다
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            StepFlyers(dt);
            foreach (Walker walker in _walkers.Values)
            {
                if (walker.Marching)
                    StepMarch(walker, dt);
                else
                    StepWander(walker, dt);
            }
        }

        private void StepMarch(Walker walker, float dt)
        {
            walker.Clock += dt;
            float length = walker.Returning ? ReturnSeconds : MarchSeconds;
            float k = Mathf.Clamp01(walker.Clock / length);
            Vector2 before = walker.Cell;
            walker.Cell = Vector2.Lerp(walker.From, walker.Target, k);
            Place(walker, walker.Cell.x - before.x);
            if (k < 1f)
                return;
            if (!walker.Returning && !walker.Won)
            {
                // 졌다 — 온 길로 돌아간다
                walker.Returning = true;
                walker.Clock = 0f;
                (walker.From, walker.Target) = (walker.Target, walker.From);
                return;
            }
            walker.Marching = false;
            walker.Rest = RestMax;
            // 들어선 땅의 색은 걸음과 따로 바뀐다(내 침략은 전투 뒤에) — 그 사이에 「남의 땅에 서 있다」고 제자리로 옮기지 않게 잠깐 둔다
            walker.Grace = ArriveGrace;
            Arrive(walker);
        }

        private static void Arrive(Walker walker)
        {
            System.Action arrived = walker.Arrived;
            walker.Arrived = null;
            arrived?.Invoke();
        }

        private void StepWander(Walker walker, float dt)
        {
            if (walker.Doomed)
                return;
            if (walker.Grace > 0f)
            {
                walker.Grace -= dt;
                return;
            }
            // 서 있던 땅을 잃었으면(침략당함 · 재생 건너뛰기) 자기 땅으로 옮겨 선다
            if (!Owns(walker.Lord, walker.Cell))
            {
                int home = RandomRegion(walker.Lord);
                if (home < 0)
                    return;
                walker.Cell = Scatter(walker.Lord, home);
                walker.HasTarget = false;
                Place(walker, 0f);
                return;
            }
            if (walker.Rest > 0f)
            {
                walker.Rest -= dt;
                return;
            }
            if (!walker.HasTarget)
            {
                // 가까운 곳의 자기 땅 칸 하나 — 못 찾으면 조금 쉬고 다시
                var target = new Vector2(Mathf.Floor(walker.Cell.x) + _random.Next(-WanderReach, WanderReach + 1) + 0.5f, Mathf.Floor(walker.Cell.y) + _random.Next(-WanderReach, WanderReach + 1) + 0.5f);
                if (Owns(walker.Lord, target) && !OnMark((int)target.x, (int)target.y))
                {
                    walker.Target = target;
                    walker.HasTarget = true;
                }
                else
                    walker.Rest = 0.2f;
                return;
            }
            Vector2 next = Vector2.MoveTowards(walker.Cell, walker.Target, WanderSpeed * dt);
            // 남의 땅 · 바다 · 표시 자리로 들어가는 걸음이면 그 목표를 버린다
            if (!Owns(walker.Lord, next) || OnMark(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y)))
            {
                walker.HasTarget = false;
                walker.Rest = RestMin;
                return;
            }
            float dx = next.x - walker.Cell.x;
            walker.Cell = next;
            Place(walker, dx);
            if (next == walker.Target)
            {
                walker.HasTarget = false;
                walker.Rest = RestMin + (float)_random.NextDouble() * (RestMax - RestMin);
            }
        }

        // 지도 칸 → 지도 그림 안의 자리(px, 정수로). dx = 이번 걸음의 가로 방향(그림은 오른쪽을 보고 그려져 있다 — 왼쪽으로 걸으면 뒤집는다)
        private void Place(Walker walker, float dx)
        {
            walker.Rect.anchoredPosition = new Vector2(Mathf.Round(walker.Cell.x * _scale), -Mathf.Round(walker.Cell.y * _scale));
            if (dx != 0f)
            {
                walker.Rect.localScale = new Vector3(dx < 0f ? -1f : 1f, 1f, 1f);
                if (walker.Label != null)
                    walker.Label.localScale = walker.Rect.localScale;
            }
        }
    }
}

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

        // 어슬렁 — 걷는 속도(지도 칸/초) · 쉬는 시간(초) · 한 번에 가는 거리(칸)
        private const float WanderSpeed = 2.5f;
        private const float RestMin = 0.8f, RestMax = 2.6f;
        private const int WanderReach = 14;
        // 유닛 · 깃발 그림을 화면에 몇 배로(사용자 10/11: 24px)
        private const int PixelScale = 3;
        // 깃발 그림의 왼쪽 아래 자리(몸 그림의 왼쪽 아래에서, 원본 px) — 깃발 그림을 좌우로 뒤집어 든다(사용자 10/11): 깃대가 몸 바로 뒤에 붙고 천이 몸 바깥(뒤쪽)으로 펄럭인다
        private static readonly Vector2 FlagAt = new Vector2(-7f, 3f);
        // 표시 자리(기준점)에서 좌우 · 위 · 아래로 비우는 칸 — 아이콘 판 둘과 번호에, 깃발을 든 그림의 크기(깃발 쪽 8칸 · 위로 6칸 · 아래로 3칸)를 더한 것
        private const int MarkHalfWidth = 17, MarkAbove = 11, MarkBelow = 15;

        private class Walker
        {
            // 걷는 땅의 주인 — 보통 그림의 영주 자신, 연합에 든 영주의 그림이면 연합(바탕 영주)
            public int Lord;
            public RectTransform Rect;
            // 지도 칸 좌표(위 왼쪽이 0, 0)
            public Vector2 Cell, Target, From;
            public bool HasTarget;
            public float Rest;
            // 침략 걸음 — Marching이면 From → Target, Returning이면 돌아오는 중. Clock = 그 걸음이 시작된 뒤 흐른 시간
            public bool Marching, Returning, Won;
            public float Clock;
        }

        private int[] _cells;
        private int _width, _height;
        private float _scale;
        private Vector2Int[] _anchors, _spots;
        private Sprite _body, _flag;
        private int[] _owner;
        private int _coalition = -1;
        private int[] _members;
        private readonly Dictionary<int, Walker> _walkers = new Dictionary<int, Walker>();
        private readonly List<int> _gone = new List<int>();
        // 연출용 난수 — 규칙의 난수(UnityEngine.Random · 런의 System.Random)와 섞지 않는다
        private readonly System.Random _random = new System.Random(20261011);

        public int Count => _walkers.Count;

        // cells = [y × width + x] 땅 번호(바다는 음수), scale = 지도 한 칸의 크기(px), anchors = 땅마다 표시 자리, body = 유닛 몸 그림(영주 색으로 칠한다), flag = 드는 깃발(흰 그림 — 영주 색을 입힌다)
        public void Init(int[] cells, int width, int height, float scale, Vector2Int[] anchors, Sprite body, Sprite flag)
        {
            _cells = cells;
            _width = width;
            _height = height;
            _scale = scale;
            _anchors = anchors;
            _body = body;
            _flag = flag;
            _spots = new Vector2Int[anchors.Length];
            for (int region = 0; region < anchors.Length; region++)
                _spots[region] = SpotOf(region);
        }

        // 땅 안에서 서 있기 좋은 칸 — 표시 자리 둘레에서 먼저 찾고, 없으면 그 땅의 아무 칸
        private Vector2Int SpotOf(int region)
        {
            Vector2Int anchor = _anchors[region];
            Vector2Int[] around = { new Vector2Int(0, 18), new Vector2Int(-21, 2), new Vector2Int(21, 2), new Vector2Int(0, -14), new Vector2Int(-19, 17), new Vector2Int(19, 17), new Vector2Int(-19, -13), new Vector2Int(19, -13) };
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
        public void Sync(int[] owner, System.Func<int, Color> colorOf)
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
                Destroy(_walkers[lord].Rect.gameObject);
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
            // 깃발(영주 색으로 통째로 칠한다) — 몸 그림의 왼쪽 아래를 (0, 0)으로 깃발의 왼쪽 아래가 FlagAt
            Vector2 flagAt = (FlagAt + _flag.rect.size / 2f - _body.rect.size / 2f) * PixelScale;
            Sprite body = LordPaint.Of(_body, color);
            // 어느 채움 색 위에서도(내 땅의 밝은 파랑 포함) 묻히지 않게 검은 테두리 — 깃발과 몸을 한 덩어리로 두르도록 검은 그림자 둘을 먼저 깔고 그 위에 그린다
            Part(rect, _flag, flagAt, Color.black, true, true);
            Part(rect, body, Vector2.zero, Color.black, true, false);
            Part(rect, _flag, flagAt, color, false, true);
            Part(rect, body, Vector2.zero, Color.white, false, false);
            var walker = new Walker { Lord = home, Rect = rect, Cell = Scatter(home, region), Rest = RestMin + (float)_random.NextDouble() * (RestMax - RestMin) };
            Place(walker, 1f);
            return walker;
        }

        // 그림 한 겹 — 원본 크기의 정수배(PixelScale). outline이면 검은 테두리를 두르고, mirror면 좌우로 뒤집어 그린다
        private static void Part(RectTransform parent, Sprite sprite, Vector2 position, Color color, bool outline, bool mirror)
        {
            var image = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.raycastTarget = false;
            image.sprite = sprite;
            image.color = color;
            // 좌우 뒤집기가 localScale을 쓴다 — Simple + Preserve Aspect(CLAUDE.md §5-1)
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = sprite.rect.size * PixelScale;
            image.rectTransform.anchoredPosition = position;
            if (mirror)
                image.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            if (!outline)
                return;
            var edge = image.gameObject.AddComponent<Outline>();
            edge.effectColor = Color.black;
            edge.effectDistance = new Vector2(PixelScale, PixelScale);
        }

        // lord가 region 땅으로 침략하러 걸어 들어간다 — 이겼으면 거기 남고, 졌으면 돌아온다
        public void March(int lord, int region, bool won)
        {
            if (!_walkers.TryGetValue(lord, out Walker walker))
                return;
            walker.Marching = true;
            walker.Returning = false;
            walker.Won = won;
            walker.Clock = 0f;
            walker.From = walker.Cell;
            walker.Target = _spots[region];
            walker.HasTarget = false;
        }

        // 걸음을 전부 즉시 끝낸다(재생 건너뛰기) — 이긴 걸음은 그 땅에, 진 걸음은 제자리에
        public void Snap()
        {
            foreach (Walker walker in _walkers.Values)
            {
                if (!walker.Marching)
                    continue;
                walker.Cell = walker.Returning ? walker.Target : walker.Won ? walker.Target : walker.From;
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
        }

        private void StepWander(Walker walker, float dt)
        {
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
                walker.Rect.localScale = new Vector3(dx < 0f ? -1f : 1f, 1f, 1f);
        }
    }
}

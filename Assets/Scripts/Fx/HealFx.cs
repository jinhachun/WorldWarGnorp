using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 회복 연출(사용자 10/23 — 회복은 스탯이 오르는 게 아니라 체력이 차는 것): 출처(기물의 층 · 유닛 · 본진)에서 회복받는 대상으로 사제의 회복 오브와 같은
    // 초록 네모가 날아가고, 닿으면 대상에서 초록 십자가 뾰로롱 피어오르며 회복 숫자가 뜬다. 체력은 이미 찼다(판정은 그대로 — 연출만 날아간다).
    // 게임 코드는 둘만 알린다 — 체력이 찬 순간 Received(Unit.Heal · Base.HealRatio), 출처가 있는 회복이면 BuffLinks(BuffKind.Heal) → BuffLinkFx가 Orb.
    // 같은 프레임 안에서 둘을 짝짓는다(LateUpdate): 짝이 있으면 숫자 · 십자는 오브가 닿을 때, 없으면(사제 오브가 닿음 · 화면 밖) 바로. 실제로 안 찬 대상의 오브는 버린다.
    // 대상 하나에 날아가는 오브는 하나 — 그 사이 또 찬 회복은 그 오브에 합친다(흡혈처럼 타격마다 차는 것). 네모 전부를 메시 하나로(PixelMesh). FxDirector가 만든다
    public class HealFx : MonoBehaviour
    {
        private const int OrbCapacity = 96;
        private const int CrossCapacity = 384;
        private const int SortingOrder = 158;   // 버프 선(150) 위 · 상태 점(160) 아래
        // 오브 — 사제 오브(HealOrb 프리팹: 0.3칸 네모)와 같은 색 · 크기(3px)
        private const int OrbSize = 3;
        // 메시 정점 색은 선형으로 읽혀 화면에서 더 밝게 나온다 — 사제 오브(스프라이트 색 0.45 · 1 · 0.5)와 화면 색이 같도록 .linear
        private static readonly Color OrbColor = new Color(0.45f, 1f, 0.5f).linear;
        // 비행 시간 = 기본 + 거리당(상한·하한) · 호 높이 = 거리 × 비율(상한·하한)
        private const float FlightBase = 0.3f;
        private const float FlightPerUnit = 0.025f;
        private const float FlightMin = 0.35f;
        private const float FlightMax = 0.7f;
        private const float ArcRatio = 0.25f;
        private const float ArcMin = 0.5f;
        private const float ArcMax = 1.5f;
        // 십자 — 3×3 「+」. 처음 · 끝 몇 몫은 1px 점(뾰로롱 켜졌다 꺼짐)
        private const float CrossLifeMin = 0.4f;
        private const float CrossLifeMax = 0.6f;
        private const float CrossDelayMax = 0.12f;   // 하나씩 차례로 켜진다
        private const float CrossRise = 1.6f;        // 처음 오르는 속도(칸/초) — 위로 갈수록 느려진다
        private const float DotShare = 0.18f;
        private static readonly Color CrossColor = new Color(0.25f, 0.9f, 0.35f);
        private static readonly Color CrossCore = new Color(0.75f, 1f, 0.75f);
        // 같은 대상에 이 시간 안에 또 피면 하나만(흡혈 · 크루세이더처럼 자주 차는 것)
        private const float RepeatGap = 0.25f;

        private class Orb
        {
            public Vector2 From;
            public Vector2 To;
            public object Target;
            public float Age;
            public float Duration;
            public float Arc;
            public bool Fresh;      // 이번 프레임에 났다 — 짝(Received)이 없으면 버린다
            public float Amount;    // 닿을 때 띄울 회복량(합친 것)
            public float MaxHp;
            public Vector2 Point;   // 숫자 자리(대상이 유닛이 아니면 이 자리)
        }

        private struct Receipt
        {
            public object Target;
            public Vector2 Point;
            public float Amount;
            public float MaxHp;
        }

        private struct Cross
        {
            public Vector2 Point;
            public float Delay;
            public float Age;
            public float Life;
        }

        private static HealFx _instance;
        private PixelMesh _mesh;
        private readonly List<Orb> _orbs = new List<Orb>();
        private readonly Stack<Orb> _free = new Stack<Orb>();
        private readonly List<Receipt> _receipts = new List<Receipt>();
        private readonly List<Cross> _crosses = new List<Cross>();
        private readonly Dictionary<object, float> _lastBurst = new Dictionary<object, float>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "HealFx", material, SortingOrder, OrbCapacity + CrossCapacity * 3);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 대상의 체력이 amount만큼 찼다 — point = 회복 숫자 자리(머리 위)
        public static void Received(object target, Vector2 point, float amount, float maxHp)
        {
            if (_instance == null)
            {
                DamageNumbers.Heal(target, point, amount);
                return;
            }
            _instance._receipts.Add(new Receipt { Target = target, Point = point, Amount = amount, MaxHp = maxHp });
        }

        // 출처 from에서 대상으로 회복 오브 — 대상이 유닛이면 그 유닛을 따라간다. 이미 그 대상에 날아가는 오브가 있으면 그걸 쓴다
        public static void Launch(Vector2 from, object target, Vector2 to)
        {
            if (_instance == null || target == null)
                return;
            List<Orb> orbs = _instance._orbs;
            for (int i = 0; i < orbs.Count; i++)
                if (orbs[i].Target == target)
                    return;
            if (orbs.Count >= OrbCapacity)
                return;
            Orb orb = _instance._free.Count > 0 ? _instance._free.Pop() : new Orb();
            float distance = Vector2.Distance(from, to);
            orb.From = from;
            orb.To = to;
            orb.Target = target;
            orb.Age = 0f;
            orb.Duration = Mathf.Clamp(FlightBase + distance * FlightPerUnit, FlightMin, FlightMax);
            orb.Arc = Mathf.Clamp(distance * ArcRatio, ArcMin, ArcMax);
            orb.Fresh = true;
            orb.Amount = 0f;
            orb.MaxHp = 0f;
            orb.Point = to;
            orbs.Add(orb);
        }

        private void LateUpdate()
        {
            Match();
            float dt = Time.deltaTime;
            _mesh.Begin();
            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                Orb orb = _orbs[i];
                if (orb.Target is Unit unit && unit.IsAlive)
                {
                    orb.To = unit.Position;
                    orb.Point = new Vector2(unit.Position.x, unit.Top);
                }
                orb.Age += dt;
                if (orb.Age >= orb.Duration)
                {
                    // 날아가는 사이 쓰러졌으면 피지 않는다
                    if (orb.Amount > 0f && !(orb.Target is Unit dead && !dead.IsAlive))
                        Burst(orb.Target, orb.Point, orb.Amount, orb.MaxHp);
                    _orbs.RemoveAt(i);
                    _free.Push(orb);
                    continue;
                }
                float s = orb.Age / orb.Duration;
                Vector2 at = Vector2.Lerp(orb.From, orb.To, s) + new Vector2(0f, orb.Arc * 4f * s * (1f - s));
                _mesh.Square(at, OrbSize, OrbColor);
            }
            for (int i = _crosses.Count - 1; i >= 0; i--)
            {
                Cross c = _crosses[i];
                if (c.Delay > 0f)
                {
                    c.Delay -= dt;
                    _crosses[i] = c;
                    continue;
                }
                c.Age += dt;
                if (c.Age >= c.Life)
                {
                    _crosses.RemoveAt(i);
                    continue;
                }
                float t = c.Age / c.Life;
                // 위로 갈수록 느려진다
                c.Point.y += CrossRise * (1f - t) * dt;
                _crosses[i] = c;
                int x = PixelMesh.ToGrid(c.Point.x), y = PixelMesh.ToGrid(c.Point.y);
                if (t < DotShare || t > 1f - DotShare)
                    _mesh.Rect(x, y, 1, 1, CrossColor);
                else
                {
                    _mesh.Rect(x - 1, y, 3, 1, CrossColor);
                    _mesh.Rect(x, y - 1, 1, 1, CrossColor);
                    _mesh.Rect(x, y + 1, 1, 1, CrossColor);
                    _mesh.Rect(x, y, 1, 1, CrossCore);
                }
            }
            _mesh.End();
        }

        // 이번 프레임의 회복과 오브를 짝짓는다 — 짝 없는 회복은 바로 피고, 짝 없는 새 오브(실제로 안 찬 대상)는 버린다
        private void Match()
        {
            foreach (Receipt r in _receipts)
            {
                Orb orb = null;
                for (int i = 0; i < _orbs.Count; i++)
                    if (_orbs[i].Target == r.Target)
                    {
                        orb = _orbs[i];
                        break;
                    }
                if (orb == null || r.Amount <= 0f)
                {
                    Burst(r.Target, r.Point, r.Amount, r.MaxHp);
                    continue;
                }
                orb.Amount += r.Amount;
                orb.MaxHp = r.MaxHp;
                orb.Point = r.Point;
                orb.Fresh = false;
            }
            _receipts.Clear();
            for (int i = _orbs.Count - 1; i >= 0; i--)
                if (_orbs[i].Fresh)
                {
                    _free.Push(_orbs[i]);
                    _orbs.RemoveAt(i);
                }
        }

        // 숫자 + 십자 — 십자 수는 최대 체력 대비 회복량으로 2~5개, 같은 대상이 방금 피었으면 1개
        private void Burst(object target, Vector2 point, float amount, float maxHp)
        {
            DamageNumbers.Heal(target, point, amount);
            if (amount <= 0f)
                return;
            float now = Time.time;
            bool repeat = _lastBurst.TryGetValue(target, out float last) && now - last < RepeatGap && now >= last;
            _lastBurst[target] = now;
            float ratio = maxHp > 0f ? amount / maxHp : 0f;
            int count = repeat ? 1 : 2 + Mathf.Min(3, Mathf.FloorToInt(ratio / 0.1f));
            float side = Random.value < 0.5f ? -1f : 1f;
            for (int k = 0; k < count && _crosses.Count < CrossCapacity; k++)
            {
                // 흰 몸 위의 연두는 안 보이고 숫자는 머리 위 가운데에 뜬다 — 유닛은 몸 양옆(번갈아)에서, 타워는 지붕 위에서 핀다
                Vector2 at;
                if (target is Base tower)
                    at = new Vector2(point.x + Random.Range(-1f, 1f) * tower.Bounds.extents.x * 0.6f, point.y + Random.Range(0f, 0.25f));
                else
                {
                    float height = target is Unit unit ? unit.Top - unit.Feet : 1f;
                    side = -side;
                    at = new Vector2(point.x + side * (height * 0.5f + Random.Range(1f, 3f) * PixelMesh.Pixel), point.y - Random.Range(0.1f, 0.8f) * height);
                }
                _crosses.Add(new Cross
                {
                    Point = at,
                    Delay = k == 0 ? 0f : Random.Range(0f, CrossDelayMax),
                    Life = Random.Range(CrossLifeMin, CrossLifeMax),
                });
            }
            if (_lastBurst.Count > 512)
                _lastBurst.Clear();
        }
    }
}

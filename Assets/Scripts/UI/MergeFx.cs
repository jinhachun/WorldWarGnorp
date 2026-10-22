using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 상점의 합치기 재생(사용자 10/11) — 상태(RunState)는 이미 끝났고, 화면만 한 단계씩 차례로 보여 준다:
    // 들어오는 조각이 짝에게 빨려 들어가 겹치는 순간 짝이 크게 튀고 · 번쩍이고 · 창틀이 새 등급 색으로 바뀌고 · 조각 파편이 튄다. 그 결과가 다음 짝으로 다시 날아간다.
    // 조각들은 끌기 고스트를 본뜬 그림(실제 칸이 아니다)이고, 끝나면 결과가 놓인 실제 칸으로 내려앉은 뒤 사라진다.
    // 재생 중에는 결과 기물의 실제 칸을 빈 칸으로 보이게 한다(Concealed — BoardView가 읽는다). 무엇이든 끝내야 하면 Finish로 즉시 끝 상태.
    // 등급 연출(사용자 10/15 「뱀서 같은 느낌 · 랜덤 · 뒤 후광」) — 나는 동안 재료 등급 색 트레일(머리 굵고 꼬리 가늘게 · 흰 심), 합쳐지는 순간 결과 등급의
    // 후광(계단 원 + 도는 빛살, 조각 뒤) · 흰 섬광 · 빛줄기 · 반짝이(모두 매번 무작위). 연출은 재생 길이(Playing)와 따로 돈다 — 업그레이드 고르기 창이 뜨는 때를 늦추지 않는다.
    // 조각 · 연출은 캔버스 맨 위의 연출 판에 그린다(상점 팝업 판은 왼쪽 패널 밑이라 탑 둘레가 가려진다)
    public class MergeFx : MonoBehaviour
    {
        // 한 단계 = 날아가기 + 머무르기(그동안 튐이 돈다). 연쇄가 길어도 한 단계 길이는 같다
        public const float MoveSeconds = 0.12f;
        public const float HoldSeconds = 0.13f;
        private const float PunchSeconds = 0.18f;
        private const float PunchScale = 1.4f;
        private const float FlashSeconds = 0.08f;
        private const float FlashAlpha = 0.7f;
        // 끝나고 결과의 실제 칸으로 내려앉는 시간
        public const float SettleSeconds = 0.1f;
        // 비행 무작위 — 출발 지연(초) · 비행 시간 배율 · 옆으로 휘는 세기(거리의 비율) · 기울기(도)
        private const float FlightDelayMax = 0.05f;
        private const float FlightMin = 0.85f, FlightMax = 1.15f;
        private const float BendMin = 0.08f, BendMax = 0.3f;
        private const float TiltMin = 5f, TiltMax = 20f;
        // 파편 — 한 번에 튀는 수 · 한 변(px) · 속도(px/초) · 수명(초)
        private const int Shards = 8;
        private const float ShardSize = 8f;
        private const float ShardSpeed = 420f;
        private const float ShardSeconds = 0.25f;

        // ── 등급 연출 — 그림 1px = 화면 Dot px 격자에 맞춘 단색 네모들(그라데이션 없이 단계로)
        private const float Dot = 3f;
        // 트레일 — 점 간격(px) · 머리 굵기 → 꼬리 굵기(px) · 수명 · 머리의 흰 심
        private const float TrailGap = Dot * 2f;
        private const float TrailHead = Dot * 6f;
        private const float TrailSeconds = 0.2f;
        private const float CoreSize = Dot * 2f;
        private const float CoreSeconds = 0.05f;
        // 폭발 — 흰 섬광 · 빛줄기(수 = 기본 + 등급마다) · 반짝이
        private const float GlorySeconds = 0.5f;
        private const float GlowSeconds = 0.1f;
        private const float GlowSize = Dot * 24f;
        private const int RaysBase = 4;
        private const int RaysPerRarity = 2;
        private const int RayDots = 5;
        private const float RayFrom = Dot * 8f;
        private const float RayReach = Dot * 30f;
        private const int SparksBase = 3;
        private const float SparkSpread = Dot * 36f;
        // 후광 — 반지름(그림 px)이 커지는 범위 · 수명 범위 · 세 단계 링의 알파 · 도는 빛살
        // 탑 칸(120px)보다 한참 커야 둘레로 비친다 — 반지름 24 → 50 그림 px(화면 72 → 150px)
        private const int HaloFrom = 24;
        private const int HaloTo = 50;
        private const float HaloMin = 0.6f, HaloMax = 0.8f;
        private static readonly float[] HaloSteps = { 0.55f, 0.35f, 0.2f };
        private const int BeamsBase = 6;
        private const int BeamDots = 6;
        private const float BeamTurn = 1.2f;
        private const float BeamAlpha = 0.35f;

        private RectTransform _template;
        private RectTransform _behind;
        private RectTransform _layer, _haloLayer;
        private readonly List<GameObject> _made = new List<GameObject>();
        // 등급 연출 그림 — 재생(Finish)과 따로 치운다(끝까지 보이게). 꺼질 때 남은 것만 치운다
        private readonly List<GameObject> _glory = new List<GameObject>();
        private Coroutine _playing;
        // 후광 계단 원 — 반지름(그림 px)마다 한 번 만든다(흰 그림, 등급 색은 칠한다)
        private static readonly Dictionary<int, Sprite> Halos = new Dictionary<int, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Halos.Clear();

        // 재생 중 실제 칸을 숨길 기물(결과) — 재생이 없으면 null
        public OwnedBuilding Concealed { get; private set; }
        public bool Playing => _playing != null;

        // template = 끌기 고스트(탑 층 조각 그림 — 레이캐스트를 막지 않는다) · behind = 후광을 그 바로 뒤에 그릴 판(내 탑의 필드 칸들)
        public void Init(RectTransform template, RectTransform behind)
        {
            _template = template;
            _behind = behind;
        }

        // from = 방금 산 조각이 출발하는 자리, targets[i] = i번째 단계의 짝이 있던 자리(화면 좌표), settle = 결과의 실제 칸을 찾는 함수(재생이 끝날 때 묻는다).
        // thrown = 짐꾼이 던진 구매(진열 클릭) — 첫 비행이 던지기 포물선(Arc)이다
        public void Play(BuildingDefinition definition, Vector3 from, IReadOnlyList<RunState.MergeStep> steps, Vector3[] targets, System.Func<Vector3> settle, bool thrown = false)
        {
            Finish();
            Concealed = steps[steps.Count - 1].Into;
            _playing = StartCoroutine(Run(definition, from, steps, targets, settle, thrown));
        }

        // ── 던지기(사용자 10/23) — 진열을 눌러 사면 짐꾼이 이고 있던 층 조각을 그 기물이 들어간 칸으로 포물선을 그리며 던진다(계단식).
        // 나는 동안 그 칸은 빈 칸으로 보이고(Thrown — ShopPanel.Concealed로 BoardView가 읽는다), 닿으면 조각이 한 번 눌렸다 사라지며 실제 칸이 나타난다.
        // 여럿이 동시에 날 수 있고, 합치기 재생(Finish)과 따로 돈다
        public const float ThrowSeconds = 0.3f;
        // 출발 · 도착 중 높은 쪽보다 더 솟는 높이(px) · 자리를 끊는 격자(px — 짐꾼 그림 1px, UI/OfferCard) · 닿아서 눌린 채 있는 시간 · 눌린 모양
        private const float ThrowArc = 160f;
        private const float ThrowGrid = 6f;
        private const float LandSeconds = 0.05f;
        private static readonly Vector3 LandSquash = new Vector3(1.15f, 0.8f, 1f);

        private readonly Dictionary<OwnedBuilding, (Coroutine Run, GameObject Piece)> _throws = new Dictionary<OwnedBuilding, (Coroutine, GameObject)>();

        public bool Thrown(OwnedBuilding building) => building != null && _throws.ContainsKey(building);

        // building = 방금 산 기물(이미 칸에 들어가 있다) · from = 짐꾼 머리 위 층 조각 자리 · target = 그 기물의 실제 칸 자리(매 프레임 묻는다 — 보관함 조각은 움직인다)
        public void Throw(OwnedBuilding building, Vector3 from, System.Func<Vector3> target)
        {
            RectTransform piece = Piece(building.Definition, building.Rarity, from);
            _made.Remove(piece.gameObject);
            _throws[building] = (StartCoroutine(RunThrow(building, piece, from, target)), piece.gameObject);
        }

        // 나는 조각을 다 치우고 실제 칸을 보인다(칸을 집을 때 — ShopPanel)
        public void FinishThrows()
        {
            foreach (var run in _throws.Values)
            {
                if (run.Run != null)
                    StopCoroutine(run.Run);
                if (run.Piece != null)
                    Destroy(run.Piece);
            }
            _throws.Clear();
        }

        private IEnumerator RunThrow(OwnedBuilding building, RectTransform piece, Vector3 from, System.Func<Vector3> target)
        {
            Layer.SetAsLastSibling();
            for (float t = 0f; t < ThrowSeconds; t += Step())
            {
                piece.position = Arc(from, target(), t / ThrowSeconds);
                piece.SetAsLastSibling();
                yield return null;
            }
            piece.position = target();
            piece.localScale = LandSquash;
            for (float t = 0f; t < LandSeconds; t += Step())
                yield return null;
            // 실제 칸이 먼저 나타난 다음 프레임에 조각을 치운다(빈 칸 한 프레임이 안 보이게 — Run과 같다)
            _throws.Remove(building);
            yield return null;
            Destroy(piece.gameObject);
        }

        // 던지기 포물선 위 k(0~1)의 자리 — 격자로 끊어 계단식으로
        private Vector3 Arc(Vector3 from, Vector3 to, float k)
        {
            float scale = Layer.lossyScale.y;
            var top = new Vector3((from.x + to.x) * 0.5f, Mathf.Max(from.y, to.y) + ThrowArc * scale, from.z);
            float u = 1f - k;
            Vector3 p = u * u * from + 2f * u * k * top + k * k * to;
            float grid = ThrowGrid * scale;
            return new Vector3(Mathf.Round(p.x / grid) * grid, Mathf.Round(p.y / grid) * grid, p.z);
        }

        // 즉시 끝 상태로 — 그림을 다 치우고 실제 칸을 다시 보인다
        public void Finish()
        {
            if (_playing != null)
                StopCoroutine(_playing);
            _playing = null;
            Concealed = null;
            foreach (GameObject made in _made)
                if (made != null)
                    Destroy(made);
            _made.Clear();
        }

        private void OnDisable()
        {
            Finish();
            FinishThrows();
            foreach (GameObject made in _glory)
                if (made != null)
                    Destroy(made);
            _glory.Clear();
        }

        private void OnDestroy()
        {
            if (_layer != null)
                Destroy(_layer.gameObject);
            if (_haloLayer != null)
                Destroy(_haloLayer.gameObject);
        }

        // 연출 판 — 캔버스 맨 위를 덮는 빈 판(처음 쓸 때 만든다). 재생마다 맨 위로
        private RectTransform Layer => _layer != null ? _layer : _layer = NewLayer("MergeFxLayer", _template.GetComponentInParent<Canvas>().rootCanvas.transform);

        // 후광 판 — 내 탑(필드 칸들) 바로 뒤. 탑은 상점이 열린 뒤 자리를 옮기므로 재생마다 그 바로 뒤로 다시 붙인다
        private RectTransform HaloLayer
        {
            get
            {
                if (_haloLayer == null)
                    _haloLayer = NewLayer("MergeHaloLayer", _behind.parent);
                if (_haloLayer.parent != _behind.parent)
                    _haloLayer.SetParent(_behind.parent, false);
                // 바로 앞 자리 — 이미 뒤에 있으면 탑 자리로, 더 앞에 있으면 탑 하나 앞으로(옮기면 번호가 밀린다)
                int behind = _behind.GetSiblingIndex(), at = _haloLayer.GetSiblingIndex();
                if (at > behind)
                    _haloLayer.SetSiblingIndex(behind);
                else if (at < behind - 1)
                    _haloLayer.SetSiblingIndex(behind - 1);
                return _haloLayer;
            }
        }

        private static RectTransform NewLayer(string name, Transform parent)
        {
            var layer = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            layer.SetParent(parent, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            return layer;
        }

        private IEnumerator Run(BuildingDefinition definition, Vector3 from, IReadOnlyList<RunState.MergeStep> steps, Vector3[] targets, System.Func<Vector3> settle, bool thrown)
        {
            Layer.SetAsLastSibling();
            // 짝들은 처음부터 제자리에 — 합쳐지기 전 등급의 모습으로
            var pieces = new RectTransform[steps.Count];
            for (int i = 0; i < steps.Count; i++)
                pieces[i] = Piece(definition, steps[i].After - 1, targets[i]);
            // 들어오는 조각은 첫 짝과 같은 등급이다(중립외교로 오른 채 산 조각이면 시작 등급보다 높다)
            RectTransform mover = Piece(definition, steps[0].After - 1, from);
            for (int i = 0; i < steps.Count; i++)
            {
                mover.SetAsLastSibling();
                Vector3 start = mover.position;
                Color material = (steps[i].After - 1).FrameColor();
                // 비행마다 무작위(사용자 10/15 「자연스럽게」) — 출발 지연 · 비행 시간 · 옆으로 휘는 방향과 세기 · 기울기.
                // 한 단계 길이(날기 + 머무르기)는 그대로 — 지연 · 비행이 길어진 만큼 머무르기를 줄인다(업그레이드 고르기 창 시점이 안 바뀐다)
                float delay = Random.Range(0f, FlightDelayMax);
                float flight = MoveSeconds * Random.Range(FlightMin, FlightMax);
                float hold = MoveSeconds + HoldSeconds - delay - flight;
                Vector3 path = targets[i] - start;
                Vector3 side = new Vector3(-path.y, path.x, 0f) * (Random.Range(BendMin, BendMax) * (Random.value < 0.5f ? -1f : 1f));
                Vector3 bend = (start + targets[i]) * 0.5f + side;
                float tilt = Random.Range(TiltMin, TiltMax) * (Random.value < 0.5f ? -1f : 1f);
                for (float t = 0f; t < delay; t += Step())
                    yield return null;
                // 짐꾼이 던진 첫 비행은 던지기 포물선 그대로(바로 출발 · ThrowSeconds) — 닿은 뒤 머무르기는 줄이지 않는다
                bool arc = thrown && i == 0;
                if (arc)
                {
                    delay = 0f;
                    flight = ThrowSeconds;
                    hold = HoldSeconds;
                }
                Vector3 trailFrom = start;
                for (float t = 0f; t < flight; t += Step())
                {
                    // 빨려 들어가듯 — 점점 빨라진다(휜 길: 2차 베지어)
                    float k = t / flight, e = k * k, u = 1f - e;
                    mover.position = arc ? Arc(start, targets[i], k) : u * u * start + 2f * u * e * bend + e * e * targets[i];
                    mover.localEulerAngles = new Vector3(0f, 0f, arc ? 0f : tilt * e);
                    trailFrom = Trail(trailFrom, mover.position, material);
                    mover.SetAsLastSibling();
                    yield return null;
                }
                Destroy(mover.gameObject);
                mover = pieces[i];
                Paint(mover, steps[i].After);
                StartCoroutine(Punch(mover));
                Burst(targets[i], steps[i].After.FrameColor());
                StartCoroutine(Halo(targets[i], steps[i].After));
                StartCoroutine(Glory(targets[i], steps[i].After));
                for (float t = 0f; t < hold; t += Step())
                    yield return null;
            }
            // 결과가 실제로 놓인 칸으로(빈 층이 메워져 자리가 달라졌을 수 있다)
            Vector3 home = settle();
            Vector3 last = mover.position;
            if ((home - last).sqrMagnitude > 1f)
                for (float t = 0f; t < SettleSeconds; t += Step())
                {
                    mover.position = Vector3.Lerp(last, home, t / SettleSeconds);
                    yield return null;
                }
            // 실제 칸이 먼저 나타난 다음 프레임에 그림을 치운다 — 같은 프레임에 치우면 한 프레임 동안 빈 칸이 보인다
            Concealed = null;
            yield return null;
            _playing = null;
            Finish();
        }

        // 한 프레임이 길어도 건너뛰지 않게 프레임당 진행을 묶는다(배속과 무관)
        private static float Step() => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        private RectTransform Piece(BuildingDefinition definition, BuildingRarity rarity, Vector3 position)
        {
            RectTransform piece = Instantiate(_template, Layer);
            piece.gameObject.SetActive(true);
            piece.position = position;
            piece.GetComponent<FloorLook>().Show(definition);
            foreach (Image image in piece.GetComponentsInChildren<Image>(true))
            {
                // 튐이 localScale을 쓴다 — 그 Image는 Simple + Preserve Aspect(CLAUDE.md §5-1)
                if (image.type == Image.Type.Simple)
                    image.preserveAspect = true;
                if (image.name == "Icon")
                    image.sprite = definition.Icon;
            }
            Paint(piece, rarity);
            _made.Add(piece.gameObject);
            return piece;
        }

        // 창틀 = 등급 색
        private static void Paint(RectTransform piece, BuildingRarity rarity)
            => piece.Find("Window").GetComponent<Image>().color = rarity.FrameColor();

        // 크게 튀었다 정확히 1로 돌아온다 · 그 위로 흰 번쩍임
        private IEnumerator Punch(RectTransform piece)
        {
            var flash = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            flash.raycastTarget = false;
            flash.rectTransform.SetParent(piece, false);
            flash.rectTransform.sizeDelta = piece.sizeDelta;
            for (float t = 0f; t < PunchSeconds && piece != null; t += Step())
            {
                piece.localScale = Vector3.one * Mathf.Lerp(PunchScale, 1f, t / PunchSeconds);
                flash.color = new Color(1f, 1f, 1f, FlashAlpha * Mathf.Clamp01(1f - t / FlashSeconds));
                yield return null;
            }
            if (piece != null)
            {
                piece.localScale = Vector3.one;
                Destroy(flash.gameObject);
            }
        }

        // 작은 네모 파편이 사방으로 — 새 등급 색
        private void Burst(Vector3 at, Color color)
        {
            for (int i = 0; i < Shards; i++)
            {
                var shard = new GameObject("Shard", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                shard.raycastTarget = false;
                shard.color = color;
                shard.rectTransform.SetParent(Layer, false);
                shard.rectTransform.sizeDelta = Vector2.one * ShardSize;
                shard.rectTransform.position = at;
                _made.Add(shard.gameObject);
                float angle = (i + 0.5f) / Shards * Mathf.PI * 2f;
                StartCoroutine(Fly(shard, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ShardSpeed));
            }
        }

        private IEnumerator Fly(Image shard, Vector2 velocity)
        {
            for (float t = 0f; t < ShardSeconds && shard != null; t += Step())
            {
                float k = t / ShardSeconds;
                shard.rectTransform.position += (Vector3)(velocity * (1f - k) * Step());
                shard.color = new Color(shard.color.r, shard.color.g, shard.color.b, 1f - k);
                yield return null;
            }
            if (shard != null)
                Destroy(shard.gameObject);
        }

        // ── 등급 연출 ──

        // 격자에 맞춘 단색 네모 하나(layer 맨 위 — 비우면 연출 판). at = 화면 자리
        private Image NewDot(Vector3 at, float size, Color color, RectTransform layer = null)
        {
            layer = layer != null ? layer : Layer;
            var dot = new GameObject("GloryDot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dot.raycastTarget = false;
            dot.color = color;
            dot.rectTransform.SetParent(layer, false);
            dot.rectTransform.sizeDelta = Vector2.one * Snap(size);
            Place(dot.rectTransform, layer.InverseTransformPoint(at));
            _glory.Add(dot.gameObject);
            return dot;
        }

        private static float Snap(float value) => Mathf.Max(Dot, Mathf.Round(value / Dot) * Dot);

        // 판 안쪽 좌표를 그림 1px 격자에 맞춰 놓는다
        private static void Place(RectTransform rect, Vector2 local)
            => rect.localPosition = new Vector3(Mathf.Round(local.x / Dot) * Dot, Mathf.Round(local.y / Dot) * Dot, 0f);

        private void Clean(Image dot)
        {
            if (dot == null)
                return;
            _glory.Remove(dot.gameObject);
            Destroy(dot.gameObject);
        }

        // 나는 조각 뒤에 남는 점들 — 지난 프레임 자리에서 지금 자리까지 간격마다 하나씩(빨리 날아도 끊기지 않게). 점은 가늘어지며 사라지므로
        // 오래된 꼬리일수록 가늘다. 맨 앞 점에는 흰 심. 마지막으로 찍은 자리를 돌려준다
        private Vector3 Trail(Vector3 from, Vector3 to, Color color)
        {
            float gap = TrailGap * Layer.lossyScale.x;
            int count = Mathf.FloorToInt(Vector3.Distance(from, to) / gap);
            Vector3 at = from;
            for (int i = 1; i <= count; i++)
            {
                at = Vector3.MoveTowards(from, to, gap * i);
                StartCoroutine(Shrink(NewDot(at, TrailHead, color), TrailHead, TrailSeconds));
            }
            if (count > 0)
                StartCoroutine(Shrink(NewDot(at, CoreSize, Color.white), CoreSize, CoreSeconds));
            return at;
        }

        // 가늘어지며(한 변 size → Dot) 옅어진다
        private IEnumerator Shrink(Image dot, float size, float seconds)
        {
            Color color = dot.color;
            for (float t = 0f; t < seconds && dot != null; t += Step())
            {
                float k = t / seconds;
                dot.rectTransform.sizeDelta = Vector2.one * Snap(Mathf.Lerp(size, Dot, k));
                dot.color = new Color(color.r, color.g, color.b, 1f - k * k);
                yield return null;
            }
            Clean(dot);
        }

        // 합쳐지는 순간의 앞 연출 — 흰 섬광 · 등급 색 빛줄기(줄마다 각도 · 길이 · 굵기 · 지연 · 속도가 무작위) · 반짝이(자리 · 크기 · 수명 무작위)
        private IEnumerator Glory(Vector3 at, BuildingRarity rarity)
        {
            Color color = rarity.FrameColor();
            Vector2 center = Layer.InverseTransformPoint(at);
            float glowSize = GlowSize * Random.Range(0.85f, 1.15f);
            Image glow = NewDot(at, glowSize, Color.white);

            int rayCount = RaysBase + RaysPerRarity * (int)rarity;
            float spacing = Mathf.PI * 2f / rayCount;
            var rays = new Image[rayCount * RayDots];
            var rayDir = new Vector2[rayCount];
            var rayLength = new float[rayCount];
            var rayDelay = new float[rayCount];
            var raySpeed = new float[rayCount];
            float turn = Random.value * spacing;
            for (int r = 0; r < rayCount; r++)
            {
                float angle = turn + r * spacing + Random.Range(-0.5f, 0.5f) * spacing;
                rayDir[r] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                rayLength[r] = RayReach * Random.Range(0.6f, 1.3f);
                rayDelay[r] = Random.Range(0f, 0.08f);
                raySpeed[r] = Random.Range(0.8f, 1.25f);
                int thick = Random.Range(2, 5);
                for (int d = 0; d < RayDots; d++)
                {
                    // 끝으로 갈수록 가늘게 · 머리는 흰색
                    rays[r * RayDots + d] = NewDot(at, Dot * Mathf.Max(1, thick - d / 2), d == 0 ? Color.white : color);
                    rays[r * RayDots + d].enabled = false;
                }
            }

            int sparkCount = SparksBase + (int)rarity + Random.Range(0, 3);
            var sparks = new Image[sparkCount];
            var sparkStart = new float[sparkCount];
            var sparkLife = new float[sparkCount];
            var sparkBlink = new float[sparkCount];
            for (int s = 0; s < sparkCount; s++)
            {
                Vector2 spot = center + Random.insideUnitCircle * SparkSpread;
                sparks[s] = NewDot(at, Dot * Random.Range(1, 4), Random.value < 0.5f ? Color.white : color);
                Place(sparks[s].rectTransform, spot);
                sparks[s].enabled = false;
                sparkStart[s] = Random.Range(0f, 0.15f);
                sparkLife[s] = Random.Range(0.2f, 0.45f);
                sparkBlink[s] = Random.Range(0.04f, 0.09f);
            }

            for (float t = 0f; t < GlorySeconds; t += Step())
            {
                if (glow != null)
                {
                    float g = Mathf.Clamp01(t / GlowSeconds);
                    glow.rectTransform.sizeDelta = Vector2.one * Snap(glowSize * (0.5f + 0.5f * g));
                    glow.color = new Color(1f, 1f, 1f, 1f - g);
                }
                for (int r = 0; r < rayCount; r++)
                {
                    float k = Mathf.Clamp01((t - rayDelay[r]) / (GlorySeconds - rayDelay[r]) * raySpeed[r]);
                    bool shown = t >= rayDelay[r] && k < 1f;
                    // 빨리 뻗었다가(제곱근) 끝에서 사라진다
                    float reach = RayFrom + rayLength[r] * Mathf.Sqrt(k);
                    for (int d = 0; d < RayDots; d++)
                    {
                        Image dot = rays[r * RayDots + d];
                        if (dot == null)
                            continue;
                        dot.enabled = shown;
                        Place(dot.rectTransform, center + rayDir[r] * (reach - d * Dot * 3f));
                        dot.color = new Color(dot.color.r, dot.color.g, dot.color.b, 1f - k);
                    }
                }
                for (int s = 0; s < sparkCount; s++)
                    if (sparks[s] != null)
                    {
                        float age = t - sparkStart[s];
                        sparks[s].enabled = age >= 0f && age < sparkLife[s] && (int)(age / sparkBlink[s]) % 2 == 0;
                    }
                yield return null;
            }

            foreach (Image dot in rays)
                Clean(dot);
            foreach (Image dot in sparks)
                Clean(dot);
            Clean(glow);
        }

        // 탑 뒤의 후광 — 결과 등급 색 계단 원(가운데 진하고 바깥 두 단계 옅게)이 천천히 커지며 사라지고, 그 위로 옅은 빛살이 천천히 돈다(탑 칸 둘레로 비친다)
        private IEnumerator Halo(Vector3 at, BuildingRarity rarity)
        {
            RectTransform layer = HaloLayer;
            Color color = rarity.FrameColor();
            Vector2 center = layer.InverseTransformPoint(at);
            float seconds = Random.Range(HaloMin, HaloMax);
            var halo = new GameObject("Halo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            halo.raycastTarget = false;
            halo.rectTransform.SetParent(layer, false);
            Place(halo.rectTransform, center);
            _glory.Add(halo.gameObject);

            int beamCount = BeamsBase + (int)rarity;
            var beams = new Image[beamCount * BeamDots];
            Color beamColor = Color.Lerp(color, Color.white, 0.3f);
            for (int i = 0; i < beams.Length; i++)
                beams[i] = NewDot(at, Dot * 2f, beamColor, layer);
            float spin = Random.value * Mathf.PI * 2f;
            float direction = Random.value < 0.5f ? -1f : 1f;

            for (float t = 0f; t < seconds && halo != null; t += Step())
            {
                float k = t / seconds;
                // 커지는 건 처음에 빠르게 · 사라지는 건 뒤 절반에
                int radius = Mathf.RoundToInt(Mathf.Lerp(HaloFrom, HaloTo, 1f - (1f - k) * (1f - k)));
                Sprite sprite = HaloSprite(radius);
                if (halo.sprite != sprite)
                {
                    halo.sprite = sprite;
                    halo.SetNativeSize();
                }
                float fade = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
                halo.color = new Color(color.r, color.g, color.b, fade);
                float angle = spin + direction * BeamTurn * t;
                for (int b = 0; b < beamCount; b++)
                {
                    float a = angle + b * Mathf.PI * 2f / beamCount;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    for (int d = 0; d < BeamDots; d++)
                    {
                        Image dot = beams[b * BeamDots + d];
                        if (dot == null)
                            continue;
                        Place(dot.rectTransform, center + dir * (radius * Dot * (0.5f + 0.18f * d)));
                        dot.color = new Color(beamColor.r, beamColor.g, beamColor.b, BeamAlpha * fade);
                    }
                }
                yield return null;
            }
            foreach (Image dot in beams)
                Clean(dot);
            Clean(halo);
        }

        // 반지름 radius(그림 px)의 계단 원 — 흰 그림에 알파 세 단계(가운데 · 가운데 링 · 바깥 링). 그림 1px = 화면 Dot px
        private static Sprite HaloSprite(int radius)
        {
            if (Halos.TryGetValue(radius, out Sprite sprite))
                return sprite;
            int size = radius * 2 + 1;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - radius) * (x - radius) + (y - radius) * (y - radius)) / radius;
                    float alpha = d <= 0.45f ? HaloSteps[0] : d <= 0.75f ? HaloSteps[1] : d <= 1f ? HaloSteps[2] : 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Halos[radius] = sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f / Dot);
            return sprite;
        }
    }
}

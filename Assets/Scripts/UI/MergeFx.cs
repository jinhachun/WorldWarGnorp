using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 상점의 합치기 재생(사용자 10/11) — 상태(RunState)는 이미 끝났고, 화면만 한 단계씩 차례로 보여 준다:
    // 들어오는 조각이 짝에게 빨려 들어가 겹치는 순간 짝이 크게 튀고 · 번쩍이고 · 창틀이 새 등급 색으로 바뀌고 · 조각 파편이 튄다. 그 결과가 다음 짝으로 다시 날아간다.
    // 조각들은 끌기 고스트를 본뜬 그림(실제 칸이 아니다)이고, 끝나면 결과가 놓인 실제 칸으로 내려앉은 뒤 사라진다.
    // 재생 중에는 결과 기물의 실제 칸을 빈 칸으로 보이게 한다(Concealed — BoardView가 읽는다). 무엇이든 끝내야 하면 Finish로 즉시 끝 상태
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
        // 파편 — 한 번에 튀는 수 · 한 변(px) · 속도(px/초) · 수명(초)
        private const int Shards = 8;
        private const float ShardSize = 8f;
        private const float ShardSpeed = 420f;
        private const float ShardSeconds = 0.25f;

        private RectTransform _template;
        private readonly List<GameObject> _made = new List<GameObject>();
        private Coroutine _playing;

        // 재생 중 실제 칸을 숨길 기물(결과) — 재생이 없으면 null
        public OwnedBuilding Concealed { get; private set; }
        public bool Playing => _playing != null;

        // template = 끌기 고스트(탑 층 조각 그림 — 레이캐스트를 막지 않는다)
        public void Init(RectTransform template) => _template = template;

        // from = 방금 산 조각이 출발하는 자리, targets[i] = i번째 단계의 짝이 있던 자리(화면 좌표), settle = 결과의 실제 칸을 찾는 함수(재생이 끝날 때 묻는다)
        public void Play(BuildingDefinition definition, Vector3 from, IReadOnlyList<RunState.MergeStep> steps, Vector3[] targets, System.Func<Vector3> settle)
        {
            Finish();
            Concealed = steps[steps.Count - 1].Into;
            _playing = StartCoroutine(Run(definition, from, steps, targets, settle));
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

        private void OnDisable() => Finish();

        private IEnumerator Run(BuildingDefinition definition, Vector3 from, IReadOnlyList<RunState.MergeStep> steps, Vector3[] targets, System.Func<Vector3> settle)
        {
            // 짝들은 처음부터 제자리에 — 합쳐지기 전 등급의 모습으로
            var pieces = new RectTransform[steps.Count];
            for (int i = 0; i < steps.Count; i++)
                pieces[i] = Piece(definition, steps[i].After - 1, targets[i]);
            RectTransform mover = Piece(definition, definition.Rarity, from);
            for (int i = 0; i < steps.Count; i++)
            {
                mover.SetAsLastSibling();
                Vector3 start = mover.position;
                for (float t = 0f; t < MoveSeconds; t += Step())
                {
                    // 빨려 들어가듯 — 점점 빨라진다
                    float k = t / MoveSeconds;
                    mover.position = Vector3.LerpUnclamped(start, targets[i], k * k);
                    yield return null;
                }
                Destroy(mover.gameObject);
                mover = pieces[i];
                Paint(mover, steps[i].After);
                StartCoroutine(Punch(mover));
                Burst(targets[i], steps[i].After.FrameColor());
                for (float t = 0f; t < HoldSeconds; t += Step())
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
            RectTransform piece = Instantiate(_template, _template.parent);
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
                shard.rectTransform.SetParent(_template.parent, false);
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
    }
}

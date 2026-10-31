using UnityEngine;

namespace GnorpWar
{
    // 손맛 공용 부품(Docs/폴리싱_계획.md J0) — 이징 · 튐(Punch) · 숫자 굴림. t는 0~1(지난 시간 ÷ 길이)로 받는다.
    // 세기 값(튐 크기 · 흔들림 · 시간)은 BattleConfig 「손맛」 묶음에 둔다 — 여기는 모양만
    public static class Juice
    {
        // 감속 — 빨리 출발해 천천히 멈춘다
        public static float OutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        // 넘쳤다 돌아옴 — 끝값을 overshoot만큼 지나쳤다가 앉는다
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }

        // 튐 — 맞는 순간 1 + amount로 커져 hold초 그대로 머물렀다가, back초 동안 1보다 살짝 작게 넘쳤다 1에 앉는다(사용자 10/31 — 커진 숫자가 눈에 남게)
        public static float Punch(float age, float amount, float hold, float back)
            => age < hold ? 1f + amount : 1f + amount * (1f - OutBack((age - hold) / back));

        // 크기 배율을 픽셀 한 칸 단위로 끊는다 — sizePx = 원래 크기(그 그림 · 글자의 픽셀 수). 그림 점이 고르지 않게 늘지 않게
        public static float SnapScale(float scale, float sizePx) => Mathf.Max(1f, Mathf.Round(scale * sizePx)) / sizePx;

        // 실제 시간의 프레임 간격 — 히트스톱 · 배속과 무관하게 흐른다. unscaledDeltaTime과 같지만, 녹화(Time.captureFramerate) 중에도 녹화 시간대로 흐른다
        // (unscaledDeltaTime은 녹화 중에도 벽시계라 영상 속 UI 연출이 빨라진다). 시간이 멈췄으면(timeScale 0) 벽시계
        public static float RealDeltaTime => Time.timeScale > 0f ? Time.deltaTime / Time.timeScale : Time.unscaledDeltaTime;

        // 숫자 굴림 — from에서 to로 감속하며 굴러간다(정수)
        public static int Roll(int from, int to, float t) => Mathf.RoundToInt(Mathf.Lerp(from, to, OutQuad(t)));
    }
}

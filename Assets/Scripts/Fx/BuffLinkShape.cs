using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 버프 선의 모양·시간 한 곳 — 전장(Fx/BuffLinkFx)과 UI 탑(UI/UiBuffLinkLines)이 같은 계산을 쓴다(그리는 법만 각자).
    // 선마다 씨앗으로 불룩함·치우침이 조금씩 다르다(같은 씨앗 = 같은 모양). 머리(흰 심지)가 출발에서 대상으로 점점 빠르게 날아가 닿는 순간 사라지고(맞음 표시 — HitRingShape가 받는다),
    // 가는 꼬리가 0.1초 안에 대상으로 빨려 든다(사용자 10/26 A안). 좌표 단위는 부르는 쪽 것(월드 칸 · UI px) — 게임 난수(Random)는 건드리지 않는다
    public static class BuffLinkShape
    {
        // 머리가 대상에 닿는 시각(초) — 버프 글자(Fx/BuffTexts) · 맞음 표시(HitRingShape)가 이때를 기준으로 한다
        public const float ArriveTime = 0.2f;
        // 닿은 뒤 꼬리가 다 빨려 드는 시간
        private const float TailTime = 0.1f;
        // 선 수명(실제 시간 — 배속과 무관)
        public const float Lifetime = ArriveTime + TailTime;
        // 약한 선(늘 켜진 효과를 대상이 발동할 때마다) — 알파에 곱한다. 굵기는 Pixels가 가늘게 돌려준다
        private const float WeakAlpha = 0.5f;

        // 수명 중 머리가 대상에 닿는 때 · 꼬리가 출발을 떠나는 때(비율)
        private const float HeadArrive = ArriveTime / Lifetime;
        private const float TailLeave = 0.15f;
        private const float TailAlpha = 0.3f;
        // 불룩함 = 두 끝 거리의 이 비율(부르는 쪽 하한·상한으로 묶은 뒤 선마다 ±30%)
        private const float BulgePerLength = 0.25f;
        private const int MaxPoints = 1024;

        private static int _seed;

        // 알림마다 새 씨앗 — 같은 프레임의 선들도 서로 다르게
        public static int NewSeed() => ++_seed;

        // 곡선 점을 출발 → 대상 순서로 채운다(점 i의 자리 비율 t = i / (Count - 1)).
        // bulgeToward = 불룩해질 쪽(선에 수직인 두 방향 중 이쪽을 향하는 것) · minBulge/maxBulge = 불룩함 하한·상한 · step = 점 간격 상한
        public static void Fill(List<Vector2> points, Vector2 from, Vector2 to, int seed, Vector2 bulgeToward, float minBulge, float maxBulge, float step)
        {
            points.Clear();
            Vector2 d = to - from;
            float length = d.magnitude;
            if (length < 0.0001f)
            {
                points.Add(from);
                points.Add(to);
                return;
            }
            Vector2 dir = d / length;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            if (Vector2.Dot(normal, bulgeToward) < 0f)
                normal = -normal;

            uint state = (uint)seed * 2654435761u + 12345u;
            Next(ref state);
            float bulge = Mathf.Clamp(length * BulgePerLength, minBulge, maxBulge) * Mathf.Lerp(0.7f, 1.3f, Next(ref state));
            // 3차 곡선 — 두 조절점의 자리·높이가 따로 놀아 봉우리가 한쪽으로 치우친다(둘 다 1.33배면 봉우리 높이 = bulge)
            Vector2 c1 = from + dir * (length * Mathf.Lerp(0.1f, 0.4f, Next(ref state))) + normal * (bulge * Mathf.Lerp(0.9f, 1.8f, Next(ref state)));
            Vector2 c2 = from + dir * (length * Mathf.Lerp(0.6f, 0.9f, Next(ref state))) + normal * (bulge * Mathf.Lerp(0.9f, 1.8f, Next(ref state)));

            // 조절점 꺾은선 길이가 곡선 길이의 상한
            float path = (c1 - from).magnitude + (c2 - c1).magnitude + (to - c2).magnitude;
            int count = Mathf.Clamp(Mathf.CeilToInt(path / step) + 1, 2, MaxPoints);
            for (int i = 0; i < count; i++)
            {
                float t = i / (count - 1f), u = 1f - t;
                points.Add(u * u * u * from + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * to);
            }
        }

        // 나이(0 ~ Lifetime) → 보이는 구간(자리 비율). 머리는 점점 빨라지며(ease-in) 대상에 닿고, 꼬리는 늦게 떠나 점점 빨리 따라붙는다
        public static void Visible(float age, out float tail, out float head)
        {
            float p = Mathf.Clamp01(age / Lifetime);
            float h = Mathf.Clamp01(p / HeadArrive);
            head = h * h;
            float q = Mathf.Clamp01((p - TailLeave) / (1f - TailLeave));
            tail = q * q;
        }

        // 머리가 닿았나 — 닿은 뒤엔 머리를 그리지 않는다(맞음 표시의 섬광이 받는다)
        public static bool Arrived(float age) => age >= ArriveTime;

        // 자리 비율 t인 점의 굵기(그림 픽셀 수) — 머리 5 · 3 · 2 · 꼬리 1(약한 선은 2 · 1, 닿은 뒤엔 1). 보이는 구간 밖이면 0
        public static int Pixels(float t, float tail, float head, bool weak, bool arrived)
        {
            if (t < tail || t > head)
                return 0;
            if (arrived)
                return 1;
            float s = Along(t, tail, head);
            if (weak)
                return s > 0.6f ? 2 : 1;
            return s > 0.85f ? 5 : s > 0.6f ? 3 : s > 0.3f ? 2 : 1;
        }

        // 굵기 size인 네모 가운데의 흰 심지 굵기 — 5 → 3 · 3 → 1 · 그보다 가늘면 없음
        public static int Core(int size) => size >= 5 ? 3 : size >= 3 ? 1 : 0;

        // 자리 비율 t인 점의 알파 — 머리 1에서 꼬리로 갈수록 옅게
        public static float Alpha(float t, float tail, float head, bool weak)
            => Mathf.Lerp(TailAlpha, 1f, Along(t, tail, head)) * (weak ? WeakAlpha : 1f);

        // 보이는 구간 안에서의 자리(꼬리 0 ~ 머리 1)
        private static float Along(float t, float tail, float head)
            => head - tail < 0.0001f ? 1f : Mathf.Clamp01((t - tail) / (head - tail));

        private static float Next(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) * (1f / 16777216f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 맞음 표시 네모 한 장(격자) — 효과 색
    public struct HitRect
    {
        public int X, Y, W, H;
        public float Alpha;
    }

    // 버프 선이 기물(층)에 닿는 순간의 맞음 표시 모양·시간 한 곳(사용자 10/26 A안 · 10/27 섬광 · 튐 뺌) — 전장 탑(Fx/HitRingFx)과 UI 탑(UI/UiHitRings)이 같은 계산을 쓴다.
    // 좌표 단위는 격자 칸(전장 = 그림 1px · UI = 3px). 프레임은 60fps 기준 실제 시간(배속과 무관):
    // 2~3 창틀 바로 바깥 2칸 링 · 4~13 링이 2프레임마다 1칸씩 4칸까지 퍼지며 2칸 → 1칸 → 소멸. 약한 선(늘 켜진 효과 재알림)은 얇은 링이 조금만
    public static class HitRingShape
    {
        private const float Fps = 60f;
        // 선 머리가 닿는 때(알림 기준 초) — 이때가 0프레임
        public const float Start = BuffLinkShape.ArriveTime;
        public const float End = Start + 14f / Fps;

        public static int Frame(float age) => age < Start ? -1 : Mathf.FloorToInt((age - Start) * Fps);

        // box = 층 네모(격자 칸 (x0, y0)부터 w×h — 바깥 한 줄이 창틀). age = 알림부터 흐른 초
        public static void Rects(List<HitRect> rects, int x0, int y0, int w, int h, float age, bool weak)
        {
            rects.Clear();
            int f = Frame(age);
            if (f < 0 || w < 3 || h < 3)
                return;
            if (weak)
            {
                // 2~7프레임: 1칸 링이 0 · 1 · 2칸 퍼지며 옅어진다
                if (f >= 2 && f < 8)
                    Ring(rects, x0, y0, w, h, (f - 2) / 2, 1, f < 6 ? 1f : 0.5f);
                return;
            }
            if (f < 2 || f >= 14)
                return;
            // 2~3 0칸 · 4~5 1칸 · 6~7 2칸(굵기 2) · 8~9 3칸 · 10~13 4칸(굵기 1, 마지막 둘은 옅게)
            int step = (f - 2) / 2;
            int offset = Mathf.Min(step, 4);
            int thickness = step < 3 ? 2 : 1;
            Ring(rects, x0, y0, w, h, offset, thickness, f < 12 ? 1f : 0.5f);
        }

        // 네모 바로 바깥에서 offset칸 떨어진 굵기 thickness칸 링 — 네 변
        private static void Ring(List<HitRect> rects, int x0, int y0, int w, int h, int offset, int thickness, float alpha)
        {
            int pad = offset + thickness;
            int ox = x0 - pad, oy = y0 - pad, ow = w + 2 * pad, oh = h + 2 * pad;
            rects.Add(new HitRect { X = ox, Y = oy, W = ow, H = thickness, Alpha = alpha });
            rects.Add(new HitRect { X = ox, Y = oy + oh - thickness, W = ow, H = thickness, Alpha = alpha });
            rects.Add(new HitRect { X = ox, Y = oy + thickness, W = thickness, H = oh - 2 * thickness, Alpha = alpha });
            rects.Add(new HitRect { X = ox + ow - thickness, Y = oy + thickness, W = thickness, H = oh - 2 * thickness, Alpha = alpha });
        }
    }
}

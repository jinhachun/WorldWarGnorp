using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 맞음 표시 네모 한 장(격자) — White면 흰색, 아니면 효과 색
    public struct HitRect
    {
        public int X, Y, W, H;
        public float Alpha;
        public bool White;
    }

    // 맞음 표시의 종류 — 보통(섬광 + 튐 + 링) · 링만(같은 칸에 RepeatGap 안에 또 옴 — 섬광 겹침 방지) · 약한 선(늘 켜진 효과 재알림 — 얇은 링이 조금만)
    public enum HitRingMode { Full, RingOnly, Weak }

    // 버프 선이 기물(층)에 닿는 순간의 맞음 표시 모양·시간 한 곳(사용자 10/26 A안) — 전장 탑(Fx/HitRingFx)과 UI 탑(UI/UiHitRings)이 같은 계산을 쓴다.
    // 좌표 단위는 격자 칸(전장 = 그림 1px · UI = 3px). 프레임은 60fps 기준 실제 시간(배속과 무관):
    // 0~1 칸 안쪽 흰 섬광 · 2~3 효과 색 반투명 + 창틀 바로 바깥 2칸 링(이때 칸이 위로 튀고 효과 숫자가 뜬다) · 4~13 링이 2프레임마다 1칸씩 4칸까지 퍼지며 2칸 → 1칸 → 소멸
    public static class HitRingShape
    {
        private const float Fps = 60f;
        // 선 머리가 닿는 때(알림 기준 초) — 이때가 0프레임
        public const float Start = BuffLinkShape.ArriveTime;
        // 대상 층의 효과 연출(튐 · 받는 효과 표시 · 버프 글자)이 보이는 때(알림 기준 초) — 2프레임. 효과 자체는 이미 걸렸다
        public const float EffectDelay = Start + 2f / Fps;
        public const float End = Start + 14f / Fps;
        // 같은 칸에 이 시간(초) 안에 또 오면 링만
        public const float RepeatGap = 0.15f;

        private const float FlashAlpha = 0.8f;
        private const float TintAlpha = 0.5f;

        public static int Frame(float age) => age < Start ? -1 : Mathf.FloorToInt((age - Start) * Fps);

        // 효과 연출 순간(2프레임)부터 since초 — 칸이 위로 튀는 칸 수: 3프레임 2칸 · 3프레임 1칸 · 그다음 0
        public static int JumpCells(float since)
        {
            if (since < 0f)
                return 0;
            int frame = Mathf.FloorToInt(since * Fps);
            return frame < 3 ? 2 : frame < 6 ? 1 : 0;
        }

        // box = 층 네모(격자 칸 (x0, y0)부터 w×h — 바깥 한 줄이 창틀). age = 알림부터 흐른 초
        public static void Rects(List<HitRect> rects, int x0, int y0, int w, int h, float age, HitRingMode mode)
        {
            rects.Clear();
            int f = Frame(age);
            if (f < 0 || w < 3 || h < 3)
                return;
            if (mode == HitRingMode.Weak)
            {
                // 2~7프레임: 1칸 링이 0 · 1 · 2칸 퍼지며 옅어진다
                if (f >= 2 && f < 8)
                    Ring(rects, x0, y0, w, h, (f - 2) / 2, 1, f < 6 ? 1f : 0.5f);
                return;
            }
            if (mode == HitRingMode.Full && f < 4)
            {
                // 안쪽(창틀 안) — 0~1 흰 섬광, 2~3 효과 색(칸과 같이 튄다)
                bool white = f < 2;
                int jump = white ? 0 : JumpCells((f - 2) / Fps);
                rects.Add(new HitRect { X = x0 + 1, Y = y0 + 1 + jump, W = w - 2, H = h - 2, Alpha = white ? FlashAlpha : TintAlpha, White = white });
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

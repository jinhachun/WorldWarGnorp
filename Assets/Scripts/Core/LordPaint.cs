using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 영주 색 칠하기 — 그림에서 열쇠 색(BattleConfig.LordKeyColor = 그린 파랑) 픽셀만 그 진영 영주의 색(RunState.LordColor)으로 바꾼 스프라이트를 돌려준다.
    // SpriteRenderer.color는 흰 몸까지 물들여서 못 쓴다. 스프라이트를 통째로 바꾸므로 color를 쓰는 피격 번쩍임, sprite를 본뜨는 시체(CorpseFx)·툴팁 유닛 카드가 그대로 같은 색이 된다.
    // 바꾼 그림은 (원본, 색)마다 한 번만 만들어 같이 쓴다 — 유닛이 몇 마리든 텍스처는 그림 수만큼
    public static class LordPaint
    {
        // 열쇠 색으로 치는 채널 오차 — 텍스처를 렌더 텍스처로 옮겨 읽을 때 색 공간 변환으로 1쯤 어긋날 수 있다
        private const int Tolerance = 4;

        private static readonly Dictionary<(Sprite, Color), Sprite> Painted = new Dictionary<(Sprite, Color), Sprite>();

        // 플레이 모드를 새로 켤 때(도메인 리로드를 끈 설정에서도) 지난 플레이의 그림이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Painted.Clear();

        // 열쇠 색이 없는 그림(무기·말)과 열쇠 색 그대로인 진영(나)은 원본을 그대로 돌려준다
        public static Sprite Of(Sprite source, Team team) => Of(source, RunState.LordColor(team));

        // color = 칠할 영주의 색 — 진영이 아니라 영주를 가리켜 칠할 때(목록 화면 등)
        public static Sprite Of(Sprite source, Color color)
        {
            Color32 key = BattleManager.Config.LordKeyColor;
            if (source == null || Near(color, key))
                return source;
            if (Painted.TryGetValue((source, color), out Sprite painted))
                return painted;

            // 임포트한 텍스처는 읽기가 꺼져 있다 — 렌더 텍스처로 한 번 옮겨서 읽는다
            Texture2D from = source.texture;
            Rect rect = source.rect;
            RenderTexture copy = RenderTexture.GetTemporary(from.width, from.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(from, copy);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = copy;
            var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.ReadPixels(rect, 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);

            Color32[] pixels = texture.GetPixels32();
            Color32 to = color;
            bool any = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0 || !Near(pixels[i], key))
                    continue;
                pixels[i] = new Color32(to.r, to.g, to.b, pixels[i].a);
                any = true;
            }
            if (any)
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                painted = Sprite.Create(texture, new Rect(0f, 0f, rect.width, rect.height), source.pivot / rect.size, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                painted.name = source.name;
            }
            else
            {
                Object.Destroy(texture);
                painted = source;
            }
            Painted[(source, color)] = painted;
            return painted;
        }

        private static bool Near(Color32 a, Color32 b)
            => Mathf.Abs(a.r - b.r) <= Tolerance && Mathf.Abs(a.g - b.g) <= Tolerance && Mathf.Abs(a.b - b.b) <= Tolerance;
    }
}

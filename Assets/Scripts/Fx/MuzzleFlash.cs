using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 발사 섬광(Docs/폴리싱_계획.md J2) — 화살 · 돌이 떠나는 자리(궁수 활 끝 · 타워 꼭대기)에 흰 점이 한 프레임 보인다.
    // 그 프레임에 쏜 자리를 모아 두었다가 다음 그리기 한 번에 전부 그리고 비운다. 전부를 메시 하나로(PixelMesh). FxDirector가 만든다
    public class MuzzleFlash : MonoBehaviour
    {
        private const int SortingOrder = 155;   // 유닛 · 화살 위 · 숫자(200) 아래
        private const int MaxQuads = 512;
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private static MuzzleFlash _instance;
        private PixelMesh _mesh;
        private readonly List<Vector2> _points = new List<Vector2>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "MuzzleFlash", material, SortingOrder, MaxQuads);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // Volley가 한 발 내보낼 때마다
        public static void At(Vector2 point)
        {
            if (_instance != null && _instance._points.Count < MaxQuads)
                _instance._points.Add(point);
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            int size = BattleManager.Config != null ? BattleManager.Config.MuzzleFlashPixels : 0;
            if (size > 0)
                foreach (Vector2 p in _points)
                    _mesh.Square(p, size, White);
            _points.Clear();
            _mesh.End();
        }
    }
}

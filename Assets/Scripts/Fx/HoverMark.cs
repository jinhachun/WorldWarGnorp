using UnityEngine;

namespace GnorpWar
{
    // 마우스로 고른 유닛 둘레의 흰 꺾쇠(사용자 10/23 — 유닛 호버 카드 UI/UnitHover가 고른 유닛). 네 모서리에 1px 두께 · 3px 팔, 충돌 박스에서 1px 밖.
    // UnitHover가 고를 때 Show(유닛)/Show(null)을 부르고, 그다음은 유닛 자리를 읽기만 한다 — 죽거나 풀로 돌아가면 저절로 지운다. 메시 하나(PixelMesh). FxDirector가 만든다
    public class HoverMark : MonoBehaviour
    {
        private const int Arm = 3;
        private const int Gap = 1;
        private const int SortingOrder = 170;   // 고도 글자(165) 위 · 숫자(200) 아래

        private static HoverMark _instance;
        private PixelMesh _mesh;
        private Unit _unit;
        private BoxCollider2D _box;
        private int _life;

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "HoverMark", material, SortingOrder, 16);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static void Show(Unit unit)
        {
            if (_instance == null || _instance._unit == unit)
                return;
            _instance._unit = unit;
            _instance._box = unit != null ? unit.GetComponent<BoxCollider2D>() : null;
            _instance._life = unit != null ? unit.Life : 0;
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            if (_unit != null && _unit.isActiveAndEnabled && _unit.IsAlive && _unit.Life == _life && _box != null)
            {
                Bounds b = _box.bounds;
                int left = PixelMesh.ToGrid(b.min.x) - Gap - 1, right = PixelMesh.ToGrid(b.max.x) + Gap;
                int bottom = PixelMesh.ToGrid(b.min.y) - Gap - 1, top = PixelMesh.ToGrid(b.max.y) + Gap;
                // 검은 1px 테두리를 먼저, 흰 꺾쇠를 그 위에 — 흰 유닛이 빽빽한 무리 속에서도 꺾쇠가 읽히게
                Corners(left, right, bottom, top, 1, Color.black);
                Corners(left, right, bottom, top, 0, Color.white);
            }
            _mesh.End();
        }

        // 네 모서리(왼아래 · 왼위 · 오른위 · 오른아래)마다 가로 팔 + 세로 팔 — grow만큼 사방으로 두껍게
        private void Corners(int left, int right, int bottom, int top, int grow, Color32 color)
        {
            int arm = Arm + grow * 2, thick = 1 + grow * 2;
            left -= grow;
            bottom -= grow;
            right -= grow;
            top -= grow;
            _mesh.Rect(left, bottom, arm, thick, color);
            _mesh.Rect(left, bottom, thick, arm, color);
            _mesh.Rect(left, top, arm, thick, color);
            _mesh.Rect(left, top - Arm + 1, thick, arm, color);
            _mesh.Rect(right - Arm + 1, top, arm, thick, color);
            _mesh.Rect(right, top - Arm + 1, thick, arm, color);
            _mesh.Rect(right - Arm + 1, bottom, arm, thick, color);
            _mesh.Rect(right, bottom, thick, arm, color);
        }
    }
}

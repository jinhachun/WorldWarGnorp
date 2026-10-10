using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GnorpWar
{
    // 전투 중 전장 탑 층(TowerFloor)에 마우스를 올리면 그 층 기물의 설명 창(BuildingTooltip) — 칸 UI에 올렸을 때와 같은 내용. 양 진영.
    // 상점에선 판이 전장을 가리므로 하지 않는다. UI 위에 마우스가 있으면 UI가 먼저다.
    // 이 오브젝트(Canvas 아래, 그림 없음)를 층의 화면 자리에 겹쳐 놓고 설명 창의 기준으로 쓴다
    [RequireComponent(typeof(RectTransform))]
    public class TowerHover : MonoBehaviour
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private readonly Collider2D[] _colliders = new Collider2D[8];
        private PointerEventData _pointerData;

        private void Update()
        {
            if (!Find(out TowerFloor floor, out BoardRunner.Slot running))
            {
                BuildingTooltip.Hide(this);
                return;
            }
            Cover(floor.GetComponent<Collider2D>().bounds);
            BuildingTooltip.ShowOwned(this, running.Building, running, floor.Team == Team.Enemy);
        }

        private bool Find(out TowerFloor floor, out BoardRunner.Slot running)
        {
            floor = null;
            running = null;
            Pointer pointer = Pointer.current;
            Camera cam = Camera.main;
            if (!BattleManager.Fighting || pointer == null || cam == null || OverUi(pointer.position.ReadValue()))
                return false;

            Vector2 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());
            int count = Physics2D.OverlapPoint(world, ContactFilter2D.noFilter, _colliders);
            for (int i = 0; i < count && floor == null; i++)
                floor = _colliders[i].GetComponent<TowerFloor>();
            if (floor == null)
                return false;

            BoardRunner board = BoardRunner.For(floor.Team);
            if (board == null || floor.Index >= board.Slots.Length)
                return false;
            running = board.Slots[floor.Index];
            return !OwnedBuilding.IsEmpty(running.Building);
        }

        private bool OverUi(Vector2 position)
        {
            if (EventSystem.current == null)
                return false;
            _pointerData ??= new PointerEventData(EventSystem.current);
            _pointerData.position = position;
            _hits.Clear();
            EventSystem.current.RaycastAll(_pointerData, _hits);
            return _hits.Count > 0;
        }

        // 층의 화면 자리에 겹친다 — Overlay 캔버스라 월드 좌표 = 화면 픽셀
        private void Cover(Bounds bounds)
        {
            Camera cam = Camera.main;
            Vector2 min = cam.WorldToScreenPoint(bounds.min);
            Vector2 max = cam.WorldToScreenPoint(bounds.max);
            var rect = (RectTransform)transform;
            rect.pivot = Vector2.zero;
            rect.position = min;
            rect.sizeDelta = (max - min) / rect.lossyScale.x;
        }
    }
}

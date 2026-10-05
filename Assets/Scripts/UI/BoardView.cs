using UnityEngine;

namespace GnorpWar
{
    // 건물 칸 한 줄 — 아군 필드 · 적 필드 · 보관함. 틀 칸(꺼져 있음)을 칸 수만큼 복제해 가로로 늘어놓는다
    public class BoardView : MonoBehaviour
    {
        public enum Source { AllyField, EnemyField, Storage }

        [SerializeField] private Source _source;
        [SerializeField] private SlotView _template;
        [Tooltip("칸 간격(px) — 음수면 왼쪽으로 늘어선다")]
        [SerializeField] private float _step = 130f;
        [SerializeField] private ShopPanel _shop;
        [Tooltip("전투 중에만 보인다 — 상점에선 가운데 필드 줄이 대신 보여 준다")]
        [SerializeField] private bool _battleOnly;

        private SlotView[] _views;

        private void Start()
        {
            int count = _source == Source.Storage ? RunState.Storage.Length : Runner.Slots.Length;
            _views = new SlotView[count];
            for (int i = 0; i < count; i++)
            {
                SlotView view = Instantiate(_template, _template.transform.parent);
                var rect = (RectTransform)view.transform;
                rect.anchoredPosition = ((RectTransform)_template.transform).anchoredPosition + new Vector2(i * _step, 0f);
                int index = i;
                view.Bind(_shop, _source, index);
                view.Button.onClick.AddListener(() => _shop.OnSlotClicked(_source, index));
                view.gameObject.SetActive(true);
                _views[i] = view;
            }
        }

        private BoardRunner Runner => BoardRunner.For(_source == Source.EnemyField ? Team.Enemy : Team.Ally);

        private void Update()
        {
            bool visible = !_battleOnly || BattleManager.Fighting;
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i].gameObject.activeSelf != visible)
                    _views[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;

                OwnedBuilding building;
                BoardRunner.Slot running = null;
                // 아군 필드는 상점에서 바뀌므로 전투 전엔 RunState를, 전투 중엔 돌고 있는 보드를 본다
                if (_source == Source.Storage)
                    building = RunState.Storage[i];
                else if (_source == Source.AllyField && !BattleManager.Fighting)
                    building = RunState.Field[i];
                else
                {
                    building = Runner.Slots[i].Building;
                    if (BattleManager.Fighting)
                        running = Runner.Slots[i];
                }
                _views[i].Show(building, running, _shop.IsSelected(_source, i), _shop.IsDragging(_source, i));
            }
        }
    }
}

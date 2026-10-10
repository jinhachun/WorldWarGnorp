using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 중 화면 구석의 맵 이름(RunState.Map) — 마우스를 올리면 설명 창(BuildingTooltip)에 맵 설명.
    // 맵과 아이콘의 짝도 여기 한 곳에 둔다(상점 맵 상자도 IconFor로 읽는다)
    [RequireComponent(typeof(Text))]
    public class MapLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [System.Serializable]
        private struct MapIcon
        {
            public MapDefinition Map;
            public Sprite Icon;
        }

        private static readonly UnitDefinition[] NoUnits = new UnitDefinition[0];
        private static MapLabel _instance;

        [Tooltip("맵별 아이콘 — 짝이 없는 맵은 아이콘 없이 이름만")]
        [SerializeField] private MapIcon[] _icons = new MapIcon[0];
        [Tooltip("이름 왼쪽 아이콘")]
        [SerializeField] private Image _icon;

        private Text _text;
        private bool _hovered;

        private void Awake()
        {
            _instance = this;
            _text = GetComponent<Text>();
        }

        public static Sprite IconFor(MapDefinition map)
        {
            if (_instance == null || map == null)
                return null;
            foreach (MapIcon pair in _instance._icons)
                if (pair.Map == map)
                    return pair.Icon;
            return null;
        }

        private void Update()
        {
            MapDefinition map = BattleManager.Fighting ? RunState.Map : null;
            _text.text = map == null ? "" : map.DisplayName;
            Sprite icon = IconFor(map);
            _icon.enabled = icon != null;
            _icon.sprite = icon;
            // 칸 폭을 글 폭에 맞춘다 — 설명 창이 칸 오른쪽 끝에 붙어 뜨므로
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(_text.preferredWidth, rect.sizeDelta.y);
            if (map == null || !_hovered)
                BuildingTooltip.Hide(this);
            else
                BuildingTooltip.Show(this, map.DisplayName, BuildingTooltip.Highlight(map.Description), NoUnits, null);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
        }
    }
}

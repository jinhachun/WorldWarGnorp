using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 중 화면 구석의 맵 이름(RunState.Map) — 마우스를 올리면 설명 창(BuildingTooltip)에 맵 설명.
    // 맵의 아이콘은 맵 에셋에 있다(MapDefinition.Icon)
    [RequireComponent(typeof(Text))]
    public class MapLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly UnitDefinition[] NoUnits = new UnitDefinition[0];

        [Tooltip("이름 왼쪽 아이콘")]
        [SerializeField] private Image _icon;

        private Text _text;
        private bool _hovered;

        private void Awake() => _text = GetComponent<Text>();

        private void Update()
        {
            MapDefinition map = BattleManager.Fighting ? RunState.Map : null;
            _text.text = map == null ? "" : map.DisplayName;
            Sprite icon = map != null ? map.Icon : null;
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

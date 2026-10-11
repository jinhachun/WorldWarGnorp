using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 패널의 아이콘 한 개 — 마우스를 올리면 설명 창(BuildingTooltip)에 이름과 효과.
    // 지역 업그레이드(가진 업그레이드 줄 · 고른 땅의 강화)와 고른 땅의 지형이 쓴다.
    // 업그레이드를 여러 개 가졌으면 효과 글의 숫자는 합쳐진 실제 값이고, 회색 줄로 「N개 보유」
    [RequireComponent(typeof(Image))]
    public class UpgradeIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly UnitDefinition[] NoUnits = new UnitDefinition[0];

        private string _title;
        private string _body;

        public void Set(RegionUpgradeDefinition upgrade, int count)
        {
            string body = BuildingTooltip.Highlight(upgrade.Describe(count));
            if (count > 1)
                body += $"\n<color={BuildingTooltip.NoteColor}>{count}개 보유</color>";
            Set(upgrade.Icon, upgrade.DisplayName, body);
        }

        // 그림은 원본 크기 그대로
        public void Set(Sprite icon, string title, string body)
        {
            _title = title;
            _body = body;
            var image = GetComponent<Image>();
            image.sprite = icon;
            image.SetNativeSize();
        }

        public void OnPointerEnter(PointerEventData eventData) => BuildingTooltip.Show(this, _title, _body, NoUnits, null);

        public void OnPointerExit(PointerEventData eventData) => BuildingTooltip.Hide(this);

        private void OnDisable() => BuildingTooltip.Hide(this);
    }
}

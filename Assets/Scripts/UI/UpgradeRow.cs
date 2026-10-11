using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 영주가 가진 지역 업그레이드 아이콘 줄 — 종류마다 아이콘 하나(종류 순서 고정, 원본 크기), 2개 이상이면 그 옆에 「×N」.
    // 아이콘에 마우스를 올리면 합쳐진 효과가 뜬다(UI/UpgradeIcon). 한 줄에 columns칸, 넘치면 아랫줄.
    // 왼쪽 패널(UI/WorldSidePanel)과 전투 화면의 본진 체력바 아래(UI/BaseUpgradeRow)가 같이 쓴다
    public class UpgradeRow
    {
        // 아이콘 한 변 · 아이콘과 「×N」 사이 · 줄 높이(px)
        private const float IconSize = 32f;
        private const float LabelGap = 4f;
        private const float CellHeight = 36f;

        private readonly UpgradeIcon[] _icons;
        private readonly Text[] _counts;
        private readonly int _columns;

        // 줄의 기준점 — 왼쪽 위(fromRight면 오른쪽 위). 자리는 부르는 쪽이 놓는다
        public readonly RectTransform Root;

        // labelTemplate = 「×N」 글의 본(도트 폰트 글 하나 — 같은 폰트 · 크기로 본뜬다). fromRight = 기준점에서 왼쪽으로 늘어놓는다
        public UpgradeRow(RectTransform parent, Text labelTemplate, float cellWidth, int columns, bool fromRight)
        {
            _columns = columns;
            Root = (RectTransform)new GameObject("Upgrades", typeof(RectTransform)).transform;
            Root.SetParent(parent, false);
            Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = Vector2.zero;
            int kinds = System.Enum.GetValues(typeof(RegionUpgradeKind)).Length;
            _icons = new UpgradeIcon[kinds];
            _counts = new Text[kinds];
            for (int i = 0; i < kinds; i++)
            {
                int column = i % columns;
                var cell = new Vector2(fromRight ? -(column + 1) * cellWidth : column * cellWidth, -(i / columns) * CellHeight);
                _icons[i] = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(UpgradeIcon)).GetComponent<UpgradeIcon>();
                Place((RectTransform)_icons[i].transform, cell);
                _icons[i].GetComponent<Image>().preserveAspect = true;

                Text count = _counts[i] = Object.Instantiate(labelTemplate, Root);
                count.name = "Count";
                count.enabled = true;
                count.alignment = TextAnchor.MiddleLeft;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.raycastTarget = false;
                Place(count.rectTransform, cell + new Vector2(IconSize + LabelGap, 0f));
                count.rectTransform.sizeDelta = new Vector2(0f, IconSize);
            }
        }

        private void Place(RectTransform rect, Vector2 position)
        {
            rect.SetParent(Root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
        }

        // 지금 보이는 칸들의 폭(px) — 첫 아이콘의 왼쪽부터 마지막 칸의 아이콘(「×N」이 있으면 그 글)의 오른쪽까지. 한 줄일 때만 뜻이 있다
        public float Width { get; private set; }

        // lord가 가진 것을 보여 주고 줄의 높이를 돌려준다(하나도 없으면 0)
        public float Show(WorldRun world, Lord lord)
            => Show(world.Map, kind => world.UpgradeCount(lord, kind), "{0}개 보유");

        // 종류마다 countOf개를 보여 주고 줄의 높이를 돌려준다(하나도 없으면 0). note = 툴팁의 회색 줄({0} = 개수)
        public float Show(WorldMapDefinition map, System.Func<RegionUpgradeKind, int> countOf, string note)
        {
            int cells = 0;
            Width = 0f;
            foreach (RegionUpgradeKind kind in System.Enum.GetValues(typeof(RegionUpgradeKind)))
            {
                int count = countOf(kind);
                if (count == 0)
                    continue;
                _icons[cells].gameObject.SetActive(true);
                _icons[cells].Set(map.UpgradeOfKind(kind), count, note);
                _counts[cells].gameObject.SetActive(true);
                _counts[cells].text = count > 1 ? $"×<color={BuildingTooltip.NumberColor}>{count}</color>" : "";
                Width = Mathf.Abs(_counts[cells].rectTransform.anchoredPosition.x) + (count > 1 ? _counts[cells].preferredWidth : -LabelGap);
                cells++;
            }
            for (int i = cells; i < _icons.Length; i++)
            {
                _icons[i].gameObject.SetActive(false);
                _counts[i].gameObject.SetActive(false);
            }
            return cells == 0 ? 0f : ((cells - 1) / _columns + 1) * CellHeight - (CellHeight - IconSize);
        }
    }
}

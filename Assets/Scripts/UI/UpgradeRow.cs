using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 영주가 가진 합치기 보상 업그레이드 아이콘 줄(RunState.UpgradesOf) — 종류마다 아이콘 하나(처음 얻은 순서, 원본 크기)와 그 옆에 「×N」(사용자 10/12: 1개일 때도 「×1」 — 비어 보이지 않게).
    // 아이콘은 등급 색 외곽선(그 업그레이드를 얻은 것 중 가장 높은 등급 — 기물 등급 테두리와 같은 색, 사용자 10/13) 안에 든다.
    // 아이콘에 마우스를 올리면 이름과 합쳐진 효과가 뜬다(UI/UpgradeIcon). 한 줄에 columns칸, 넘치면 아랫줄.
    // 왼쪽 패널 · 위 줄(UI/WorldSidePanel)과 전투 화면의 본진 체력바 아래(UI/BaseUpgradeRow)가 같이 쓴다
    public class UpgradeRow
    {
        // 아이콘 한 변 · 외곽선 두께 · 아이콘과 「×N」 사이 · 줄 간격(px)
        private const float IconSize = 32f;
        private const float Frame = 2f;
        private const float LabelGap = 2f;
        // 「×N」 글자 크기 — 도트 폰트의 가장 작은 단계(12), 칸 아래쪽에 붙인다(사용자 10/14: 개수 표시는 작게)
        public const int CountSize = 12;
        private const float CellHeight = 40f;
        // 외곽선까지 친 칸의 한 변(px) — 한 줄의 높이
        public const float BoxSize = IconSize + Frame * 2f;

        private readonly List<Image> _frames = new List<Image>();
        private readonly List<UpgradeIcon> _icons = new List<UpgradeIcon>();
        private readonly List<Text> _counts = new List<Text>();
        private readonly Text _labelTemplate;
        private readonly float _cellWidth;
        private readonly int _columns;
        private readonly bool _fromRight;

        // 줄의 기준점 — 왼쪽 위(fromRight면 오른쪽 위). 자리는 부르는 쪽이 놓는다
        public readonly RectTransform Root;

        // labelTemplate = 「×N」 글의 본(도트 폰트 글 하나 — 같은 폰트 · 크기로 본뜬다). fromRight = 기준점에서 왼쪽으로 늘어놓는다
        public UpgradeRow(RectTransform parent, Text labelTemplate, float cellWidth, int columns, bool fromRight)
        {
            _labelTemplate = labelTemplate;
            _cellWidth = cellWidth;
            _columns = columns;
            _fromRight = fromRight;
            Root = (RectTransform)new GameObject("Upgrades", typeof(RectTransform)).transform;
            Root.SetParent(parent, false);
            Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = Vector2.zero;
        }

        // i번째 칸을 만든다 — 가진 종류가 늘 때마다 하나씩. 외곽선 = 그림 없는 등급 색 판 위에 검은 판을 얹은 것(늘려 쓰는 그림이 없다)
        private void AddCell()
        {
            int i = _icons.Count, column = i % _columns;
            var cell = new Vector2(_fromRight ? -(column + 1) * _cellWidth : column * _cellWidth, -(i / _columns) * CellHeight);
            Image frame = NewImage("Frame", Root, cell, BoxSize);
            _frames.Add(frame);
            NewImage("Fill", frame.rectTransform, new Vector2(Frame, -Frame), IconSize).color = Color.black;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(UpgradeIcon)).GetComponent<UpgradeIcon>();
            Place((RectTransform)icon.transform, frame.rectTransform, new Vector2(Frame, -Frame));
            icon.GetComponent<Image>().preserveAspect = true;
            _icons.Add(icon);

            Text count = Object.Instantiate(_labelTemplate, Root);
            count.name = "Count";
            count.enabled = true;
            count.fontSize = CountSize;
            count.alignment = TextAnchor.LowerLeft;
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            count.raycastTarget = false;
            Place(count.rectTransform, Root, cell + new Vector2(BoxSize + LabelGap, 0f));
            count.rectTransform.sizeDelta = new Vector2(0f, BoxSize);
            _counts.Add(count);
        }

        private static Image NewImage(string name, RectTransform parent, Vector2 position, float size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            Place(image.rectTransform, parent, position);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        private static void Place(RectTransform rect, RectTransform parent, Vector2 position)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
        }

        // 지금 보이는 칸들의 폭(px) — 첫 칸의 왼쪽부터 마지막 칸의 「×N」 글의 오른쪽까지. 한 줄일 때만 뜻이 있다
        public float Width { get; private set; }

        // 가진 것(holdings)을 보여 주고 줄의 높이를 돌려준다(하나도 없으면 0)
        public float Show(List<UpgradeHolding> holdings)
        {
            int cells = holdings.Count;
            while (_icons.Count < cells)
                AddCell();
            Width = 0f;
            for (int i = 0; i < _icons.Count; i++)
            {
                bool shown = i < cells;
                _frames[i].gameObject.SetActive(shown);
                _counts[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                _frames[i].color = holdings[i].Rarity.FrameColor();
                _icons[i].Set(holdings[i]);
                _counts[i].text = $"×<color={BuildingTooltip.NumberColor}>{holdings[i].Count}</color>";
                Width = Mathf.Abs(_counts[i].rectTransform.anchoredPosition.x) + _counts[i].preferredWidth;
            }
            return cells == 0 ? 0f : ((cells - 1) / _columns + 1) * CellHeight - (CellHeight - BoxSize);
        }
    }
}

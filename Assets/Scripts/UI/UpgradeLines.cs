using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 설명 창(UI/BuildingTooltip)의 판 한 장에 들어가는 「걸려 있는 업그레이드」 줄(사용자 10/14) — 기물 · 유닛에 걸린 업그레이드(RunState.UpgradesOn)를
    // 아이콘만 나란히: 등급 색 테두리 칸 안의 아이콘(가진 업그레이드 줄 UI/UpgradeRow의 칸과 같은 모양 · 원본 크기), 같은 것을 여러 번 얻었으면 칸 옆에 「×N」.
    // 폭을 넘으면 아랫줄. 무엇인지는 화면 위쪽 가진 업그레이드 줄에 마우스를 올려 본다(사용자 10/14 — 판의 효과 글이 묻히지 않게)
    public class UpgradeLines
    {
        // 아이콘 한 변 · 외곽선 두께 · 칸 사이 · 칸과 「×N」 사이(가진 업그레이드 줄과 같다) · 줄 사이(px)
        private const float IconSize = 32f;
        private const float Frame = 2f;
        private const float Box = IconSize + Frame * 2f;
        private const float IconsGap = 4f;
        private const float CountGap = 2f;
        private const float RowGap = 4f;
        // 「×N」 글자 크기 · 자리 — 가진 업그레이드 줄과 같다(칸 아래쪽에 붙인다)
        private const int CountSize = UpgradeRow.CountSize;

        private class Line
        {
            public RectTransform Root;
            public Image Frame, Icon;
            public Text Count;
        }

        private readonly RectTransform _root;
        private readonly float _width;
        private readonly Font _dot;
        private readonly List<Line> _lines = new List<Line>();

        // panel = 줄이 들어갈 판(위쪽 기준), side = 판 왼쪽 여백, width = 줄의 폭
        public UpgradeLines(RectTransform panel, float side, float width, Font dot)
        {
            _width = width;
            _dot = dot;
            _root = NewRect("Upgrades", panel);
            _root.anchoredPosition = new Vector2(side, 0f);
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static Image NewImage(string name, RectTransform parent, float position, float size)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchoredPosition = new Vector2(position, -position);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Line NewLine()
        {
            var line = new Line { Root = NewRect("Line", _root) };
            // 외곽선 = 그림 없는 등급 색 판 위에 검은 판을 얹은 것(늘려 쓰는 그림이 없다)
            line.Frame = NewImage("Frame", line.Root, 0f, Box);
            NewImage("Fill", line.Root, Frame, IconSize).color = Color.black;
            line.Icon = NewImage("Icon", line.Root, Frame, IconSize);
            line.Icon.preserveAspect = true;
            line.Count = NewRect("Count", line.Root).gameObject.AddComponent<Text>();
            line.Count.font = _dot;
            line.Count.fontSize = CountSize;
            line.Count.color = Color.white;
            line.Count.alignment = TextAnchor.LowerLeft;
            line.Count.raycastTarget = false;
            line.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.Count.verticalOverflow = VerticalWrapMode.Overflow;
            line.Count.rectTransform.anchoredPosition = new Vector2(Box + CountGap, 0f);
            return line;
        }

        // 판 위쪽에서 y만큼 내려 놓고(y는 음수) 줄의 높이를 돌려준다(걸린 것이 없으면 0)
        public float Show(List<UpgradeHolding> upgrades, float y)
        {
            int count = upgrades != null ? upgrades.Count : 0;
            while (_lines.Count < count)
                _lines.Add(NewLine());
            _root.anchoredPosition = new Vector2(_root.anchoredPosition.x, y);
            // 다음 칸이 놓일 자리 — 옆으로 놓다가 폭을 넘으면 아랫줄
            float x = 0f, top = 0f;
            for (int i = 0; i < _lines.Count; i++)
            {
                Line line = _lines[i];
                bool shown = i < count;
                if (line.Root.gameObject.activeSelf != shown)
                    line.Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                UpgradeHolding upgrade = upgrades[i];
                line.Frame.color = upgrade.Rarity.FrameColor();
                line.Icon.sprite = upgrade.Definition.Icon;
                line.Icon.SetNativeSize();
                float width = Box;
                line.Count.enabled = upgrade.Count > 1;
                if (line.Count.enabled)
                {
                    line.Count.text = $"×<color={BuildingTooltip.NumberColor}>{upgrade.Count}</color>";
                    float countWidth = line.Count.preferredWidth;
                    line.Count.rectTransform.sizeDelta = new Vector2(countWidth, Box);
                    width = Box + CountGap + countWidth;
                }
                if (x > 0f && x + width > _width)
                {
                    x = 0f;
                    top += Box + RowGap;
                }
                line.Root.anchoredPosition = new Vector2(x, -top);
                x += width + IconsGap;
            }
            return count == 0 ? 0f : top + Box;
        }
    }
}

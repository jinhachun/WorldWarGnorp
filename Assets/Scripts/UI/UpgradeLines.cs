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
        // 칸 한 변(외곽선까지) · 칸 사이 · 칸과 「×N」 사이(칸 프리팹 안 자리와 같다) · 줄 사이(px)
        private const float Box = UpgradeRow.BoxSize;
        private const float IconsGap = 4f;
        private const float CountGap = 2f;
        private const float RowGap = 4f;

        private class Line
        {
            public RectTransform Root;
            public Image Frame, Icon;
            public Text Count;
        }

        private readonly RectTransform _root;
        private float _width;
        // 판 폭이 바뀌면(설명 창이 상점 자리에 설 때 — BuildingTooltip) 줄의 폭도 바꾼다
        public float Width { set => _width = value; }
        private readonly Image _cell;
        private readonly List<Line> _lines = new List<Line>();

        // panel = 줄이 들어갈 판(위쪽 기준), side = 판 왼쪽 여백, width = 줄의 폭, cell = 칸 하나의 프리팹(Parts/UI_UpgradeCell — 올림 설명 없음)
        public UpgradeLines(RectTransform panel, float side, float width, Image cell)
        {
            _width = width;
            _cell = cell;
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

        // 칸 하나 = 칸 프리팹 하나(외곽선 = 그림 없는 등급 색 판 위에 검은 판을 얹은 것 — 늘려 쓰는 그림이 없다). 칸 자체가 줄의 뿌리다
        private Line NewLine()
        {
            Image frame = Object.Instantiate(_cell, _root);
            frame.name = "Frame";
            return new Line
            {
                Root = frame.rectTransform,
                Frame = frame,
                Icon = frame.transform.Find("Icon").GetComponent<Image>(),
                Count = frame.transform.Find("Count").GetComponent<Text>(),
            };
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

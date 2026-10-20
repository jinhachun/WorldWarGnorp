using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 설명 글 줄 안 아이콘(사용자 10/21) — 스탯 낱말(공격력 · 체력 · 공격속도 · 쿨다운 · 골드 · 「N기」) 앞에 작은 아이콘을 끼운다.
    // uGUI Text에는 줄 안 그림이 없으므로: ① 낱말 앞에 보이지 않는 한글 한 글자(자리 · 그 낱말과 붙어 줄이 안 갈린다)를 넣고
    // ② Text의 글자 위치(TextGenerator — 피벗 기준)를 읽어 ③ 그 자리 가운데에 Image(원본 크기 · §5-1)를 얹는다.
    // 낱말 → 아이콘 표는 여기 한 곳(Words). 그림은 WorldConfig 「줄 안 아이콘」 — 칸이 비면 그 낱말엔 아이콘도 자리도 넣지 않는다
    [RequireComponent(typeof(Text))]
    public class InlineIcons : MonoBehaviour
    {
        // 직렬화되는 순서(WorldConfig 칸) — 맨 뒤에만 더할 것
        public enum Kind { Attack, Hp, AttackSpeed, Cooldown, Gold, Units }

        // 자리 글자 — 투명 한글 한 자(본문 글꼴의 한 칸 폭 ≈ 아이콘 16px + 양옆 여백). 숫자 노랑 칠하기(BuildingTooltip.ColorNumbers)는 이미 끝난 글에 넣는다
        private const string Slot = "<color=#00000000>ㅁ</color>";
        private const string SlotChar = "ㅁ";

        // 낱말 → 아이콘 — 위에서부터 먼저 맞는 것(공격속도가 공격력보다 · 최대 체력이 체력보다 먼저). 숫자는 노랑 색 태그에 싸여 있을 수 있다
        private const string Num = @"(?:<color=#[0-9A-Fa-f]{6}>)?\d+(?:\.\d+)?(?:</color>)?";
        private static readonly (string Pattern, Kind Kind)[] Words =
        {
            ("공격속도", Kind.AttackSpeed),
            ("공격력", Kind.Attack),
            ("최대 체력", Kind.Hp),
            ("체력", Kind.Hp),
            ("쿨다운", Kind.Cooldown),
            (Num + "초마다", Kind.Cooldown),
            ("골드", Kind.Gold),
            (Num + "기(?!물)", Kind.Units),
        };
        private static readonly Regex Any = new Regex(string.Join("|", System.Array.ConvertAll(Words, w => "(" + w.Pattern + ")")));

        private Text _text;
        private readonly List<(int Index, Kind Kind)> _marks = new List<(int, Kind)>();
        private readonly List<Image> _pool = new List<Image>();
        private readonly TextGenerator _generator = new TextGenerator();

        // 판이 꺼진 채 붙으면 Awake가 늦게 돈다 — 쓸 때 찾는다
        private Text Body => _text != null ? _text : _text = GetComponent<Text>();

        private static Sprite IconOf(Kind kind) => RunState.Config != null && RunState.Config.World != null ? RunState.Config.World.InlineIcon(kind) : null;

        // 글을 넣는다(자리 글자를 끼워서) — 높이를 재기(preferredHeight) 전에 부른다. 아이콘 자리는 Place가 정한다
        public void SetText(string text)
        {
            _marks.Clear();
            if (string.IsNullOrEmpty(text))
            {
                Body.text = text;
                return;
            }
            var result = new System.Text.StringBuilder(text.Length + 64);
            int last = 0;
            foreach (Match m in Any.Matches(text))
            {
                Kind kind = KindOf(m);
                result.Append(text, last, m.Index - last);
                if (IconOf(kind) != null)
                {
                    int at = result.Length + Slot.IndexOf(SlotChar, System.StringComparison.Ordinal);
                    result.Append(Slot);
                    _marks.Add((at, kind));
                }
                result.Append(m.Value);
                last = m.Index + m.Length;
            }
            result.Append(text, last, text.Length - last);
            Body.text = result.ToString();
        }

        private static Kind KindOf(Match m)
        {
            for (int g = 1; g < m.Groups.Count; g++)
                if (m.Groups[g].Success)
                    return Words[g - 1].Kind;
            return Kind.Attack;
        }

        // 글 상자의 크기가 정해진 뒤(높이 맞춤 뒤) — 자리 글자마다 아이콘을 그 칸 가운데에
        public void Place()
        {
            Vector2 size = Body.rectTransform.rect.size;
            _generator.Populate(_text.text, _text.GetGenerationSettings(size));
            IList<UICharInfo> chars = _generator.characters;
            IList<UILineInfo> lines = _generator.lines;
            float ppu = _text.pixelsPerUnit;
            int used = 0;
            foreach ((int index, Kind kind) in _marks)
            {
                if (index >= chars.Count || lines.Count == 0)
                    continue;
                UICharInfo c = chars[index];
                if (c.charWidth <= 0f)
                    continue;   // 잘려 안 보이는 글자
                UILineInfo line = lines[0];
                foreach (UILineInfo l in lines)
                    if (l.startCharIdx <= index)
                        line = l;
                Image icon = Icon(used++);
                icon.sprite = IconOf(kind);
                icon.SetNativeSize();
                Vector2 iconSize = icon.rectTransform.sizeDelta;
                float x = (c.cursorPos.x + c.charWidth * 0.5f) / ppu - iconSize.x * 0.5f;
                float y = (c.cursorPos.y - line.height * 0.5f) / ppu;   // cursorPos.y = 그 줄의 윗변
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = _text.rectTransform.pivot;   // 글자 좌표는 Text의 피벗 기준
                icon.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
                icon.gameObject.SetActive(true);
            }
            for (int i = used; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        private Image Icon(int i)
        {
            while (_pool.Count <= i)
            {
                var image = new GameObject("InlineIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(_text.rectTransform, false);
                image.rectTransform.pivot = new Vector2(0f, 0.5f);
                image.preserveAspect = true;
                image.raycastTarget = false;
                _pool.Add(image);
            }
            return _pool[i];
        }
    }
}

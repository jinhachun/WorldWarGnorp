using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GnorpWar
{
    // 국가 고르기 카드(사용자 10/15) — 국가 하나(이름 · 대표 유닛 그림 · 설명)를 좌우 화살표(키보드 ←/→)로 넘기고 「시작」을 누르면 고른 국가를 알린다.
    // 보여 주기만 한다 — 국가를 정하고 첫 턴을 여는 건 WorldManager가 Chosen을 받아서 한다. 이 오브젝트 = 국가 고르기 판(켜고 끄는 단위)
    public class NationCard : MonoBehaviour
    {
        // 대표 유닛 그림의 가장 긴 변이 이 크기(px)를 넘지 않는 가장 큰 정수배로 키운다
        private const float ArtMax = 256f;

        [SerializeField] private Text _name;
        [Tooltip("대표 유닛을 정수배로 키워 그리는 자리(가운데 기준) — 부품마다 Image를 여기 만든다")]
        [SerializeField] private RectTransform _art;
        [Tooltip("설명 글 — 깔끔 폰트")]
        [SerializeField] private Text _body;
        [SerializeField] private Button _prev;
        [SerializeField] private Button _next;
        [SerializeField] private Button _start;
        [Tooltip("판의 종족 테두리 — 보고 있는 국가의 것(사용자 10/15)")]
        [SerializeField] private NationFrame _frame;

        // 「시작」을 눌렀다 — 고른 국가. 판은 스스로 닫힌다
        public event System.Action<BuildingNation> Chosen;

        private BuildingNation[] _choices;
        private WorldConfig _world;
        // 지금 보이는 국가(_choices의 칸)
        private int _index;

        private void Awake()
        {
            _prev.onClick.AddListener(() => Turn(-1));
            _next.onClick.AddListener(() => Turn(1));
            _start.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                Chosen?.Invoke(_choices[_index]);
            });
        }

        // choices = 고를 수 있는 국가(카드 순서) · world = 이름 밖의 카드 내용(대표 유닛 · 설명)
        public void Open(BuildingNation[] choices, WorldConfig world)
        {
            _choices = choices;
            _world = world;
            _index = 0;
            Show();
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                Turn(-1);
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                Turn(1);
        }

        private void Turn(int step)
        {
            int count = _choices.Length;
            _index = ((_index + step) % count + count) % count;
            Show();
        }

        private void Show()
        {
            BuildingNation nation = _choices[_index];
            _name.text = nation.DisplayName();
            _frame.ShowNation(nation);
            _body.text = _world.NationDescription(nation);
            for (int i = _art.childCount - 1; i >= 0; i--)
                Destroy(_art.GetChild(i).gameObject);
            UnitDefinition unit = _world.NationUnit(nation);
            if (unit != null)
                DrawUnit(unit.PrefabFor(Team.Ally), _art);
        }

        // 유닛 프리팹의 그림 부품(몸 · 말 · 무기)을 같은 배치로 UI에 — 그림 1px = 화면 k px(정수배). 진영 색은 내 색(LordPaint), 그리는 순서는 정렬 순서대로
        private static void DrawUnit(Unit prefab, RectTransform into)
        {
            var parts = new List<SpriteRenderer>();
            foreach (SpriteRenderer part in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (part.sprite != null && part.enabled && part.gameObject.activeSelf)
                    parts.Add(part);
            parts.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            // 부품들을 그림 px로 — 자리(프리팹 안 · px) · 크기(px)
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (SpriteRenderer part in parts)
            {
                Sprite sprite = part.sprite;
                Vector2 scale = part.transform.localScale;
                Vector2 at = (Vector2)part.transform.localPosition * sprite.pixelsPerUnit;
                Vector2 lo = at - Vector2.Scale(sprite.pivot, scale), hi = lo + Vector2.Scale(sprite.rect.size, scale);
                min = Vector2.Min(min, lo);
                max = Vector2.Max(max, hi);
            }
            if (parts.Count == 0)
                return;
            Vector2 size = max - min, center = (min + max) * 0.5f;
            int k = Mathf.Max(1, Mathf.FloorToInt(ArtMax / Mathf.Max(size.x, size.y)));
            float canvasPpu = into.GetComponentInParent<Canvas>(true).referencePixelsPerUnit;
            foreach (SpriteRenderer part in parts)
            {
                Sprite sprite = part.sprite;
                var image = new GameObject(part.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(into, false);
                image.sprite = LordPaint.Of(sprite, Team.Ally);
                image.color = part.color;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.SetNativeSize();
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                // 원본 크기(SetNativeSize)에서 정수배 k로 — 캔버스 기준은 그림 1px = 100 ÷ PPU
                Vector2 scale = part.transform.localScale;
                float toK = k * sprite.pixelsPerUnit / canvasPpu;
                image.rectTransform.localScale = new Vector3(scale.x * toK, scale.y * toK, 1f);
                image.rectTransform.anchoredPosition = ((Vector2)part.transform.localPosition * sprite.pixelsPerUnit - center) * k;
            }
        }
    }
}

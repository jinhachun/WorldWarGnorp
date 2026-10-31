using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GnorpWar
{
    // 국가 고르기 화면(사용자 10/21 B안 — 목록 + 무대). 골격은 월드 화면 그대로(왼쪽 기둥 · 탑 기둥 · 지도 자리)라 고른 뒤 지도로 넘어갈 때 판이 안 바뀐다.
    // 왼쪽 기둥 = 국가 목록(UI/NationRow) · 탑 기둥 = 그 국가 재질의 탑 · 지도 자리 = 무대(이름 · 한 줄 · 먼 산 · 랜드마크 · 유닛 줄 · 바닥선 아래 빌드 갈래).
    // 보여 주기만 한다 — 국가를 정하고 첫 턴을 여는 건 WorldManager가 Chosen을 받아서 한다. 이 오브젝트 = 국가 고르기 판(켜고 끄는 단위)
    public class NationCard : MonoBehaviour
    {
        // 무대 그림의 배율(그림 1px = 화면 k px, 정수배) — 유닛 · 랜드마크 · 먼 산
        private const int UnitScale = 10;
        private const int LandmarkScale = 4;
        private const int BackdropScale = 2;
        private const float UnitGap = 20f;
        // 목록 줄 간격 · 줄 아래 안내 글까지
        private const float RowStep = 116f;
        private const float HintGap = 32f;
        // 먼 산 색 — 검은 바탕에 겨우 보이게
        private static readonly Color BackdropTint = new Color(0.14f, 0.14f, 0.14f);
        // 움직임(실제 시간 · 스타일 가이드 §5) — 고름 바뀜: 랜드마크가 계단식으로 내려앉고(3단) 이름 줄이 8px 아래서 올라오고, 유닛이 왼쪽에서 걸어 들어온다
        private static readonly float[] LandmarkDrop = { 24f, 12f, 4f };
        private const float StepTime = 0.04f;
        private const float RiseFrom = 8f;
        private const float RiseTime = 0.1f;
        private const float WalkTime = 0.3f;
        // 첫 유닛(무대 x 520)이 무대 왼쪽 선(x 412) 밖으로 나가지 않는 거리
        private const float WalkFrom = 100f;
        private const float WalkStagger = 0.04f;
        // 「시작」 — 유닛이 오른쪽으로 걸어 나가고(WalkTime) 판이 3단으로 사라지며 지도가 드러난다
        // 가장 긴 줄(트라이브 · 끝 x 약 1160)도 무대 오른쪽 선(x 1910) 안에서 멈추는 거리
        private const float ExitWalk = 400f;
        private const float FadeTime = 0.12f;

        [Header("목록(왼쪽 기둥)")]
        [Tooltip("국가 한 줄의 본 — 꺼 두고 국가 수만큼 복제한다")]
        [SerializeField] private NationRow _rowTemplate;
        [Tooltip("「고른 국가의 기물이 상점에 더 자주 나옵니다」 — 목록 바로 아래로 따라간다")]
        [SerializeField] private RectTransform _hint;

        [Header("탑 기둥 — 그 국가 재질의 층 셋 + 내 색 지붕")]
        [SerializeField] private FloorLook[] _floors;
        [SerializeField] private Image _roof;

        [Header("무대(지도 자리)")]
        [Tooltip("국가 기호 · 이름 · 한 줄을 묶은 것 — 고름이 바뀌면 같이 올라온다")]
        [SerializeField] private RectTransform _title;
        [SerializeField] private Image _emblem;
        [SerializeField] private Text _name;
        [SerializeField] private Text _line;
        [Tooltip("먼 산을 늘어놓는 자리(가려 자르는 판) — 아래 = 바닥선")]
        [SerializeField] private RectTransform _backdrop;
        [Tooltip("랜드마크 — 왼쪽 아래 기준, 바닥선 위")]
        [SerializeField] private Image _landmark;
        [Tooltip("유닛 줄 — 왼쪽 아래 = 바닥선 위 첫 유닛의 발")]
        [SerializeField] private RectTransform _units;
        [Tooltip("빌드 갈래 칸(왼쪽부터) — 칸마다 대표 기물 아이콘 · 흐름 이름 · 한 줄. 갈래보다 많은 칸은 끈다")]
        [SerializeField] private RectTransform[] _paths;
        [SerializeField] private Image[] _pathIcons;
        [SerializeField] private Text[] _pathNames;
        [SerializeField] private Text[] _pathLines;
        [SerializeField] private Button _start;
        [Tooltip("판 전체 — 시작할 때 사라지는 단위")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("국가 고르기 앞에 닉네임을 묻는 판(UI/NicknamePanel) — 새 런마다 한 번")]
        [SerializeField] private NicknamePanel _nickname;

        // 「시작」을 눌렀다 — 고른 국가. 판은 스스로 닫힌다
        public event System.Action<BuildingNation> Chosen;

        private BuildingNation[] _choices;
        private WorldConfig _world;
        // 지금 고른 국가(_choices의 칸)
        private int _index;
        private readonly List<NationRow> _rows = new List<NationRow>();
        private readonly List<RectTransform> _unitViews = new List<RectTransform>();
        private readonly List<float> _unitHome = new List<float>();
        private Vector2 _titleHome, _landmarkHome;
        private bool _homesRead;
        // 고름이 바뀐 뒤 · 시작을 누른 뒤 흐른 실제 시간(음수 = 안 움직임)
        private float _showClock = -1f;
        private float _exitClock = -1f;

        private void Awake()
        {
            _start.onClick.AddListener(Begin);
            _rowTemplate.gameObject.SetActive(false);
            // 처음 열림(UI/PopupMotion) — 검은 바탕 · 테두리(Frame)는 제자리, 목록 · 탑 · 무대를 같은 크기의 판에 옮겨(자리는 그대로) 그 판이 내려앉는다.
            // 바탕까지 흐려지면 뒤의 지도가 비친다(닉네임 판과 같은 이유)
            var content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            content.SetParent(transform, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            var parts = new List<Transform>();
            foreach (Transform child in transform)
                if (child != content && child.name != "Frame")
                    parts.Add(child);
            foreach (Transform part in parts)
                part.SetParent(content, false);
            content.SetAsFirstSibling();
            _motion = PopupMotion.Attach(content);
        }

        private PopupMotion _motion;

        // choices = 고를 수 있는 국가(목록 순서) · world = 국가마다의 무대 내용
        public void Open(BuildingNation[] choices, WorldConfig world)
        {
            // 이번 런에서 아직 닉네임을 안 물었으면 그 판부터 — 확정하면 여기로 다시 온다
            if (_nickname != null && _nickname.Ask(() => Open(choices, world)))
                return;
            _choices = choices;
            _world = world;
            if (!_homesRead)
            {
                _titleHome = _title.anchoredPosition;
                _landmarkHome = _landmark.rectTransform.anchoredPosition;
                _homesRead = true;
            }
            foreach (NationRow row in _rows)
                Destroy(row.gameObject);
            _rows.Clear();
            var at = ((RectTransform)_rowTemplate.transform).anchoredPosition;
            for (int i = 0; i < choices.Length; i++)
            {
                NationRow row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
                row.gameObject.SetActive(true);
                ((RectTransform)row.transform).anchoredPosition = at - new Vector2(0f, i * RowStep);
                BuildingNation nation = choices[i];
                NationShowcase show = world.Showcase(nation);
                row.Set(show != null ? show.EmblemSmall : null, nation.DisplayName(), Tagline(nation));
                int index = i;
                row.Clicked += () => Select(index);
                _rows.Add(row);
            }
            // 안내 글 = 마지막 줄 바로 아래(줄의 기준 = 왼쪽 위)
            float listBottom = at.y - (choices.Length - 1) * RowStep - ((RectTransform)_rowTemplate.transform).rect.height;
            _hint.anchoredPosition = new Vector2(_hint.anchoredPosition.x, listBottom - HintGap);
            // 탑 — 지도 화면의 탑 기둥과 같은 자리(탑 기둥 가운데 · 바닥 막대 위, UI/FrameBars)
            float top = -FrameBars.Floor;
            foreach (FloorLook floor in _floors)
            {
                var rect = (RectTransform)floor.transform;
                rect.anchoredPosition = new Vector2(FrameBars.TowerCenter, top + rect.rect.height * rect.pivot.y);
                top += rect.rect.height;
            }
            _roof.rectTransform.anchoredPosition = new Vector2(FrameBars.TowerCenter, top);
            _roof.color = RunState.LordKeyColor;
            _group.alpha = 1f;
            _exitClock = -1f;
            gameObject.SetActive(true);
            _motion.Open();
            Select(0);
        }

        private void Update()
        {
            if (_exitClock >= 0f)
            {
                Exit();
                return;
            }
            Animate();
            Keyboard keys = Keyboard.current;
            if (keys == null || _choices == null)
                return;
            if (keys.upArrowKey.wasPressedThisFrame)
                Select((_index - 1 + _choices.Length) % _choices.Length);
            else if (keys.downArrowKey.wasPressedThisFrame)
                Select((_index + 1) % _choices.Length);
            else if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                Begin();
        }

        // 국가의 한 줄 — 설명 글의 첫 문장(마침표 앞)
        private string Tagline(BuildingNation nation)
        {
            string text = _world.NationDescription(nation);
            int end = text.IndexOf('.');
            return end > 0 ? text.Substring(0, end) : text;
        }

        private void Select(int index)
        {
            _index = index;
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = i == index;
            BuildingNation nation = _choices[index];
            NationShowcase show = _world.Showcase(nation);
            _name.text = nation.DisplayName();
            _line.text = Tagline(nation);
            _emblem.sprite = show != null ? show.Emblem : null;
            _emblem.enabled = _emblem.sprite != null;
            if (_emblem.enabled)
                _emblem.SetNativeSize();
            DrawBackdrop(show != null ? show.Backdrop : null);
            Sprite landmark = show != null ? show.Landmark : null;
            _landmark.enabled = landmark != null;
            if (landmark != null)
            {
                _landmark.sprite = landmark;
                _landmark.rectTransform.sizeDelta = landmark.rect.size * LandmarkScale;
            }
            DrawUnits(show != null ? show.Units : new UnitDefinition[0]);
            NationPath[] paths = show != null ? show.Paths : new NationPath[0];
            for (int i = 0; i < _paths.Length; i++)
            {
                bool used = i < paths.Length;
                _paths[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                Sprite icon = paths[i].Building != null ? paths[i].Building.IconLarge : null;
                _pathIcons[i].enabled = icon != null;
                _pathIcons[i].sprite = icon;
                if (icon != null)
                    _pathIcons[i].SetNativeSize();
                _pathNames[i].text = paths[i].Name;
                _pathLines[i].text = paths[i].Line;
            }
            // 탑 기둥 — 그 국가 기물로 쌓은 층(대표 기물의 재질 · 창)
            BuildingDefinition sample = paths.Length > 0 ? paths[0].Building : null;
            foreach (FloorLook floor in _floors)
                floor.Show(sample);
            _showClock = 0f;
            Animate();
        }

        // 먼 산 — 무대 폭을 채울 만큼 같은 그림을 이어 붙인다(아래 = 바닥선)
        private void DrawBackdrop(Sprite sprite)
        {
            for (int i = _backdrop.childCount - 1; i >= 0; i--)
                Destroy(_backdrop.GetChild(i).gameObject);
            if (sprite == null)
                return;
            Vector2 size = sprite.rect.size * BackdropScale;
            for (float x = 0f; x < _backdrop.rect.width; x += size.x)
            {
                var image = new GameObject("Hill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(_backdrop, false);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = Vector2.zero;
                image.rectTransform.anchoredPosition = new Vector2(x, 0f);
                image.rectTransform.sizeDelta = size;
                image.sprite = sprite;
                image.color = BackdropTint;
                image.raycastTarget = false;
            }
        }

        // 유닛 줄 — 왼쪽부터 발을 바닥선에 맞춰 UnitGap 간격으로
        private void DrawUnits(UnitDefinition[] units)
        {
            for (int i = _units.childCount - 1; i >= 0; i--)
                Destroy(_units.GetChild(i).gameObject);
            _unitViews.Clear();
            _unitHome.Clear();
            float x = 0f;
            foreach (UnitDefinition unit in units)
            {
                if (unit == null)
                    continue;
                var view = (RectTransform)new GameObject(unit.name, typeof(RectTransform)).transform;
                view.SetParent(_units, false);
                view.anchorMin = view.anchorMax = view.pivot = Vector2.zero;
                Vector2 size = UnitFigure.Draw(unit.PrefabFor(Team.Ally), view, UnitScale, RunState.LordColor(Team.Ally));
                view.sizeDelta = size;
                view.anchoredPosition = new Vector2(x, 0f);
                _unitViews.Add(view);
                _unitHome.Add(x);
                x += size.x + UnitGap;
            }
        }

        private void Animate()
        {
            if (_showClock < 0f)
                return;
            _showClock += Time.unscaledDeltaTime;
            float t = _showClock;
            // 랜드마크 — 3단 계단으로 내려앉는다
            int step = Mathf.FloorToInt(t / StepTime);
            float drop = step < LandmarkDrop.Length ? LandmarkDrop[step] : 0f;
            _landmark.rectTransform.anchoredPosition = _landmarkHome + new Vector2(0f, drop);
            // 이름 줄 — 8px 아래서 2단으로 올라온다
            float rise = t < RiseTime * 0.5f ? RiseFrom : t < RiseTime ? RiseFrom * 0.5f : 0f;
            _title.anchoredPosition = _titleHome - new Vector2(0f, rise);
            // 유닛 — 왼쪽에서 걸어 들어온다(그림 1px 단위로 끊어서 · 한 걸음마다 1px 들썩)
            bool moving = false;
            for (int i = 0; i < _unitViews.Count; i++)
                moving |= Walk(i, Mathf.Clamp01((t - i * WalkStagger) / WalkTime), -WalkFrom, 0f);
            if (!moving && step >= LandmarkDrop.Length && t >= RiseTime)
                _showClock = -1f;
        }

        // i번째 유닛을 home + from → home + to로 p(0~1)만큼 — 움직이는 중이면 true
        private bool Walk(int i, float p, float from, float to)
        {
            bool moving = p < 1f;
            _unitViews[i].anchoredPosition = new Vector2(_unitHome[i], 0f) + UnitFigure.Step(Mathf.Lerp(from, to, p), UnitScale, moving);
            return moving;
        }

        private void Begin()
        {
            if (_exitClock >= 0f)
            {
                // 나가는 중에 또 누르면 바로 끝낸다
                Finish();
                return;
            }
            _showClock = -1f;
            _exitClock = 0f;
            Animate();
        }

        // 시작 연출 — 유닛이 오른쪽으로 걸어 나가고(WalkTime), 판이 3단으로 사라진다(FadeTime)
        private void Exit()
        {
            _exitClock += Time.unscaledDeltaTime;
            float t = _exitClock;
            for (int i = 0; i < _unitViews.Count; i++)
                Walk(i, Mathf.Clamp01(t / WalkTime), 0f, ExitWalk);
            float fade = (t - WalkTime) / FadeTime;
            _group.alpha = fade <= 0f ? 1f : fade < 1f / 3f ? 2f / 3f : fade < 2f / 3f ? 1f / 3f : 0f;
            Keyboard keys = Keyboard.current;
            bool skip = keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (fade >= 1f || skip)
                Finish();
        }

        private void Finish()
        {
            _exitClock = -1f;
            gameObject.SetActive(false);
            Chosen?.Invoke(_choices[_index]);
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 테스트용 치트 패널 — F1로 여닫는다. 씬에 두지 않는다(게임이 켜질 때 스스로 생긴다 — 에디터 플레이·빌드 둘 다).
    // IMGUI는 그리기만 하고 클릭·글자 입력은 Input System으로 직접 읽는다 — 입력 설정이 Input System 전용이라 빌드에서는 OnGUI에 입력 이벤트가 오지 않는다.
    // 분류 탭(기물 · 값 · 전투 · 런) 하나만 보인다. 지금 단계(지도·전투)에서 못 쓰는 것은 흐리게.
    // 기물 넣기는 칸에 바로 놓는다(「구매할 때마다,」·합치기 없음). 게임 쪽 훅은 RunState·BattleManager·Base의 Cheat* 뿐 — 에셋 값은 바꾸지 않는다
    public class CheatPanel : MonoBehaviour
    {
        // 배포 빌드에서는 false로
        private static readonly bool Enabled = true;

        private const float Width = 640f;
        private const float RowHeight = 30f;
        private const float Pad = 8f;
        private const float Gap = 4f;
        private const int FontSize = 16;
        private const int MaxDigits = 9;
        private static readonly string[] TabNames = { "기물", "값", "전투", "런" };
        private static readonly string[] RowNames = { "내 탑", "보관함", "적 탑" };
        private static readonly float[] Speeds = { 0f, 8f, 16f };
        private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.27f, 1f);
        private static readonly Color DisabledColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        private static readonly Color FieldColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color Lit = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color Dim = new Color(0.45f, 0.45f, 0.48f, 1f);

        private bool _open;
        private int _tab;
        // 고른 줄(0 내 탑 · 1 보관함 · 2 적 탑)과 칸
        private int _row;
        private int _index;
        // 기물 목록에서 펼친 국가·세력 묶음(상점 풀에 있는 것만)
        private readonly List<(BuildingNation nation, BuildingFaction faction)> _groups = new List<(BuildingNation, BuildingFaction)>();
        private BuildingDefinition[] _groupsOf;
        private int _group;
        // 적 탑 편집본(빈 칸 없이 아래부터) — 켜져 있으면 적이 상점에서 짠 탑 대신 이 보드
        private OwnedBuilding[] _enemy;
        private bool _enemyOn;
        // 치트 배속 — 0이면 끔(게임의 배속 버튼 그대로)
        private float _speed;
        private string _note = "";

        // 값 입력 — 포커스된 란(없으면 null)과 치고 있는 글자. Enter = _submit
        private string _focus;
        private string _buffer = "";
        private bool _submit;
        private bool _keepFocus;
        private Keyboard _keyboard;

        private Rect _panel = new Rect(104f, 8f, Width, 600f);
        private bool _click;
        private Vector2 _pointer;
        private float _x, _y;
        private RectTransform _blocker;
        private GUIStyle _label, _center;
        private Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (!Enabled)
                return;
            var host = new GameObject("CheatPanel");
            DontDestroyOnLoad(host);
            host.AddComponent<CheatPanel>();
        }

        // 화면 높이 800칸 기준으로 키운다 — 패널이 어느 해상도에서도 다 들어오게
        private static float Scale => Screen.height / 800f;

        // 기물 · 런을 고칠 수 있는 때 — 지도에서 명령을 받는 중(상점 팝업이 열려 있어도)
        private static bool Shop => WorldManager.Commanding;
        private static bool Fight => BattleManager.Fighting;

        // 패널 뒤의 게임 UI가 눌리지 않게 — 패널 크기의 투명한 판을 맨 위 캔버스에 둔다. 클릭 · 호버 · 카메라 끌기를 EventSystem이 평소대로 막는다
        // (EventSystem을 껐다 켜는 방식은 패널에서 한 클릭이 패널 밖으로 나간 자리의 UI에 뒤늦게 들어간다)
        private void Awake()
        {
            var canvas = new GameObject("Blocker", typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var image = new GameObject("Rect", typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(canvas.transform, false);
            image.color = Color.clear;
            _blocker = image.rectTransform;
            _blocker.anchorMin = _blocker.anchorMax = Vector2.zero;
            _blocker.pivot = new Vector2(0f, 1f);
        }

        private void OnDisable() => Listen(null);

        // 글자 입력은 키보드가 바뀌어도(다시 꽂기) 따라간다
        private void Listen(Keyboard keyboard)
        {
            if (_keyboard == keyboard)
                return;
            if (_keyboard != null)
                _keyboard.onTextInput -= OnText;
            _keyboard = keyboard;
            if (_keyboard != null)
                _keyboard.onTextInput += OnText;
        }

        // 포커스된 란에 숫자만 — 지우기·Enter·Esc는 Update가 키로 읽는다
        private void OnText(char c)
        {
            if (!_open || _focus == null || _buffer.Length >= MaxDigits)
                return;
            if (char.IsDigit(c) || (c == '.' && !_buffer.Contains(".")) || (c == '-' && _buffer.Length == 0))
                _buffer += c;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Listen(keyboard);
            if (keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame)
                {
                    _open = !_open;
                    _focus = null;
                }
                if (_focus != null)
                {
                    if (keyboard.backspaceKey.wasPressedThisFrame && _buffer.Length > 0)
                        _buffer = _buffer.Substring(0, _buffer.Length - 1);
                    if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                        _submit = true;
                    if (keyboard.escapeKey.wasPressedThisFrame)
                        _focus = null;
                }
            }

            Mouse mouse = Mouse.current;
            bool over = false;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                _pointer = new Vector2(at.x, Screen.height - at.y) / Scale;
                over = _open && _panel.Contains(_pointer);
                _click = over && mouse.leftButton.wasPressedThisFrame;
                if (!over && mouse.leftButton.wasPressedThisFrame)
                    _focus = null;
            }
            _blocker.gameObject.SetActive(_open);
            _blocker.anchoredPosition = new Vector2(_panel.x * Scale, Screen.height - _panel.y * Scale);
            _blocker.sizeDelta = _panel.size * Scale;

            // 전투 시작·배속 버튼이 게임 배속으로 되돌리면 다시 건다(히트스톱 중인 값은 건드리지 않는다)
            if (_speed > 0f && Fight && Mathf.Approximately(Time.timeScale, SpeedButton.Current))
                Time.timeScale = _speed;
        }

        private void OnGUI()
        {
            if (!_open || Event.current.type != EventType.Repaint)
                return;
            if (_label == null)
            {
                // 한글이 나오게 OS 글꼴로(씬·프리팹 참조 없이 쓸 수 있다). HideAndDontSave — 플레이를 끌 때 같이 지워지면(Destroy로 지워도)
                // 다음 컴파일 때 에디터가 "Deleting invalid font reference" 에러를 낸다
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, FontSize);
                _font.hideFlags = HideFlags.HideAndDontSave;
                _label = new GUIStyle(GUI.skin.label) { font = _font, fontSize = FontSize, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
                _label.normal.textColor = Color.white;
                _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            Fill(_panel, PanelColor);
            _x = _panel.x + Pad;
            _y = _panel.y + Pad;
            bool pressed = _click;
            _keepFocus = false;
            Draw();
            NewLine();
            _panel.height = _y - _panel.y + Pad;
            // 란 밖을 누르면 입력을 그만둔다
            if (pressed && !_keepFocus)
                _focus = null;
            _click = false;
            _submit = false;
        }

        private void Draw()
        {
            if (!RunState.Started || (BattleManager.Config == null && !WorldManager.Commanding))
            {
                Label("치트 (F1) — 지도에서 명령을 받는 중이거나 전투 씬일 때만 쓴다");
                return;
            }
            string phase = Fight ? $"전투 {BattleManager.Elapsed:0}초" : Shop ? "지도" : "결과";
            Label($"치트 (F1 닫기) · 턴 {RunState.Round + 1} · 땅 {RunState.Lands}/{RunState.RegionCount} · {phase} · 골드 {RunState.Gold} · 맵 {MapName(RunState.Map)} · 시간 ×{Time.timeScale:0.##}", Lit);
            for (int t = 0; t < TabNames.Length; t++)
                if (Button(TabNames[t], 120f, _tab == t))
                {
                    _tab = t;
                    _focus = null;
                    _note = "";
                }
            _y += Gap;
            switch (_tab)
            {
                case 0: DrawBuildings(); break;
                case 1: DrawValues(); break;
                case 2: DrawBattle(); break;
                default: DrawRun(); break;
            }
            if (_note.Length > 0)
                Label(_note, Lit);
        }

        // ── 탭: 기물 — 줄 → 칸 → 칸 조작 → 국가·세력 묶음 → 기물
        private void DrawBuildings()
        {
            Label(Shop ? "줄과 칸을 고르고, 아래 기물을 누르면 그 줄의 빈 칸에 들어간다" : "기물은 지도에서만 고친다", Shop ? Dim : Lit);
            for (int r = 0; r < RowNames.Length; r++)
                if (Button(RowNames[r], 120f, _row == r))
                    SelectRow(r);
            if (_row == 2)
                Text(_enemyOn ? "치트 적 보드 켬" : "고치면 치트 적 보드가 켜진다", 220f, Dim);
            NewLine();
            OwnedBuilding[] row = Row;
            _index = Mathf.Min(_index, row.Length - 1);
            for (int i = 0; i < row.Length; i++)
            {
                bool empty = OwnedBuilding.IsEmpty(row[i]);
                string name = empty ? "빈 칸" : $"{row[i].Definition.DisplayName} {row[i].Rarity.DisplayName()}";
                if (Button($"{i + 1}  {name}", 310f, i == _index, true, empty ? Dim : row[i].Rarity.FrameColor()))
                    _index = i;
            }
            NewLine();
            if (Button("등급 +", 120f, false, Shop)) Rank(1);
            if (Button("등급 −", 120f, false, Shop)) Rank(-1);
            if (Button("칸 비우기", 120f, false, Shop)) ClearSlot();
            if (Button("전부 비우기", 120f, false, Shop)) ClearRow();
            NewLine();
            _y += Gap;

            RefreshGroups();
            for (int g = 0; g < _groups.Count; g++)
                if (Button(GroupName(g), 121f, _group == g))
                    _group = g;
            NewLine();
            foreach (BuildingDefinition definition in RunState.Pool)
                if (InGroup(definition, _group) && Button(definition.DisplayName, 204f, false, Shop, definition.Rarity.FrameColor()))
                    Add(definition);
        }

        // 상점 풀에 실제로 있는 국가·세력만, enum 순서로
        private void RefreshGroups()
        {
            if (_groupsOf == RunState.Pool)
                return;
            _groupsOf = RunState.Pool;
            _groups.Clear();
            foreach (BuildingNation nation in System.Enum.GetValues(typeof(BuildingNation)))
                foreach (BuildingFaction faction in System.Enum.GetValues(typeof(BuildingFaction)))
                    if (System.Array.Exists(_groupsOf, b => b.Nation == nation && b.Faction == faction))
                        _groups.Add((nation, faction));
            _group = Mathf.Clamp(_group, 0, Mathf.Max(0, _groups.Count - 1));
        }

        private string GroupName(int group)
        {
            string faction = _groups[group].faction.DisplayName();
            return faction.Length > 0 ? $"{_groups[group].nation.DisplayName()} {faction}" : _groups[group].nation.DisplayName();
        }

        private bool InGroup(BuildingDefinition definition, int group)
            => group < _groups.Count && definition.Nation == _groups[group].nation && definition.Faction == _groups[group].faction;

        // ── 탭: 값 — 플레이 중 메모리 값만 고친다(에셋은 그대로)
        private void DrawValues()
        {
            Label("칸을 누르고 숫자를 친 뒤 Enter(또는 적용) · −/+ 는 한 번에 오른쪽 폭만큼", Dim);
            Label("런", Lit);
            DrawRunValues();
            Label(Fight ? "전투" : "전투 — 전투 중에만", Lit);
            DrawBattleValues();
        }

        private void DrawRunValues()
        {
            ValueRow("gold", "골드", RunState.Gold, $"{RunState.Gold}", 10f, true, v => RunState.CheatSetGold(Mathf.RoundToInt(v)));
            ValueRow("lives", "목숨", RunState.Lives, $"{RunState.Lives} / {RunState.LivesMax}", 1f, true, v => RunState.CheatSetLives(Mathf.RoundToInt(v)));
            ValueRow("round", "턴", RunState.Round + 1, $"{RunState.Round + 1}", 1f, Shop, v => SetRound(Mathf.RoundToInt(v) - 1));
            ValueRow("floors", "내 탑 층수", RunState.Floors, $"{RunState.Floors} / {RunState.MaxFloors}", 1f, Shop, v => SetFloors(Mathf.RoundToInt(v)));
        }

        private void DrawBattleValues()
        {
            HpRow("hp", "내 본진 체력", Team.Ally);
            HpRow("ehp", "적 본진 체력", Team.Enemy);
            ValueRow("time", "경과 시간(초)", BattleManager.Elapsed, $"{BattleManager.Elapsed:0}", 10f, Fight, v => BattleManager.CheatSkipTo(Mathf.Max(0f, v)));
            ValueRow("speed", "시간 배율 (0 = 끔)", _speed, _speed > 0f ? $"×{_speed:0.##}" : "끔", 1f, Fight, v => SetSpeed(Mathf.Clamp(v, 0f, 32f)));
        }

        private void HpRow(string id, string name, Team team)
        {
            Base target = Fight ? Base.For(team) : null;
            if (target == null)
                ValueRow(id, name, 0f, "-", 100f, false, null);
            else
                ValueRow(id, name, target.Hp, $"{Mathf.CeilToInt(target.Hp)} / {Mathf.CeilToInt(target.MaxHp)}", 100f, true, target.CheatSetHp);
        }

        // ── 탭: 전투
        private void DrawBattle()
        {
            Label(Fight ? "전투 중" : "전투 중에만 쓸 수 있다", Fight ? Dim : Lit);
            if (Button("데스매치로", 130f, false, Fight)) BattleManager.CheatSkipTo(BattleManager.DeathmatchAt);
            if (Button("기물 정지로", 130f, false, Fight)) BattleManager.CheatSkipTo(BattleManager.BoardStopAt);
            if (Button("승리로 끝", 130f, false, Fight)) BattleManager.CheatFinish(true);
            if (Button("패배로 끝", 130f, false, Fight)) BattleManager.CheatFinish(false);
            NewLine();
            if (Button("본진 피해", 130f, false, Fight)) NudgeAllyBase(-0.1f);
            if (Button("본진 회복", 130f, false, Fight)) NudgeAllyBase(0.1f);
            NewLine();
            foreach (float speed in Speeds)
                if (Button(speed > 0f ? $"배속 ×{speed:0}" : "배속 끔", 130f, _speed == speed, Fight))
                    SetSpeed(speed);
            NewLine();
            _y += Gap;
            DrawBattleValues();
        }

        // 내 본진 체력을 최대 체력의 ratio만큼 바꾼다(체력바 손맛 확인용 — 1 아래로는 안 내려간다)
        private static void NudgeAllyBase(float ratio)
        {
            Base target = Base.For(Team.Ally);
            if (target != null)
                target.CheatSetHp(target.Hp + target.MaxHp * ratio);
        }

        // ── 탭: 런
        private void DrawRun()
        {
            Label("맵 — 고르면 「땅의 지형」으로 풀 때까지 어느 땅을 치든 그 맵", Dim);
            if (Button("땅의 지형", 100f, RunState.CheatMap == null))
                SetMap(null);
            foreach (MapDefinition map in RunState.Config.Maps)
                if (Button(MapName(map), 84f, RunState.CheatMap == map))
                    SetMap(map);

            Label("턴 — 성 체력 · 진열 등급이 따라간다 (골드 · 새 진열은 안 주고, 영주들도 그대로다)", Dim);
            ValueRow("round", "턴", RunState.Round + 1, $"{RunState.Round + 1}", 1f, Shop, v => SetRound(Mathf.RoundToInt(v) - 1));
            NewLine();
            if (Button("진열 다시 뽑기", 150f, false, Shop)) RunState.CheatRollOffers();
            if (Button("새 런 시작", 150f, false, Shop)) NewRun();

            Label($"판도 — 내 땅 {RunState.Lands}/{RunState.RegionCount} · 목숨 {RunState.Lives}/{RunState.LivesMax}{(RunState.InCrisis ? "(국가 위기)" : "")} ·남은 영주 {RunState.World.State.AiLords}명", Dim);
            if (Button("이웃 영주 먹기", 150f, false, Shop)) Conquer();
            Text("땅이 가장 적은 이웃 영주를 전투 없이 이긴 것으로 친다", 460f, Dim);
            NewLine();
            System.Collections.Generic.List<int> visitable = RunState.VisitableEvents();
            if (Button("이벤트 땅 방문", 150f, false, Shop && visitable.Count > 0)) Visit();
            Text(visitable.Count > 0 ? $"방문할 수 있는 이벤트 땅 {string.Join(" · ", visitable.ConvertAll(e => $"{e + 1:00} {EventLands.Name(RunState.World.State.EventKind[e])}"))} — 첫 땅을 방문(이번 턴의 침략 대신)" : "국경을 맞댄 열린 이벤트 땅이 없다", 460f, Dim);
            NewLine();
            if (Button(_enemyOn ? "치트 적 보드: 켬" : "치트 적 보드: 끔", 180f, _enemyOn))
                SetEnemyOverride(!_enemyOn);
            Text("켜면 적 탑 = 기물 탭 「적 탑」 편집본", 320f, Dim);
        }

        private static string MapName(MapDefinition map)
            => map == null ? "-" : map.DisplayName.Length > 0 ? map.DisplayName : map.name;

        // ── 그리기 — 왼쪽에서 오른쪽으로 놓고, 넘치면 다음 줄

        private void NewLine()
        {
            if (_x <= _panel.x + Pad)
                return;
            _x = _panel.x + Pad;
            _y += RowHeight;
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void Write(Rect rect, string text, Color color, GUIStyle style)
        {
            GUI.contentColor = color;
            GUI.Label(rect, text, style);
            GUI.contentColor = Color.white;
        }

        // 한 줄을 다 쓰는 글
        private void Label(string text) => Label(text, Color.white);

        private void Label(string text, Color color)
        {
            NewLine();
            Write(new Rect(_x, _y, Width - Pad * 2f, RowHeight), text, color, _label);
            _y += RowHeight;
        }

        // 줄 안에 놓는 글
        private void Text(string text, float width, Color color)
        {
            Write(new Rect(_x + Gap, _y, width - Gap, RowHeight - Gap), text, color, _label);
            _x += width + Gap;
        }

        // lit = 고른 것(노란 테두리) · enabled가 꺼지면 흐리고 안 눌린다 · color = 글자색(기물 등급)
        private bool Button(string text, float width, bool lit = false, bool enabled = true, Color? color = null)
        {
            if (_x + width > _panel.xMax - Pad)
                NewLine();
            var rect = new Rect(_x, _y, width, RowHeight - Gap);
            _x += width + Gap;
            if (lit)
            {
                Fill(rect, Lit);
                Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), enabled ? ButtonColor : DisabledColor);
            }
            else
                Fill(rect, enabled ? ButtonColor : DisabledColor);
            Write(rect, text, !enabled ? Dim : color ?? (lit ? Lit : Color.white), _center);
            if (!enabled || !_click || !rect.Contains(_pointer))
                return false;
            _click = false;   // 한 번 누르면 버튼 하나만 — 눌러서 줄이 바뀌어도 다음 버튼이 같이 눌리지 않게
            return true;
        }

        // 값 한 줄 — 이름 · 지금 값 · 입력 칸 · − · + · 적용. 입력 칸을 누르면 포커스, Enter나 적용으로 set(친 값)
        private void ValueRow(string id, string name, float value, string shown, float step, bool enabled, System.Action<float> set)
        {
            NewLine();
            Text(name, 150f, enabled ? Color.white : Dim);
            Text(shown, 120f, enabled ? Lit : Dim);
            bool focused = enabled && _focus == id;
            var field = new Rect(_x, _y, 120f, RowHeight - Gap);
            _x += field.width + Gap;
            Fill(field, focused ? Lit : enabled ? ButtonColor : DisabledColor);
            Fill(new Rect(field.x + 2f, field.y + 2f, field.width - 4f, field.height - 4f), FieldColor);
            if (focused)
                Write(field, _buffer + "_", Color.white, _center);
            if (enabled && _click && field.Contains(_pointer))
            {
                _click = false;
                _keepFocus = true;
                if (!focused)
                {
                    _focus = id;
                    _buffer = "";
                }
            }
            if (Button("−", 40f, false, enabled)) set(value - step);
            if (Button("+", 40f, false, enabled)) set(value + step);
            Text($"±{step:0}", 44f, Dim);
            bool typed = float.TryParse(_buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out float entered);
            if ((Button("적용", 60f, false, focused && typed) || (focused && _submit)) && typed)
            {
                set(entered);
                _focus = null;
            }
            NewLine();
        }

        // ── 기물

        private OwnedBuilding[] Row => _row == 0 ? RunState.Field : _row == 1 ? RunState.Storage : (_enemy ??= CopyEnemyBoard());

        public void SelectRow(int row)
        {
            _row = row;
            _index = 0;
            // 덮어쓰기가 꺼져 있으면 편집본은 지금 적 탑에서 다시 뜬다
            if (row == 2 && !_enemyOn)
                _enemy = CopyEnemyBoard();
        }

        public void SelectSlot(int index) => _index = index;

        // 지금 적 탑의 사본 — 적이 상점에서 짠 탑(RunState.EnemyField)의 기물을 그대로 고치면 적의 런이 바뀐다
        private static OwnedBuilding[] CopyEnemyBoard()
        {
            var copy = new OwnedBuilding[RunState.MaxFloors];
            // 지도 씬에는 돌고 있는 보드가 없다 — 고른 땅의 영주의 탑(없으면 빈 탑)
            BoardRunner board = BoardRunner.For(Team.Enemy);
            OwnedBuilding[] field = board != null ? board.Field : RunState.EnemyField;
            for (int i = 0; i < field.Length && i < copy.Length; i++)
                if (!OwnedBuilding.IsEmpty(field[i]))
                    copy[i] = Copy(field[i], field[i].Upgrades);
            return copy;
        }

        private static OwnedBuilding Copy(OwnedBuilding building, int upgrades)
        {
            var copy = new OwnedBuilding(building.Definition);
            for (int i = 0; i < upgrades; i++)
                copy.Upgrade();
            copy.AddStacks(building.Stacks);
            copy.AddSpent(building.SpentGold);   // 판매가의 바탕(RunState.SellValue) — 사본이 공짜 기물이 되지 않게
            return copy;
        }

        public void Add(BuildingDefinition definition)
        {
            if (!Shop)
                return;
            OwnedBuilding[] row = Row;
            int slot = System.Array.FindIndex(row, OwnedBuilding.IsEmpty);
            _note = slot < 0 ? "빈 칸이 없다" : "";
            if (slot < 0)
                return;
            row[slot] = new OwnedBuilding(definition);
            _index = slot;
            Changed();
        }

        // 등급만 바꾼다(합쳐진 것처럼 — 스택은 그대로)
        public void Rank(int delta)
        {
            OwnedBuilding[] row = Row;
            if (!Shop || _index >= row.Length || OwnedBuilding.IsEmpty(row[_index]))
                return;
            OwnedBuilding building = row[_index];
            if (delta > 0 && building.CanUpgrade)
                building.Upgrade();
            else if (delta < 0 && building.Upgrades > 0)
                row[_index] = Copy(building, building.Upgrades - 1);
            Changed();
        }

        public void ClearSlot()
        {
            OwnedBuilding[] row = Row;
            if (!Shop || _index >= row.Length)
                return;
            row[_index] = null;
            Changed();
        }

        public void ClearRow()
        {
            if (!Shop)
                return;
            System.Array.Clear(Row, 0, Row.Length);
            Changed();
        }

        // 탑(내 탑 · 적 탑)은 중간 층을 비울 수 없다 — 아래로 당긴다. 적 탑을 고쳤으면 덮어쓰기를 켠다
        private void Changed()
        {
            if (_row == 1)
                return;
            OwnedBuilding[] row = Row;
            System.Array.Copy(OwnedBuilding.Compacted(row), row, row.Length);
            if (_row == 2)
                SetEnemyOverride(true);
        }

        // 켜면 다음 전투부터 적 탑 = 편집본(끌 때까지 매 라운드 — 그동안에도 적은 상점을 돌며 자기 탑을 키운다)
        public void SetEnemyOverride(bool on)
        {
            _enemyOn = on;
            _enemy ??= CopyEnemyBoard();
            OwnedBuilding[] field = null;
            if (on)
            {
                // 층수 = 기물 수(적어도 한 층)
                field = new OwnedBuilding[Mathf.Max(1, System.Array.FindAll(_enemy, b => !OwnedBuilding.IsEmpty(b)).Length)];
                System.Array.Copy(_enemy, field, field.Length);
            }
            RunState.CheatEnemyField = field;
        }

        // ── 값 · 맵 · 라운드 · 전투

        public void SetFloors(int floors)
        {
            if (Shop)
                RunState.CheatSetFloors(floors);
        }

        // 이웃 영주 하나를 먹는다 — 지도를 다시 불러온다
        public void Conquer()
        {
            if (!Shop)
                return;
            RunState.CheatConquer();
            Reload();
        }

        // 국경을 맞댄 첫 이벤트 땅을 방문한다(쉬기와 같은 길) — 지도를 다시 불러오면 AI 턴이 돈다
        public void Visit()
        {
            System.Collections.Generic.List<int> visitable = RunState.VisitableEvents();
            if (!Shop || visitable.Count == 0)
                return;
            RunState.Visit(visitable[0]);
            Reload();
        }

        public void SetMap(MapDefinition map)
        {
            RunState.CheatSetMap(map);
            if (map != null && Shop)
                Reload();
        }

        public void SetRound(int round)
        {
            if (!Shop)
                return;
            RunState.CheatSetRound(Mathf.Max(0, round));
            if (!_enemyOn)
                _enemy = null;   // 새 라운드의 적 탑에서 다시 뜬다
            Reload();
        }

        // 런을 처음부터(골드·탑·판도·영구 강화 전부) — 고른 맵 · 치트 적 보드는 남는다
        public void NewRun()
        {
            if (!Shop)
                return;
            RunState.CheatNewRun();
            if (!_enemyOn)
                _enemy = null;
            Reload();
        }

        // 땅 모양 · 적 탑 · 성 체력은 씬을 불러올 때 정해진다 — 지금 씬(지도)을 다시 불러온다(런 상태는 그대로)
        private static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        public void SetSpeed(float speed)
        {
            _speed = speed;
            if (Fight)
                Time.timeScale = speed > 0f ? speed : SpeedButton.Current;
        }
    }
}

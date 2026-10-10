using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GnorpWar
{
    // 테스트용 치트 패널 — F1로 여닫는다. 씬에 두지 않는다(게임이 켜질 때 스스로 생긴다 — 에디터 플레이·빌드 둘 다).
    // IMGUI는 그리기만 하고 클릭은 Input System으로 직접 읽는다 — 입력 설정이 Input System 전용이라 빌드에서는 OnGUI에 입력 이벤트가 오지 않는다.
    // 기물 넣기는 칸에 바로 놓는다(「구매할 때마다,」·합치기 없음). 게임 쪽 훅은 RunState·BattleManager의 Cheat* 뿐
    public class CheatPanel : MonoBehaviour
    {
        // 배포 빌드에서는 false로
        private static readonly bool Enabled = true;

        private const float Width = 640f;
        private const float RowHeight = 24f;
        private const float Pad = 6f;
        private static readonly string[] RowNames = { "내 탑", "보관함", "적 탑" };
        private static readonly float[] Speeds = { 0f, 8f, 16f };

        private bool _open;
        // 고른 줄(0 내 탑 · 1 보관함 · 2 적 탑)과 칸
        private int _row;
        private int _index;
        // 적 탑 편집본(빈 칸 없이 아래부터) — 켜져 있으면 라운드 표 대신 이 보드
        private OwnedBuilding[] _enemy;
        private bool _enemyOn;
        // 치트 배속 — 0이면 끔(게임의 배속 버튼 그대로)
        private float _speed;
        private string _note = "";

        private Rect _panel = new Rect(8f, 8f, Width, 600f);
        private bool _click;
        private Vector2 _pointer;
        private float _x, _y;
        private EventSystem _events;
        private GUIStyle _button, _label;
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

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                _open = !_open;

            Mouse mouse = Mouse.current;
            bool over = false;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                _pointer = new Vector2(at.x, Screen.height - at.y) / Scale;
                over = _open && _panel.Contains(_pointer);
                _click = over && mouse.leftButton.wasPressedThisFrame;
            }
            // 패널 위에서는 뒤의 게임 UI가 같이 눌리지 않게
            if (_events == null)
                _events = FindFirstObjectByType<EventSystem>();
            if (_events != null && _events.enabled == over)
                _events.enabled = !over;

            // 전투 시작·배속 버튼이 게임 배속으로 되돌리면 다시 건다(히트스톱 중인 값은 건드리지 않는다)
            if (_speed > 0f && BattleManager.Fighting && Mathf.Approximately(Time.timeScale, SpeedButton.Current))
                Time.timeScale = _speed;
        }

        private void OnGUI()
        {
            if (!_open || Event.current.type != EventType.Repaint)
                return;
            if (_button == null)
            {
                // 한글이 나오게 OS 글꼴로(씬·프리팹 참조 없이 쓸 수 있다). HideAndDontSave — 플레이를 끌 때 같이 지워지면(Destroy로 지워도)
                // 다음 컴파일 때 에디터가 "Deleting invalid font reference" 에러를 낸다
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 13);
                _font.hideFlags = HideFlags.HideAndDontSave;
                _button = new GUIStyle(GUI.skin.button) { font = _font, fontSize = 13, clipping = TextClipping.Clip, wordWrap = false };
                _label = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 13, wordWrap = false };
                _label.normal.textColor = Color.white;
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(_panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            _x = _panel.x + Pad;
            _y = _panel.y + Pad;
            Draw();
            NewLine();
            _panel.height = _y - _panel.y + Pad;
            _click = false;
        }

        private void Draw()
        {
            if (!RunState.Started || BattleManager.Config == null)
            {
                Label("치트 (F1) — 전투 씬이 아니다");
                return;
            }
            string phase = BattleManager.Fighting ? $"전투 {BattleManager.Elapsed:0}초" : BattleManager.InShop ? "상점" : "결과";
            Label($"치트 (F1) · 라운드 {RunState.Round + 1} · 골드 {RunState.Gold} · 맵 {MapName(RunState.Map)} · {phase} · 시간 ×{Time.timeScale:0.##}");

            Label("기물 — 줄을 고르고 아래 목록에서 누르면 빈 칸에 들어간다 (상점에서만 · 구매 효과와 합치기 없음)");
            for (int r = 0; r < RowNames.Length; r++)
                if (Button(RowNames[r], 90f, _row == r))
                    SelectRow(r);
            if (Button(_enemyOn ? "치트 적 보드: 켬" : "치트 적 보드: 끔", 140f, _enemyOn))
                SetEnemyOverride(!_enemyOn);
            NewLine();
            OwnedBuilding[] row = Row;
            for (int i = 0; i < row.Length; i++)
            {
                string name = OwnedBuilding.IsEmpty(row[i]) ? "빈 칸" : $"{row[i].Definition.DisplayName} {row[i].Rarity.DisplayName()}";
                if (Button($"{i + 1} {name}", 154f, i == _index))
                    _index = i;
            }
            NewLine();
            if (Button("등급 올리기", 100f)) Rank(1);
            if (Button("등급 내리기", 100f)) Rank(-1);
            if (Button("칸 비우기", 100f)) ClearSlot();
            if (Button("전부 비우기", 100f)) ClearRow();
            if (Button("내 탑 층 +1", 100f)) AddFloor();
            if (Button("골드 +100", 100f)) RunState.CheatAddGold(100);
            NewLine();
            foreach (BuildingDefinition definition in RunState.Pool)
                if (Button(definition.DisplayName, 155f))
                    Add(definition);

            Label("맵 — 고르면 「무작위」로 풀 때까지 매 라운드 그 맵 (상점에서 누르면 상점을 다시 불러온다)");
            if (Button("무작위", 86f, RunState.CheatMap == null))
                SetMap(null);
            foreach (MapDefinition map in BattleManager.Config.Maps)
                if (Button(MapName(map), 86f, RunState.CheatMap == map))
                    SetMap(map);

            Label("라운드 — 적 보드 · 성 체력 · 진열 등급이 따라간다 (상점에서만 · 골드와 새 진열은 주지 않는다)");
            if (Button("-5", 60f)) AddRound(-5);
            if (Button("-1", 60f)) AddRound(-1);
            if (Button("+1", 60f)) AddRound(1);
            if (Button("+5", 60f)) AddRound(5);

            Label("전투");
            if (Button("데스매치로", 100f)) BattleManager.CheatSkipTo(BattleManager.DeathmatchAt);
            if (Button("기물 정지로", 100f)) BattleManager.CheatSkipTo(BattleManager.BoardStopAt);
            foreach (float speed in Speeds)
                if (Button(speed > 0f ? $"배속 ×{speed:0}" : "배속 끔", 76f, _speed == speed))
                    SetSpeed(speed);
            if (Button("승리로 끝", 86f)) BattleManager.CheatFinish(true);
            if (Button("패배로 끝", 86f)) BattleManager.CheatFinish(false);

            if (_note.Length > 0)
                Label(_note);
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

        private void Label(string text)
        {
            NewLine();
            GUI.Label(new Rect(_x, _y, Width - Pad * 2f, RowHeight), text, _label);
            _y += RowHeight;
        }

        private bool Button(string text, float width, bool lit = false)
        {
            if (_x + width > _panel.xMax - Pad)
                NewLine();
            var rect = new Rect(_x, _y, width, RowHeight - 2f);
            _x += width + 2f;
            GUI.color = lit ? Color.yellow : Color.white;
            GUI.Box(rect, text, _button);
            GUI.color = Color.white;
            if (!_click || !rect.Contains(_pointer))
                return false;
            _click = false;   // 한 번 누르면 버튼 하나만 — 눌러서 줄이 바뀌어도 다음 버튼이 같이 눌리지 않게
            return true;
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

        // 지금 적 탑의 사본 — 라운드 표(에셋)의 기물을 그대로 고치면 에셋이 바뀐다
        private static OwnedBuilding[] CopyEnemyBoard()
        {
            var copy = new OwnedBuilding[RunState.MaxFloors];
            BoardRunner board = BoardRunner.For(Team.Enemy);
            for (int i = 0; board != null && i < board.Field.Length && i < copy.Length; i++)
                if (!OwnedBuilding.IsEmpty(board.Field[i]))
                    copy[i] = Copy(board.Field[i], board.Field[i].Upgrades);
            return copy;
        }

        private static OwnedBuilding Copy(OwnedBuilding building, int upgrades)
        {
            var copy = new OwnedBuilding(building.Definition);
            for (int i = 0; i < upgrades; i++)
                copy.Upgrade();
            copy.AddStacks(building.Stacks);
            return copy;
        }

        private bool CanEdit()
        {
            _note = BattleManager.InShop ? "" : "보드·라운드는 상점에서만 고친다";
            return BattleManager.InShop;
        }

        public void Add(BuildingDefinition definition)
        {
            if (!CanEdit())
                return;
            OwnedBuilding[] row = Row;
            int slot = System.Array.FindIndex(row, OwnedBuilding.IsEmpty);
            if (slot < 0)
            {
                _note = "빈 칸이 없다";
                return;
            }
            row[slot] = new OwnedBuilding(definition);
            _index = slot;
            Changed();
        }

        // 등급만 바꾼다(합쳐진 것처럼 — 스택은 그대로)
        public void Rank(int delta)
        {
            OwnedBuilding[] row = Row;
            if (!CanEdit() || _index >= row.Length || OwnedBuilding.IsEmpty(row[_index]))
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
            if (!CanEdit() || _index >= row.Length)
                return;
            row[_index] = null;
            Changed();
        }

        public void ClearRow()
        {
            if (!CanEdit())
                return;
            System.Array.Clear(Row, 0, Row.Length);
            Changed();
        }

        public void AddFloor()
        {
            if (CanEdit())
                RunState.CheatAddFloor();
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

        // 켜면 다음 전투부터 적 탑 = 편집본(끌 때까지 매 라운드). 상점에서는 바로 적 탑에 올린다
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
            BoardRunner board = BoardRunner.For(Team.Enemy);
            if (BattleManager.InShop && board != null)
                board.Load(OwnedBuilding.Compacted(field ?? BattleManager.Config.EnemyRoundAt(RunState.Round).Field));
        }

        // ── 맵 · 라운드 · 전투

        public void SetMap(MapDefinition map)
        {
            RunState.CheatSetMap(map);
            if (map != null && BattleManager.InShop)
                Reload();
        }

        public void AddRound(int delta)
        {
            if (!CanEdit())
                return;
            RunState.CheatSetRound(Mathf.Max(0, RunState.Round + delta));
            if (!_enemyOn)
                _enemy = null;   // 새 라운드의 적 탑에서 다시 뜬다
            Reload();
        }

        // 땅 모양 · 적 탑 · 성 체력은 씬을 불러올 때 정해진다 — 상점을 다시 불러온다(런 상태는 그대로)
        private static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        public void SetSpeed(float speed)
        {
            _speed = speed;
            if (BattleManager.Fighting)
                Time.timeScale = speed > 0f ? speed : SpeedButton.Current;
        }
    }
}

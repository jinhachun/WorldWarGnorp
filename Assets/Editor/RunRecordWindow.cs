using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 개발 확인용 — 내 런 기록(Run/RunRecord)을 사람이 읽게 푼다: 왼쪽 = 기록 파일 목록, 오른쪽 = 고른 기록의 턴마다 층 · 보관함 · 업그레이드(사용자 10/23).
    // 읽기만 한다. 다른 게임 버전의 기록도 그 기록의 버전으로 읽어서 보여 준다(유령으로는 안 쓰인다)
    internal class RunRecordWindow : EditorWindow
    {
        private class Entry
        {
            public string Path;
            public string Label;
            public RunRecord.Loaded Loaded;
            public string Error;
        }

        // 머리 줄에서 버전만 꺼낸다
        [System.Serializable]
        private class VersionLine
        {
            public string GameVersion;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _selected = -1;
        private Vector2 _listScroll, _bodyScroll;
        private readonly Dictionary<int, bool> _open = new Dictionary<int, bool>();

        [MenuItem("GnorpWar/런 기록 보기")]
        private static void Open() => GetWindow<RunRecordWindow>("런 기록").Reload();

        private void OnEnable() => Reload();

        private void Reload()
        {
            _entries.Clear();
            _selected = -1;
            _open.Clear();
            if (!Directory.Exists(RunRecord.Folder))
                return;
            AssetIds ids = Load<AssetIds>();
            BattleConfig config = Load<BattleConfig>();
            foreach (string path in Directory.GetFiles(RunRecord.Folder, "*.json", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTime))
            {
                var entry = new Entry { Path = path };
                try
                {
                    string version = JsonUtility.FromJson<VersionLine>(File.ReadLines(path).First()).GameVersion;
                    entry.Loaded = RunRecord.Read(path, ids, config, version);
                }
                catch (System.Exception e)
                {
                    entry.Error = e.Message;
                }
                string relative = path.Substring(RunRecord.Folder.Length).TrimStart('\\', '/');
                entry.Label = entry.Loaded == null ? $"⚠ {relative}"
                    : $"{Name(entry.Loaded)} · {entry.Loaded.Nation.DisplayName()} · 턴 {entry.Loaded.LastTurn} · {EndingText(entry.Loaded.Ending)}\n{relative}";
                _entries.Add(entry);
            }
        }

        private static T Load<T>() where T : ScriptableObject
        {
            string guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static string Name(RunRecord.Loaded loaded) => string.IsNullOrEmpty(loaded.Nickname) ? "(닉네임 없음)" : loaded.Nickname;

        private static string EndingText(RunRecord.Ending ending) =>
            ending == RunRecord.Ending.Unified ? "통일" : ending == RunRecord.Ending.GameOver ? "게임 오버" : "중간에 끔";

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Reload();
                if (GUILayout.Button("폴더 열기", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    Directory.CreateDirectory(RunRecord.Folder);
                    EditorUtility.OpenWithDefaultApp(RunRecord.Folder);
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"기록 {_entries.Count}개", EditorStyles.miniLabel);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawBody();
            }
        }

        private void DrawList()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.Width(300)))
            {
                _listScroll = scroll.scrollPosition;
                if (_entries.Count == 0)
                    EditorGUILayout.HelpBox("기록이 없습니다. 런을 한 턴 이상 하면 생깁니다.", MessageType.Info);
                for (int i = 0; i < _entries.Count; i++)
                {
                    var style = new GUIStyle(i == _selected ? EditorStyles.helpBox : EditorStyles.label) { wordWrap = true, richText = false };
                    if (GUILayout.Button(_entries[i].Label, style))
                    {
                        _selected = i;
                        _open.Clear();
                    }
                }
            }
        }

        private void DrawBody()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_bodyScroll))
            {
                _bodyScroll = scroll.scrollPosition;
                if (_selected < 0 || _selected >= _entries.Count)
                {
                    EditorGUILayout.HelpBox("왼쪽에서 기록을 고르세요.", MessageType.None);
                    return;
                }
                Entry entry = _entries[_selected];
                if (entry.Loaded == null)
                {
                    EditorGUILayout.HelpBox($"읽지 못한 기록입니다 — {entry.Error}", MessageType.Warning);
                    return;
                }
                RunRecord.Loaded loaded = entry.Loaded;
                EditorGUILayout.LabelField(Name(loaded), EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"국가 {loaded.Nation.DisplayName()} · 게임 버전 {loaded.GameVersion} · 시작 {loaded.StartedAt}");
                EditorGUILayout.LabelField($"턴 {loaded.Turns.Count}줄(마지막 턴 {loaded.LastTurn}) · {EndingText(loaded.Ending)}");
                if (loaded.GameVersion != Application.version)
                    EditorGUILayout.HelpBox($"지금 게임 버전({Application.version})과 달라 유령 영주로는 쓰이지 않습니다.", MessageType.Info);
                EditorGUILayout.Space();

                AssetIds ids = Load<AssetIds>();
                BattleConfig config = Load<BattleConfig>();
                for (int i = 0; i < loaded.Snapshots.Count; i++)
                {
                    TowerSnapshot snapshot = TowerSnapshot.FromJson(loaded.Snapshots[i], Team.Enemy, ids, config, loaded.GameVersion);
                    TowerState tower = snapshot.Tower;
                    _open.TryGetValue(i, out bool open);
                    _open[i] = EditorGUILayout.Foldout(open, $"턴 {snapshot.Turn} — 목숨 {snapshot.Lives} · {tower.Field.Length}층 · 기물 {tower.Field.Count(b => b != null)} · 업그레이드 {tower.Upgrades.Count}", true);
                    if (!_open[i])
                        continue;
                    using (new EditorGUI.IndentLevelScope())
                        DrawTower(tower);
                }
            }
        }

        private static void DrawTower(TowerState tower)
        {
            EditorGUILayout.LabelField("탑(위층부터)", EditorStyles.miniBoldLabel);
            for (int floor = tower.Field.Length - 1; floor >= 0; floor--)
                EditorGUILayout.LabelField($"{floor + 1}층", BuildingText(tower.Field[floor]));
            if (tower.Storage.Any(b => b != null))
            {
                EditorGUILayout.LabelField("보관함", EditorStyles.miniBoldLabel);
                foreach (OwnedBuilding building in tower.Storage.Where(b => b != null))
                    EditorGUILayout.LabelField(" ", BuildingText(building));
            }
            if (tower.Upgrades.Count > 0)
            {
                EditorGUILayout.LabelField("업그레이드(얻은 순서)", EditorStyles.miniBoldLabel);
                foreach (UpgradeGain gain in tower.Upgrades)
                    EditorGUILayout.LabelField(" ", $"{gain.Definition.DisplayName} ({gain.Rarity.DisplayName()})");
            }
            EditorGUILayout.Space();
        }

        private static string BuildingText(OwnedBuilding building)
        {
            if (building == null)
                return "—";
            string text = $"{building.Definition.DisplayName} ({building.Rarity.DisplayName()} · {building.Definition.Nation.DisplayName()})";
            if (building.Stacks > 0)
                text += $" · 스택 {building.Stacks}";
            return text;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GnorpWar
{
    // 내 런 기록(아웃게임기획 §9-0 — 비동기 멀티의 유령 영주가 될 줄). 런 하나 = 파일 하나(<Folder>/<런 id>.json), 한 줄에 JSON 하나:
    //   첫 줄 = 머리(형식 · 런 id · 게임 버전 · 국가 · 시작 시각) → 턴마다 내 탑 스냅샷 한 줄(TowerSnapshot — 그 턴에 싸우러 나간 탑, 쉰 턴도) → 런이 끝나면(통일 · 게임 오버) 끝 줄.
    //   끝 줄이 없으면 중간 이탈(게임을 껐거나 새 런). 턴마다 덧붙여 쓰므로 게임이 꺼져도 그때까지는 남는다. 턴이 하나도 없으면 파일을 만들지 않는다.
    // 쓰다 실패하면(등록 안 된 에셋 · 디스크) 경고 로그를 남기고 그 런의 기록을 멈춘다 — 게임은 멈추지 않는다
    public class RunRecord
    {
        public enum Ending { Abandoned, Unified, GameOver }

        // 파일 모양이 바뀌면 올린다 — 다른 값의 파일은 읽지 않는다
        public const int CurrentFormat = 1;

        public static string Folder => Path.Combine(Application.persistentDataPath, "Ghosts");

        [System.Serializable]
        private class Line
        {
            public string Kind;
        }

        [System.Serializable]
        private class Header
        {
            public string Kind = HeaderKind;
            public int Format;
            public string RunId;
            public string GameVersion;
            public BuildingNation Nation;
            public string StartedAt;
            // 플레이어 닉네임(RunState.PlayerName — 없으면 "")
            public string Nickname;
        }

        [System.Serializable]
        private class EndLine
        {
            public string Kind = EndKind;
            public Ending Ending;
            public int Turn;
            public string EndedAt;
        }

        private const string HeaderKind = "Run";
        private const string EndKind = "End";

        private readonly AssetIds _ids;
        private readonly string _runId;
        private readonly string _startedAt;
        private int _lastTurn = -1;
        private bool _stopped;
        private bool _ended;

        public string FilePath { get; }

        public RunRecord(string folder, AssetIds ids)
        {
            _ids = ids;
            System.DateTime now = System.DateTime.Now;
            _runId = now.ToString("yyyyMMdd-HHmmss") + "-" + new System.Random().Next(0x10000).ToString("x4");
            _startedAt = now.ToString("o");
            FilePath = Path.Combine(folder, _runId + ".json");
        }

        // 그 턴의 내 탑을 한 줄 덧붙인다 — 같은 턴은 한 번만(처음 것). 국가 · 닉네임은 첫 줄(머리)에 그때 값으로
        public void Add(TowerState tower, BuildingNation nation, Color lordColor, int lives, int turn, string nickname = "")
        {
            if (_stopped || _ended || turn == _lastTurn)
                return;
            try
            {
                if (_ids == null)
                    throw new TowerSnapshot.InvalidException("BattleConfig에 에셋 ID 등록부(AssetIds)가 비어 있음");
                string json = TowerSnapshot.ToJson(tower, nation, lordColor, lives, turn, _ids);
                var lines = new List<string>();
                if (_lastTurn < 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    lines.Add(JsonUtility.ToJson(new Header { Format = CurrentFormat, RunId = _runId, GameVersion = Application.version, Nation = nation, StartedAt = _startedAt, Nickname = nickname ?? "" }));
                }
                lines.Add(json);
                File.AppendAllLines(FilePath, lines);
                _lastTurn = turn;
            }
            catch (System.Exception e) when (e is TowerSnapshot.InvalidException || e is IOException || e is System.UnauthorizedAccessException)
            {
                _stopped = true;
                Debug.LogWarning($"[RunRecord] 런 기록을 멈춤(턴 {turn}) — {e.Message} · {FilePath}");
            }
        }

        // 런이 끝났다(통일 · 게임 오버) — 끝 줄. 적힌 턴이 없으면 아무것도 하지 않는다
        public void End(Ending ending, int turn)
        {
            if (_stopped || _ended || _lastTurn < 0)
                return;
            _ended = true;
            try
            {
                File.AppendAllLines(FilePath, new[] { JsonUtility.ToJson(new EndLine { Ending = ending, Turn = turn, EndedAt = System.DateTime.Now.ToString("o") }) });
            }
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
            {
                Debug.LogWarning($"[RunRecord] 끝 표시를 못 씀 — {e.Message} · {FilePath}");
            }
        }

        // ── 읽기(유령 영주가 쓴다 — 읽기만)

        public class Loaded
        {
            public string FilePath;
            public string RunId;
            public string GameVersion;
            public BuildingNation Nation;
            public string StartedAt;
            public string Nickname;
            public Ending Ending;
            // 턴 순서대로의 스냅샷 JSON(TowerSnapshot.FromJson으로 세운다) · 그 줄의 턴
            public List<string> Snapshots = new List<string>();
            public List<int> Turns = new List<int>();

            public int LastTurn => Turns.Count > 0 ? Turns[Turns.Count - 1] : 0;

            // 내 턴 turn에 이 기록이 세울 탑 = 그 턴 이하의 마지막 스냅샷(기록이 일찍 끝났으면 마지막에 멈춘다 · 첫 턴보다 앞이면 첫 스냅샷)
            public string SnapshotAt(int turn)
            {
                int index = 0;
                for (int i = 0; i < Turns.Count; i++)
                    if (Turns[i] <= turn)
                        index = i;
                return Snapshots[index];
            }
        }

        // 파일 하나를 읽고 줄마다 FromJson이 되는지까지 확인한다. 안 되면 InvalidException(머리 · 버전 · 스냅샷 하나라도)
        public static Loaded Read(string path, AssetIds ids, BattleConfig config, string expectedVersion = null)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
            {
                throw new TowerSnapshot.InvalidException($"런 기록을 못 읽음 — {e.Message}");
            }
            if (lines.Length == 0 || Parse<Line>(lines[0])?.Kind != HeaderKind)
                throw new TowerSnapshot.InvalidException("런 기록의 첫 줄이 머리가 아님");
            Header header = Parse<Header>(lines[0]);
            if (header.Format != CurrentFormat)
                throw new TowerSnapshot.InvalidException($"런 기록 형식 {header.Format} — 지금 형식은 {CurrentFormat}");
            string version = expectedVersion ?? Application.version;
            if (header.GameVersion != version)
                throw new TowerSnapshot.InvalidException($"게임 버전 「{header.GameVersion}」의 런 기록 — 지금 버전은 「{version}」");

            var loaded = new Loaded { FilePath = path, RunId = header.RunId, GameVersion = header.GameVersion, Nation = header.Nation, StartedAt = header.StartedAt, Nickname = header.Nickname ?? "", Ending = Ending.Abandoned };
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                if (Parse<Line>(lines[i])?.Kind == EndKind)
                {
                    loaded.Ending = Parse<EndLine>(lines[i]).Ending;
                    break;
                }
                TowerSnapshot snapshot = TowerSnapshot.FromJson(lines[i], Team.Enemy, ids, config, version);
                if (loaded.Turns.Count > 0 && snapshot.Turn <= loaded.LastTurn)
                    throw new TowerSnapshot.InvalidException($"런 기록의 턴이 거꾸로 감({loaded.LastTurn} → {snapshot.Turn})");
                loaded.Snapshots.Add(lines[i]);
                loaded.Turns.Add(snapshot.Turn);
            }
            if (loaded.Snapshots.Count == 0)
                throw new TowerSnapshot.InvalidException("런 기록에 턴이 없음");
            return loaded;
        }

        // 폴더의 기록 전부 — 읽히는 것(같은 형식 · 같은 게임 버전 · 줄마다 FromJson이 되는 것)만, 오래 버틴(마지막 턴이 큰) 것부터. 안 읽히는 파일은 경고 로그를 남기고 건너뛴다.
        // except = 빼고 읽을 파일(지금 런의 기록)
        public static List<Loaded> ReadAll(string folder, AssetIds ids, BattleConfig config, string except = null)
        {
            var all = new List<Loaded>();
            if (ids == null)
            {
                Debug.LogWarning("[RunRecord] BattleConfig에 에셋 ID 등록부(AssetIds)가 비어 있어 기록을 읽지 않음");
                return all;
            }
            if (!Directory.Exists(folder))
                return all;
            foreach (string path in Directory.GetFiles(folder, "*.json"))
            {
                if (except != null && Path.GetFullPath(path) == Path.GetFullPath(except))
                    continue;
                try
                {
                    all.Add(Read(path, ids, config));
                }
                catch (TowerSnapshot.InvalidException e)
                {
                    Debug.LogWarning($"[RunRecord] 기록을 건너뜀 — {e.Message} · {path}");
                }
            }
            all.Sort((a, b) => b.LastTurn != a.LastTurn ? b.LastTurn.CompareTo(a.LastTurn) : string.CompareOrdinal(a.RunId, b.RunId));
            return all;
        }

        private static T Parse<T>(string json) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (System.ArgumentException e)
            {
                throw new TowerSnapshot.InvalidException($"런 기록의 줄을 읽지 못함 — {e.Message}");
            }
        }
    }
}

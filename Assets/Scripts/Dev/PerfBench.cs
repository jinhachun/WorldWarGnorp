using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GnorpWar
{
    // 측정 전용 — 유닛 N마리(양 진영 합) 전투를 만들어 놓고 프레임을 구간별로 잰다. 게임 코드는 이걸 모른다.
    // 켜는 법: 빌드 = 실행 인자 `-perfbench 250,500,1000` · 에디터 = 플레이 중 PerfBench.Run("250,500,1000").
    // 흐름: 지도 씬이면 침략 가능한 첫 땅으로 Invade → 전투 씬. 전투가 열리면 단계마다 목표 수까지 진영당 절반씩
    // 성문에서 소환(BoardRunner.Summon — 게임과 같은 입구)하고 죽은 만큼 채운다. 본진 체력은 매 프레임 최대로, 전투 경과 시간은 고정(데스매치·가속이 안 오게).
    // 목표의 95%에 닿은 뒤 Warmup초 지나서 Measure초 동안 잰다. 결과는 콘솔 `[PerfBench]`와 persistentDataPath/perfbench.txt. 빌드면 끝나고 종료.
    public class PerfBench : MonoBehaviour
    {
        private const float Warmup = 8f;
        private const float Measure = 15f;
        private const float FixedElapsed = 20f;
        private const int SpawnPerFramePerTeam = 2;
        private const float StageTimeout = 90f;

        // 재는 구간(Unity 내부 표시 이름). 없으면 「?」
        private static readonly (string name, ProfilerCategory cat)[] Markers =
        {
            ("Main Thread", ProfilerCategory.Internal),
            ("FixedBehaviourUpdate", ProfilerCategory.Scripts),
            ("Physics2D.Simulate", ProfilerCategory.Physics),
            ("Physics2D.FindNewContacts", ProfilerCategory.Physics),
            ("Physics2D.Step", ProfilerCategory.Physics),
            ("Physics2D.UpdateTransforms", ProfilerCategory.Physics),
            ("Physics2D.SyncTransformChanges", ProfilerCategory.Physics),
            ("BehaviourUpdate", ProfilerCategory.Scripts),
            ("PreLateUpdate.ScriptRunBehaviourLateUpdate", ProfilerCategory.Scripts),
            ("Unit.FixedUpdate() [Invoke]", ProfilerCategory.Scripts),
            ("Unit.Update() [Invoke]", ProfilerCategory.Scripts),
            ("Unit.Contacts", ProfilerCategory.Scripts),
            ("Unit.Scan", ProfilerCategory.Scripts),
            ("Unit.Support", ProfilerCategory.Scripts),
            ("Unit.Walk", ProfilerCategory.Scripts),
            ("Unit.Climb", ProfilerCategory.Scripts),
            ("Projectile.Update() [Invoke]", ProfilerCategory.Scripts),
            ("Projectile.FixedUpdate() [Invoke]", ProfilerCategory.Scripts),
            ("Base.FixedUpdate() [Invoke]", ProfilerCategory.Scripts),
            ("FxDirector.Update() [Invoke]", ProfilerCategory.Scripts),
            ("FxDirector.LateUpdate() [Invoke]", ProfilerCategory.Scripts),
            ("CorpseFx.LateUpdate() [Invoke]", ProfilerCategory.Scripts),
            ("BattleManager.Update() [Invoke]", ProfilerCategory.Scripts),
            ("BoardRunner.Update() [Invoke]", ProfilerCategory.Scripts),
            ("CheatPanel.OnGUI() [Invoke]", ProfilerCategory.Scripts),
            ("PostLateUpdate.FinishFrameRendering", ProfilerCategory.Render),
            ("PostLateUpdate.PlayerUpdateCanvases", ProfilerCategory.Gui),
            ("PostLateUpdate.PlayerEmitCanvasGeometry", ProfilerCategory.Gui),
            ("Gfx.WaitForPresentOnGfxThread", ProfilerCategory.Render),
            ("GC.Collect", ProfilerCategory.Memory),
        };
        // 프레임당 개수 값
        private static readonly (string name, ProfilerCategory cat)[] Counters =
        {
            ("Batches Count", ProfilerCategory.Render),
            ("SetPass Calls Count", ProfilerCategory.Render),
            ("Draw Calls Count", ProfilerCategory.Render),
            ("GC Allocated In Frame", ProfilerCategory.Memory),
        };

        private static PerfBench _instance;

        private int[] _targets;
        private int _stage;
        private float _reachedAt = -1f;
        private float _stageStart;
        private bool _measuring;
        private float _nextCount;
        private int _allies, _enemies, _projectiles;
        private readonly List<UnitDefinition> _mix = new List<UnitDefinition>();
        private readonly List<float> _frames = new List<float>();
        private ProfilerRecorder[] _markerRec;
        private ProfilerRecorder[] _counterRec;
        private double[] _markerSum;
        private double[] _counterSum;
        private int _fixedSteps;
        private int _spawnCursor;
        private readonly StringBuilder _report = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-perfspeed" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float speed))
                    _speed = speed;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-perfbench")
                    Run(args[i + 1]);
        }

        // 전투 배속(-perfspeed · 게임 배속 버튼은 ×1·×2·×4)
        private static float _speed = 1f;

        public static string Run(string targets, float speed = 0f)
        {
            if (speed > 0f)
                _speed = speed;
            if (_instance != null)
                return "already running";
            var list = new List<int>();
            foreach (string s in targets.Split(','))
                if (int.TryParse(s.Trim(), out int n) && n > 0)
                    list.Add(n);
            var host = new GameObject("PerfBench");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PerfBench>();
            _instance._targets = list.ToArray();
            return "started " + targets;
        }

        public static string Status => _instance == null ? "idle" : $"stage {_instance._stage}/{_instance._targets.Length} units {_instance._allies}+{_instance._enemies} measuring={_instance._measuring}";
        public static string LastReport { get; private set; } = "";

        private void OnEnable()
        {
            _markerRec = new ProfilerRecorder[Markers.Length];
            _markerSum = new double[Markers.Length];
            for (int i = 0; i < Markers.Length; i++)
                _markerRec[i] = ProfilerRecorder.StartNew(Markers[i].cat, Markers[i].name);
            _counterRec = new ProfilerRecorder[Counters.Length];
            _counterSum = new double[Counters.Length];
            for (int i = 0; i < Counters.Length; i++)
                _counterRec[i] = ProfilerRecorder.StartNew(Counters[i].cat, Counters[i].name);
        }

        private void OnDisable()
        {
            foreach (ProfilerRecorder r in _markerRec) r.Dispose();
            foreach (ProfilerRecorder r in _counterRec) r.Dispose();
        }

        private void FixedUpdate()
        {
            if (_measuring)
                _fixedSteps++;
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == WorldManager.SceneName)
            {
                if (RunState.Started && RunState.Phase != RunState.RunPhase.Battle)
                {
                    List<int> regions = RunState.InvadableRegions();
                    if (regions.Count > 0)
                    {
                        RunState.Invade(regions[0]);
                        SceneManager.LoadScene("Battle");
                    }
                }
                return;
            }
            if (!BattleManager.Fighting)
                return;

            Time.timeScale = _speed;
            BattleManager.CheatSkipTo(FixedElapsed);
            foreach (Team team in new[] { Team.Ally, Team.Enemy })
            {
                Base b = Base.For(team);
                if (b != null)
                    b.CheatSetHp(b.MaxHp);
            }
            if (_mix.Count == 0)
                BuildMix();

            if (Time.unscaledTime >= _nextCount)
            {
                _nextCount = Time.unscaledTime + 0.25f;
                _allies = _enemies = 0;
                foreach (Unit u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                    if (u.IsAlive)
                    {
                        if (u.Team == Team.Ally) _allies++; else _enemies++;
                    }
                _projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
            }

            int target = _targets[_stage];
            // 채우기 — 진영당 절반까지
            if (_allies < target / 2)
                Spawn(Team.Ally);
            if (_enemies < target / 2)
                Spawn(Team.Enemy);

            if (_stageStart == 0f)
                _stageStart = Time.unscaledTime;
            if (_reachedAt < 0f && (_allies + _enemies >= target * 0.95f || Time.unscaledTime - _stageStart > StageTimeout))
                _reachedAt = Time.unscaledTime;
            if (_reachedAt < 0f)
                return;

            if (!_measuring && Time.unscaledTime - _reachedAt >= Warmup)
            {
                _measuring = true;
                _frames.Clear();
                _fixedSteps = 0;
                System.Array.Clear(_markerSum, 0, _markerSum.Length);
                System.Array.Clear(_counterSum, 0, _counterSum.Length);
                _measureStart = Time.unscaledTime;
                _unitSamples = 0;
                _unitSum = 0;
                _projSum = 0;
                return;
            }
            if (!_measuring)
                return;

            _frames.Add(Time.unscaledDeltaTime * 1000f);
            for (int i = 0; i < _markerRec.Length; i++)
                if (_markerRec[i].Valid)
                    _markerSum[i] += _markerRec[i].LastValue * 1e-6;
            for (int i = 0; i < _counterRec.Length; i++)
                if (_counterRec[i].Valid)
                    _counterSum[i] += _counterRec[i].LastValue;
            _unitSamples++;
            _unitSum += _allies + _enemies;
            _projSum += _projectiles;

            if (Time.unscaledTime - _measureStart >= Measure)
                FinishStage();
        }

        private float _measureStart;
        private int _unitSamples;
        private long _unitSum, _projSum;

        private void BuildMix()
        {
            // 섞는 비율 — 칼 4 · 활 3 · 방패 1.5 · 기사 1 · 사제 0.5 (10칸 중)
            string[] names = { "Unit_Melee", "Unit_Melee", "Unit_Melee", "Unit_Melee", "Unit_Ranged", "Unit_Ranged", "Unit_Ranged", "Unit_Tank", "Unit_Knight", "Unit_Melee", "Unit_Ranged", "Unit_Tank", "Unit_Melee", "Unit_Ranged", "Unit_Melee", "Unit_Ranged", "Unit_Tank", "Unit_Knight", "Unit_Knight", "Unit_Priest" };
            var byName = new Dictionary<string, UnitDefinition>();
            foreach (BuildingDefinition b in BattleManager.Config.ShopPool)
                if (b != null && b.Unit != null)
                    byName[b.Unit.name] = b.Unit;
            foreach (string n in names)
                if (byName.TryGetValue(n, out UnitDefinition d))
                    _mix.Add(d);
            if (_mix.Count == 0)
                _mix.AddRange(byName.Values);
        }

        private void Spawn(Team team)
        {
            BoardRunner board = BoardRunner.For(team);
            if (board == null || _mix.Count == 0)
                return;
            for (int i = 0; i < SpawnPerFramePerTeam; i++)
                board.Summon(_mix[_spawnCursor++ % _mix.Count], 1, -1);
            // 다음 세기 전까지 넘치게 뽑지 않게
            if (team == Team.Ally) _allies += SpawnPerFramePerTeam; else _enemies += SpawnPerFramePerTeam;
        }

        // 이 실행에서 쓸 수 있는 구간 이름 중 관심 있는 것 — 처음 한 번 파일 끝에 붙인다(이름을 맞추려고)
        private static string AvailableMarkers()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var names = new List<string>();
            string[] keys = { "Unit", "Projectile", "Fx", "Corpse", "Base.", "Board", "Render", "Camera", "Batch", "Sprite", "Canvas", "Physics2D", "Draw", "SetPass", "Invoke", "GUI", "Gfx", "Culling" };
            foreach (ProfilerRecorderHandle h in handles)
            {
                string n = ProfilerRecorderHandle.GetDescription(h).Name;
                foreach (string k in keys)
                    if (n.Contains(k))
                    {
                        names.Add(n);
                        break;
                    }
            }
            names.Sort();
            return string.Join("\n", names);
        }

        private void FinishStage()
        {
            _measuring = false;
            var sorted = new List<float>(_frames);
            sorted.Sort();
            float avg = 0f;
            foreach (float f in _frames) avg += f;
            avg /= Mathf.Max(1, _frames.Count);
            float p99 = sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Count * 0.99f) - 1, 0, sorted.Count - 1)];
            float worst = sorted[sorted.Count - 1];
            int n = _frames.Count;
            var line = new StringBuilder();
            line.Append($"[PerfBench] target {_targets[_stage]} · speed ×{_speed:0} · units avg {(float)_unitSum / Mathf.Max(1, _unitSamples):F0} · projectiles avg {(float)_projSum / Mathf.Max(1, _unitSamples):F0}");
            line.Append($" · frames {n} · frame avg {avg:F2}ms · p99 {p99:F2}ms · max {worst:F2}ms · physSteps/frame {(float)_fixedSteps / Mathf.Max(1, n):F2}");
            for (int i = 0; i < Markers.Length; i++)
                line.Append(_markerRec[i].Valid ? $"\n  {Markers[i].name} {_markerSum[i] / n:F2}ms" : $"\n  {Markers[i].name} ?");
            for (int i = 0; i < Counters.Length; i++)
                line.Append(_counterRec[i].Valid ? $"\n  {Counters[i].name} {_counterSum[i] / n:F0}" : $"\n  {Counters[i].name} ?");
            Debug.Log(line.ToString());
            _report.AppendLine(line.ToString());
            LastReport = _report.ToString();

            _stage++;
            _reachedAt = -1f;
            _stageStart = 0f;
            if (_stage < _targets.Length)
                return;
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "perfbench.txt"), _report + "\n--- markers ---\n" + AvailableMarkers());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PerfBench] write failed " + e.Message);
            }
            Debug.Log("[PerfBench] done");
            _instance = null;
            Destroy(gameObject);
            if (!Application.isEditor)
                Application.Quit();
        }
    }
}

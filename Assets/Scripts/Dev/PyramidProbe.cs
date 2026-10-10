using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 측정 전용 — 여러 판을 차례로 돌려 Captures/Pile/_front.txt에 한 줄씩 적는다. 플레이 중 PyramidBatch.Run("이름,보드,끝낼 시각;…", "보드=구성;…")
    // 보드 = "Sword Bow^1 …"(아래층부터, ^n = 등급업 횟수) — 양 진영 같은 보드, 기본 맵(라운드 0)
    public class PyramidBatch : MonoBehaviour
    {
        public static string Status = "idle";

        public static void Run(string jobs, string boards)
        {
            var host = new GameObject("PyramidBatch");
            DontDestroyOnLoad(host);
            host.AddComponent<PyramidBatch>().StartCoroutine(Loop(jobs, boards, host));
        }

        private static OwnedBuilding[] Board(string spec)
        {
            string[] parts = spec.Trim().Split(' ');
            var field = new OwnedBuilding[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string[] p = parts[i].Split('^');
                BuildingDefinition def = null;
                foreach (BuildingDefinition d in Resources.FindObjectsOfTypeAll<BuildingDefinition>())
                    if (d.name == "Building_" + p[0]) def = d;
                field[i] = new OwnedBuilding(def);
                for (int k = 0; p.Length > 1 && k < int.Parse(p[1]); k++) field[i].Upgrade();
            }
            return field;
        }

        private static System.Collections.IEnumerator Loop(string jobs, string boards, GameObject host)
        {
            var boardSpecs = new Dictionary<string, string>();
            foreach (string b in boards.Split(';')) { string[] kv = b.Split('='); boardSpecs[kv[0].Trim()] = kv[1]; }
            const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
            typeof(SpeedButton).GetField("_index", Any).SetValue(null, 2);   // ×4
            foreach (string job in jobs.Split(';'))
            {
                string[] j = job.Trim().Split(',');
                float cut = float.Parse(j[2]);
                Status = "running " + j[0];
                RunState.StartNew(BattleManager.Config);
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                yield return null;
                yield return null;
                typeof(RunState).GetProperty("Field").SetValue(null, Board(boardSpecs[j[1]]));
                var bm = FindFirstObjectByType<BattleManager>();
                ((BoardRunner)typeof(BattleManager).GetField("_enemyBoard", Any).GetValue(bm)).Load(Board(boardSpecs[j[1]]));
                Unit.DevDamage[0] = Unit.DevDamage[1] = 0;
                var probe = bm.gameObject.AddComponent<PyramidProbe>();
                probe.Shot = j[0];
                probe.ShotAt = new[] { 60f, 90f, 120f };
                yield return null;
                bm.StartBattle();
                float real = Time.realtimeSinceStartup, last = 0f;
                while (BattleManager.Fighting && BattleManager.Elapsed < cut)
                {
                    last = BattleManager.Elapsed;
                    yield return null;
                }
                double m = Unit.DevDamage[0], r = Unit.DevDamage[1];
                System.IO.File.AppendAllText("Captures/Pile/_front.txt",
                    $"{j[0]} board {j[1]} map {RunState.Map.name} end {last:F0} ({(BattleManager.Fighting ? "cut" : "over")}) real {Time.realtimeSinceStartup - real:F0}s | dmg melee {100 * m / System.Math.Max(1, m + r):F0}% | {probe.Front()} | {probe.Melee()} | {probe.Short()}\n");
            }
            Status = "done";
            Time.timeScale = 0f;
            Destroy(host);
        }
    }

    // 측정 전용 — 계단 피라미드·전선. 게임 1초마다 표본을 쌓고, Summary()가 평균을 돌려준다(콘솔에는 안 남긴다).
    // 씬에는 두지 않는다. 플레이 중 Battle에 붙이고, 전투 경과 From초부터 잰다
    [DefaultExecutionOrder(-1000)]
    public class PyramidProbe : MonoBehaviour
    {
        public float From = 30f;
        public float Until = 150f;
        public string Shot;          // 캡처 파일 머리말(Captures/Pile/) — 비우면 안 찍는다
        public float[] ShotAt = new float[0];
        private int _shotIndex;
        private const float Period = 1f;
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        private float _next;

        private int _samples;
        private double _alive, _stacked, _onEnemy, _beyond;
        private int _supportCol, _supportStair, _supportWide, _overhangBack, _overhangFront;
        private readonly List<float> _frontRecess = new List<float>();
        private readonly List<float> _rearRecess = new List<float>();
        private double _gapSum, _midMoveSum, _maxLayerSum, _heightSum, _widthSum, _hwSum;
        private int _shapeN;
        // 땅 대기 줄 — 전선 12칸 안에서: 전체 · 땅을 딛은 유닛 · 그중 머리에 아군이 없는 유닛 · 그중 산(2층 이상) 뒤끝보다 뒤에 선 유닛. 원딜: 전체 · 머리에 아군이 올라탄 수
        private double _zone, _ground, _bare, _tail, _ranged, _rangedBuried;
        // 자리 기준 뒤로 밀림 · 등반 아닌 위로 튐 · 땅 아래 유닛 · 서로 겹친 쌍
        private const float PopUp = 3.5f;
        private readonly Dictionary<Unit, float> _peak = new Dictionary<Unit, float>();
        private readonly Dictionary<Unit, float> _lastFx = new Dictionary<Unit, float>();
        private readonly HashSet<Unit> _wasStag = new HashSet<Unit>();
        private readonly HashSet<Unit> _inPop = new HashSet<Unit>();
        private int _netRetreats, _pops;
        private double _backDist, _under, _overlapPairs;
        private readonly List<Bounds> _bounds = new List<Bounds>();
        private int _gapN, _midMoveN;
        private float _lastMid = float.NaN;
        private readonly int[] _layerCount = new int[16];

        private struct Info { public Unit U; public float X; public int Layer; public bool OnGround, OnEnemy, OnAlly, HeadAlly; }
        private readonly List<Info> _units = new List<Info>();

        // 뒤로 움직인 유닛-초 — 넉백(경직) 아닌데 가로 속도가 진행 반대로 BackSpeed 넘은 시간. 물리 스텝마다 센다(유닛 목록은 1초마다 새로)
        // 실행 순서 -1000(클래스 특성) — 유닛이 속도를 덮어쓰기 전, 지난 물리 스텝이 남긴 속도(충돌 밀어내기 포함)를 읽는다.
        // "뒤로 점프" = 위로 JumpUp 넘으면서 뒤로 BackSpeed 넘는 넉백 아닌 순간 — 유닛마다 들어선 횟수를 센다
        private const float BackSpeed = 0.3f;
        private const float JumpUp = 0.5f;
        private Unit[] _all = new Unit[0];
        private double _backSeconds, _unitSeconds, _probeSeconds;
        private readonly HashSet<Unit> _inBackJump = new HashSet<Unit>();
        private int _backJumps;
        // 원인별 뒤로 움직인 유닛-초 · 뒤로 점프 횟수
        private readonly Dictionary<string, double> _backByCause = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _jumpByCause = new Dictionary<string, int>();
        private const float VisibleBack = 0.15f;
        private readonly Dictionary<Unit, float> _episodeStart = new Dictionary<Unit, float>();
        private readonly HashSet<Unit> _wasUp = new HashSet<Unit>();
        private readonly Dictionary<string, int> _visibleByCause = new Dictionary<string, int>();
        public bool FreezeOnBackJump;   // 켜면 첫 뒤로 점프에서 시간을 거의 멈춘다(캡처용)
        public string LastBackJump = "";

        private string Cause(Unit u, Rigidbody2D rb, out Vector2 at)
        {
            at = rb.position;
            if (u.IsClimbing) return "climbing";
            if (u.IsSlamJumping) return "slam";
            int n = rb.GetContacts(_contacts);
            bool below = false, front = false, above = false;
            string support = null;
            for (int i = 0; i < n; i++)
            {
                Vector2 nrm = _contacts[i].normal;
                _contacts[i].collider.TryGetComponent(out Unit o);
                if (nrm.y > 0.5f)
                {
                    below = true;
                    if (o != null && o.GetComponent<Rigidbody2D>().linearVelocity.x * u.Forward < -BackSpeed)
                        support = o.Team == u.Team ? "riding ally" : "riding enemy";
                    else if (o == null && support == null)
                        support = _contacts[i].collider.TryGetComponent(out Ground _) ? null : "on " + _contacts[i].collider.name;
                }
                else if (nrm.y < -0.5f)
                    above = true;
                else if (nrm.x * u.Forward < -0.5f)
                    front = true;   // 앞에서 밀림
            }
            if (support != null) return support;
            if (!below && n == 0) return "free air";
            if (front && above) return "squeezed";
            if (front) return below ? "pushed from front" : "pushed in air";
            return below ? "other grounded" : "other contact";
        }

        private void FixedUpdate()
        {
            if (!BattleManager.Fighting || BattleManager.Elapsed < From || BattleManager.Elapsed > Until)
                return;
            float dt = Time.fixedDeltaTime;
            _probeSeconds += dt;
            foreach (Unit u in _all)
            {
                if (u == null || !u.isActiveAndEnabled || !u.IsAlive || u.Definition.AttackType == AttackType.Build)
                {
                    if (u != null) { _peak.Remove(u); _lastFx.Remove(u); }
                    continue;   // 건축가는 성으로 돌아가는 게 정상 동작
                }
                _unitSeconds += dt;
                var rb = u.GetComponent<Rigidbody2D>();
                Vector2 v = rb.linearVelocity;
                // 자리로 재는 뒤로 밀림(속도와 무관) — 넉백이 아닌데 가장 앞섰던 자리에서 VisibleBack칸 넘게 물러난 횟수 · 스텝마다 뒤로 간 거리 합
                float fx = rb.position.x * u.Forward;
                bool stag = u.IsStaggered;
                bool wasStag = _wasStag.Contains(u);
                if (stag) _wasStag.Add(u); else _wasStag.Remove(u);
                if (stag || wasStag || !_peak.TryGetValue(u, out float peak) || fx > peak)
                    _peak[u] = fx;
                else if (fx < peak - VisibleBack)
                {
                    _netRetreats++;
                    _peak[u] = fx;
                }
                if (!stag && !wasStag && _lastFx.TryGetValue(u, out float lastFx) && fx < lastFx - 0.001f && lastFx - fx < 3f)
                    _backDist += lastFx - fx;
                _lastFx[u] = fx;
                bool pop = !stag && !u.IsClimbing && !u.IsSlamJumping && v.y > PopUp;
                if (!pop) _inPop.Remove(u);
                else if (_inPop.Add(u)) _pops++;
                bool back = !u.IsStaggered && v.x * u.Forward < -BackSpeed;
                bool backJump = back && v.y > JumpUp;
                if (!backJump)
                    _inBackJump.Remove(u);
                // 눈에 보이는 뒤로 움직임 — 넉백 아닌 뒤로 움직임이 이어지는 동안 VisibleBack칸 넘게 물러나면 한 번(위로 뜨고 있었으면 "점프")
                if (!back)
                {
                    _episodeStart.Remove(u);
                    continue;
                }
                if (!_episodeStart.TryGetValue(u, out float startX))
                    _episodeStart[u] = startX = rb.position.x;
                else if (startX != float.PositiveInfinity && (rb.position.x - startX) * u.Forward < -VisibleBack)
                {
                    _episodeStart[u] = float.PositiveInfinity;   // 이 사건은 센 것
                    string vc = Cause(u, rb, out _);
                    string key = (v.y > JumpUp || _wasUp.Contains(u) ? "jump " : "slide ") + vc;
                    _visibleByCause.TryGetValue(key, out int vcN);
                    _visibleByCause[key] = vcN + 1;
                }
                if (v.y > JumpUp) _wasUp.Add(u); else _wasUp.Remove(u);
                _backSeconds += dt;
                string cause = Cause(u, rb, out Vector2 at);
                _backByCause.TryGetValue(cause, out double s);
                _backByCause[cause] = s + dt;
                if (backJump && _inBackJump.Add(u))
                {
                    _backJumps++;
                    _jumpByCause.TryGetValue(cause, out int c);
                    _jumpByCause[cause] = c + 1;
                    LastBackJump = $"{BattleManager.Elapsed:F1}s {u.Definition.name} {cause} at ({at.x:F1},{at.y:F1}) v({v.x:F1},{v.y:F1})";
                    if (FreezeOnBackJump)
                    {
                        FreezeOnBackJump = false;
                        Time.timeScale = 0.0001f;
                    }
                }
            }
        }

        private string Causes()
        {
            var sb = new System.Text.StringBuilder(" · back by cause:");
            foreach (var kv in _backByCause)
                sb.Append($" {kv.Key} {100 * kv.Value / System.Math.Max(1e-6, _backSeconds):F0}%");
            sb.Append($" · backJumps {_backJumps} ({_backJumps / System.Math.Max(1e-6, _probeSeconds / 60):F1}/min):");
            foreach (var kv in _jumpByCause)
                sb.Append($" {kv.Key} {kv.Value}");
            int vis = 0, visJump = 0;
            foreach (var kv in _visibleByCause) { vis += kv.Value; if (kv.Key.StartsWith("jump")) visJump += kv.Value; }
            double min = System.Math.Max(1e-6, _probeSeconds / 60);
            sb.Append($" · VISIBLE back(>{VisibleBack}칸) {vis / min:F1}/min, of which jump {visJump / min:F1}/min:");
            foreach (var kv in _visibleByCause)
                sb.Append($" {kv.Key} {kv.Value}");
            return sb.ToString();
        }

        private void Update()
        {
            if (BattleManager.Fighting && !string.IsNullOrEmpty(Shot) && _shotIndex < ShotAt.Length && BattleManager.Elapsed >= ShotAt[_shotIndex])
            {
                // 카메라를 전선(양 진영 가장 앞 유닛의 가운데)에 맞추고 찍는다
                float ally = float.NegativeInfinity, enemy = float.PositiveInfinity;
                foreach (Unit u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                    if (u.IsAlive)
                    {
                        if (u.Team == Team.Ally) ally = Mathf.Max(ally, u.transform.position.x);
                        else enemy = Mathf.Min(enemy, u.transform.position.x);
                    }
                if (!float.IsInfinity(ally) && !float.IsInfinity(enemy) && Camera.main != null)
                {
                    Vector3 p = Camera.main.transform.position;
                    Camera.main.transform.position = new Vector3((ally + enemy) * 0.5f, p.y, p.z);
                }
                ScreenCapture.CaptureScreenshot($"Captures/Pile/{Shot}_t{ShotAt[_shotIndex++]:F0}.png");
            }
            if (!BattleManager.Fighting || BattleManager.Elapsed < From || BattleManager.Elapsed > Until || Ground.Instance == null)
                return;
            if (Time.time < _next)
                return;
            _next = Time.time + Period;
            _all = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            Sample();
        }

        private void Sample()
        {
            _units.Clear();
            float[] line = { float.NegativeInfinity, float.NegativeInfinity };   // 진영별 땅 선두 x(아군 = 최대, 적 = 최소를 전방 좌표로)
            foreach (Unit u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                if (!u.IsAlive)
                    continue;
                var rb = u.GetComponent<Rigidbody2D>();
                var info = new Info { U = u, X = rb.position.x };
                info.Layer = u.StackLayer;   // 받침 그래프 층(땅 0) — 지형 기울기와 무관
                float nearestDx = float.MaxValue;
                float minSupport = float.PositiveInfinity, maxSupport = float.NegativeInfinity;   // 받침 아군들의 전방 좌표
                int n = rb.GetContacts(_contacts);
                for (int i = 0; i < n; i++)
                {
                    if (_contacts[i].normal.y < -0.5f && _contacts[i].collider.TryGetComponent(out Unit over) && over.IsAlive && over.Team == u.Team)
                        info.HeadAlly = true;
                    if (_contacts[i].normal.y <= 0.5f)
                        continue;
                    if (_contacts[i].collider.TryGetComponent(out Unit below))
                    {
                        if (!below.IsAlive)
                            continue;
                        if (below.Team == u.Team)
                        {
                            info.OnAlly = true;
                            nearestDx = Mathf.Min(nearestDx, Mathf.Abs(below.transform.position.x - info.X));
                            float sa = below.transform.position.x * u.Forward;
                            minSupport = Mathf.Min(minSupport, sa);
                            maxSupport = Mathf.Max(maxSupport, sa);
                        }
                        else
                            info.OnEnemy = true;
                    }
                    else if (_contacts[i].collider.TryGetComponent(out Ground _))
                        info.OnGround = true;
                }
                if (info.OnAlly && !info.OnEnemy)
                {
                    if (nearestDx < 0.25f) _supportCol++;
                    else if (nearestDx <= 0.75f) _supportStair++;
                    else _supportWide++;
                    // 받침 밖으로 반 칸 넘게 걸친 유닛 — 뒤로 기운 사슬 · 앞으로 내민 처마
                    float a = info.X * u.Forward;
                    if (a < minSupport - 0.25f) _overhangBack++;
                    if (a > maxSupport + 0.25f) _overhangFront++;
                }
                if (info.OnGround)
                {
                    int t = (int)u.Team;
                    line[t] = Mathf.Max(line[t], info.X * u.Forward);
                }
                _units.Add(info);
            }
            if (float.IsNegativeInfinity(line[0]) || float.IsNegativeInfinity(line[1]))
                return;
            _samples++;
            // 근접이 무엇을 하고 있나(Unit.DevIdle) — 내 땅 선두에서 15칸 안의 근접만, 구간 0 = 90초 전 · 1 = 뒤. 근접 선두 사이 거리
            float[] meleeLead = { float.NegativeInfinity, float.NegativeInfinity };
            // 층마다 가장 앞에 선 유닛(병종 무관) — 그 유닛이 근접이면 "층 선두 근접"
            var layerLead = new float[2, 16];
            for (int t = 0; t < 2; t++) for (int k = 0; k < 16; k++) layerLead[t, k] = float.NegativeInfinity;
            foreach (Info i in _units)
                if ((i.OnGround || i.OnAlly || i.OnEnemy) && i.Layer < 16)
                    layerLead[(int)i.U.Team, i.Layer] = Mathf.Max(layerLead[(int)i.U.Team, i.Layer], i.X * i.U.Forward);
            int bucket = BattleManager.Elapsed < 90f ? 0 : 1;
            foreach (Info i in _units)
            {
                if (i.U.Definition.AttackType != AttackType.Melee)
                    continue;
                int t = (int)i.U.Team;
                float ahead = i.X * i.U.Forward;
                meleeLead[t] = Mathf.Max(meleeLead[t], ahead);
                if (ahead < line[t] - 15f)
                    continue;
                int code = i.U.DevIdle;
                if (code == 1 && Mathf.Abs(i.U.GetComponent<Rigidbody2D>().linearVelocity.x) < 0.3f)
                    code = 8;   // 걸으라는데 안 움직인다(막힘)
                _melee[bucket, code]++;
                bool standing = i.OnGround || i.OnAlly || i.OnEnemy;
                if (standing && i.Layer < 16 && ahead >= layerLead[t, i.Layer] - 0.01f) { _front[0, code]++; if (i.Layer == 0) _front[2, code]++; }
                if (standing && i.U.DevFront) _front[1, code]++;
                // 적 원거리에게 맞는데 아무것도 못 하는 근접 — 최근 HelplessWindow초 안에 맞았고 그동안 한 번도 못 쳤다
                _zoneMelee++;
                if (Time.time - i.U.DevHitAt < HelplessWindow) { _shotAt++; if (Time.time - i.U.DevAttackAt > HelplessWindow) { _helpless++; if (code != 1) _helplessStill++; } }
            }
            if (!float.IsNegativeInfinity(meleeLead[0]) && !float.IsNegativeInfinity(meleeLead[1]))
            {
                _meleeGap += -meleeLead[(int)Team.Enemy] - meleeLead[(int)Team.Ally];
                _meleeGapN++;
            }
            _bounds.Clear();
            foreach (Info i in _units)
            {
                Bounds b = i.U.GetComponent<Collider2D>().bounds;
                _bounds.Add(b);
                if (b.min.y < Ground.Instance.HeightAt(b.center.x) - 0.5f)
                    _under++;
            }
            for (int a = 0; a < _bounds.Count; a++)
                for (int c = a + 1; c < _bounds.Count; c++)
                {
                    float ox = Mathf.Min(_bounds[a].max.x, _bounds[c].max.x) - Mathf.Max(_bounds[a].min.x, _bounds[c].min.x);
                    if (ox <= 0.3f) continue;
                    float oy = Mathf.Min(_bounds[a].max.y, _bounds[c].max.y) - Mathf.Max(_bounds[a].min.y, _bounds[c].min.y);
                    if (oy > 0.3f) _overlapPairs++;
                }
            float allyLine = line[(int)Team.Ally], enemyLine = -line[(int)Team.Enemy];
            _gapSum += enemyLine - allyLine;
            _gapN++;
            float mid = (allyLine + enemyLine) * 0.5f;
            if (!float.IsNaN(_lastMid))
            {
                _midMoveSum += Mathf.Abs(mid - _lastMid);
                _midMoveN++;
            }
            _lastMid = mid;

            int maxLayerBoth = 0;
            for (int team = 0; team < 2; team++)
            {
                float forward = team == (int)Team.Ally ? 1f : -1f;
                float myLine = line[team];
                float otherLine = line[1 - team];   // 상대 전방 좌표 → 내 좌표로는 -otherLine
                var front = new float[16];
                var rear = new float[16];
                for (int k = 0; k < 16; k++) { front[k] = float.NegativeInfinity; rear[k] = float.PositiveInfinity; }
                foreach (Info i in _units)
                {
                    if ((int)i.U.Team != team)
                        continue;
                    _alive++;
                    float ahead = i.X * forward;
                    if (ahead > -otherLine + 0.01f)
                        _beyond++;
                    if (i.OnEnemy)
                        _onEnemy++;
                    if (i.OnAlly || i.OnEnemy)
                        _stacked++;
                    // 산 모양 — 전선에서 12칸 안, 땅이나 아군을 딛고 선 유닛만
                    if (i.OnEnemy || !(i.OnGround || i.OnAlly) || ahead < myLine - 12f || i.Layer >= 16)
                        continue;
                    _layerCount[i.Layer]++;
                    _zone++;
                    if (i.Layer == 0) { _ground++; if (!i.HeadAlly) _bare++; }
                    if (i.U.Definition.AttackType == AttackType.Ranged) { _ranged++; if (i.HeadAlly) _rangedBuried++; }
                    front[i.Layer] = Mathf.Max(front[i.Layer], ahead);
                    if (i.Layer > 0)
                        rear[i.Layer] = Mathf.Min(rear[i.Layer], ahead);
                }
                int maxLayer = 0;
                for (int k = 1; k < 16; k++)
                {
                    if (float.IsNegativeInfinity(front[k]) || float.IsNegativeInfinity(front[k - 1]))
                        break;
                    maxLayer = k;
                    _frontRecess.Add(front[k - 1] - front[k]);
                    if (k >= 2)
                        _rearRecess.Add(rear[k] - rear[k - 1]);
                }
                maxLayerBoth = Mathf.Max(maxLayerBoth, maxLayer);
                _maxLayerSum += maxLayer;
                // 산 높이(층 수) · 폭(2층 이상이 덮은 가로 길이) — "아래로 퍼졌나"는 높이 ÷ 폭
                if (maxLayer >= 1)
                {
                    float f = float.NegativeInfinity, r = float.PositiveInfinity;
                    for (int k = 1; k <= maxLayer; k++) { f = Mathf.Max(f, front[k]); r = Mathf.Min(r, rear[k]); }
                    float width = f - r + 1f;
                    _heightSum += maxLayer + 1;
                    _widthSum += width;
                    _hwSum += (maxLayer + 1) / width;
                    _shapeN++;
                    foreach (Info i in _units)
                        if ((int)i.U.Team == team && !i.OnEnemy && i.OnGround && i.Layer == 0 && !i.HeadAlly && i.X * forward < r - 0.5f && i.X * forward >= myLine - 12f)
                            _tail++;
                }
            }
        }

        private readonly double[,] _melee = new double[2, 11];
        private const float HelplessWindow = 3f;
        private readonly double[,] _front = new double[3, 11];   // 0 층 선두 근접 · 1 앞에 맞닿은 아군이 없는 근접 · 2 땅 선두 근접
        private double _zoneMelee, _shotAt, _helpless, _helplessStill;
        private static readonly string[] IdleNames = { "fight", "walk", "waitAbove", "onBase", "stairLine", "behindRanged", "behindAllySupport", "behindAlly", "blocked", "other", "stairSupport" };

        // 맨 앞 근접이 무엇을 하고 있나 — 층 선두 · 앞이 빈 유닛 · 땅 선두. 그리고 원거리에 맞기만 하는 근접(표본당 기 수)
        public string Front()
        {
            string[] groups = { "layerLead", "noAllyAhead", "groundLead" };
            var sb = new System.Text.StringBuilder();
            for (int g = 0; g < 3; g++)
            {
                double total = 0;
                for (int c = 0; c < 11; c++) total += _front[g, c];
                sb.Append($"[{groups[g]}] n/sample {total / System.Math.Max(1, _samples):F1} idle {100 * (total - _front[g, 0] - _front[g, 1]) / System.Math.Max(1, total):F0}%:");
                for (int c = 0; c < 11; c++)
                    if (_front[g, c] > 0) sb.Append($" {IdleNames[c]} {100 * _front[g, c] / total:F0}%");
                sb.Append(" · ");
            }
            int s = System.Math.Max(1, _samples);
            sb.Append($"zoneMelee {_zoneMelee / s:F1} shotAt {_shotAt / s:F1} helpless {_helpless / s:F2} ({100 * _helpless / System.Math.Max(1, _zoneMelee):F1}%) helplessStanding {_helplessStill / s:F2}");
            return sb.ToString();
        }
        private double _meleeGap;
        private int _meleeGapN;

        // 근접 상태 분포(%) — 구간별. fight 싸움 · walk 걷는 중 · 그 밖은 사거리에 적이 없는데 선 이유
        public string Melee()
        {
            string[] names = IdleNames;
            var sb = new System.Text.StringBuilder($"meleeGap {_meleeGap / Mathf.Max(1, _meleeGapN):F2}");
            for (int b = 0; b < 2; b++)
            {
                double total = 0;
                for (int c = 0; c < 11; c++) total += _melee[b, c];
                double idle = total - _melee[b, 0] - _melee[b, 1];
                sb.Append($" · [{(b == 0 ? "30-90" : "90-150")}] n {total / System.Math.Max(1, _samples) * 2:F0} idle {100 * idle / System.Math.Max(1, total):F0}%:");
                for (int c = 0; c < 11; c++)
                    if (_melee[b, c] > 0) sb.Append($" {names[c]} {100 * _melee[b, c] / total:F0}%");
            }
            return sb.ToString();
        }

        // 한 줄 요약 — 변형 비교용
        public string Short()
        {
            if (_samples == 0)
                return "no samples";
            int supports = Mathf.Max(1, _supportCol + _supportStair + _supportWide);
            int vis = 0, visJump = 0;
            foreach (var kv in _visibleByCause) { vis += kv.Value; if (kv.Key.StartsWith("jump")) visJump += kv.Value; }
            double min = System.Math.Max(1e-6, _probeSeconds / 60);
            int n = Mathf.Max(1, _shapeN);
            return $"n{_samples} H {_heightSum / n:F1} W {_widthSum / n:F1} H/W {_hwSum / n:F2} stair {100f * _supportStair / supports:F0}% col {100f * _supportCol / supports:F0}% beyond {100 * _beyond / _alive:F1}% onEnemy {100 * _onEnemy / System.Math.Max(1, _stacked):F1}% gap {_gapSum / Mathf.Max(1, _gapN):F2} alive {_alive / (2 * _samples):F0} back {100 * _backSeconds / System.Math.Max(1e-6, _unitSeconds):F2}% vis {vis / min:F0}/m jump {visJump / min:F1}/m bjump {_backJumps / min:F1}/m · zone {_zone / (2 * _samples):F1} ground {_ground / (2 * _samples):F1} ({100 * _ground / System.Math.Max(1, _zone):F0}%) bare {_bare / (2 * _samples):F1} tail {_tail / (2 * _samples):F1} · rangedBuried {100 * _rangedBuried / System.Math.Max(1, _ranged):F0}% maxL {_maxLayerSum / (2 * _samples):F1} · netBack {_netRetreats / min:F1}/m backDist {_backDist / System.Math.Max(1e-6, _unitSeconds / 60):F3}/unit-min pop {_pops / min:F1}/m under {_under / _samples:F2} overlap {_overlapPairs / _samples:F1}";
        }

        public string Summary()
        {
            if (_samples == 0)
                return "no samples";
            string Rec(List<float> list)
            {
                if (list.Count == 0) return "-";
                int stair = 0, over = 0, sag = 0; float sum = 0f;
                foreach (float r in list)
                {
                    sum += r;
                    if (r >= 0.25f && r <= 0.75f) stair++;
                    else if (r < 0.25f) over++;
                    else sag++;
                }
                return $"mean {sum / list.Count:F2} · stair {100f * stair / list.Count:F0}% · <0.25 {100f * over / list.Count:F0}% · >0.75 {100f * sag / list.Count:F0}% (n {list.Count})";
            }
            int supports = _supportCol + _supportStair + _supportWide;
            var layers = new System.Text.StringBuilder();
            for (int k = 0; k < 10; k++)
                layers.Append($"{_layerCount[k] / (2f * _samples):F1} ");
            return $"H {_heightSum / System.Math.Max(1, _shapeN):F1} W {_widthSum / System.Math.Max(1, _shapeN):F1} H/W {_hwSum / System.Math.Max(1, _shapeN):F2} · back {100 * _backSeconds / System.Math.Max(1e-6, _unitSeconds):F2}% ({_backSeconds:F0} unit-s) · samples {_samples} · alive/team {_alive / (2 * _samples):F0} · stacked {100 * _stacked / _alive:F0}% · onEnemy/stacked {100 * _onEnemy / Mathf.Max(1, (float)_stacked):F1}% · beyond {100 * _beyond / _alive:F1}%"
                 + $" · support col {100f * _supportCol / Mathf.Max(1, supports):F0}% stair {100f * _supportStair / Mathf.Max(1, supports):F0}% wide {100f * _supportWide / Mathf.Max(1, supports):F0}% · overhang back {100f * _overhangBack / Mathf.Max(1, supports):F0}% front {100f * _overhangFront / Mathf.Max(1, supports):F0}%"
                 + $" · frontRecess {Rec(_frontRecess)} · rearRecess {Rec(_rearRecess)}"
                 + $" · maxLayer {_maxLayerSum / (2 * _samples):F1} · gap {_gapSum / Mathf.Max(1, _gapN):F2} · midMove/s {_midMoveSum / Mathf.Max(1, _midMoveN):F2} · layers {layers}" + Causes();
        }
    }
}

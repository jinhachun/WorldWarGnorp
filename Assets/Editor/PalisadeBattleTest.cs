using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GnorpWar.Editor
{
    // 목책 실측(10/29) — 같은 탑 거울전에서 내 탑 한 칸에 목책을 넣은 판과 비운 판을 번갈아 실제 전투로 돌리고,
    // 목책이 막아 낸 피해 · 서 있던 시간 · 동시에 선 수 · 세울 때 적과 겹친 수 · 승패를 잰다. 실행 방식은 RunawayBattleTest와 같다(플레이 중 메뉴).
    // 런 기록은 Ghosts/_목책검증에. 결과: 콘솔 [목책 실측] + Temp/Palisade/<시각>.txt. 종료 조건: 판마다 실제 WallLimit초 · 판 수 Cases × Rounds
    public static class PalisadeBattleTest
    {
        private const float Speed = 6f;
        private const double WallLimit = 360.0;
        private const int Rounds = 4;
        private const int Floors = 5;   // 목책 말고 깐 기물 수 — 양쪽 같은 탑

        private static readonly BuildingNation[] Nations = { BuildingNation.Kingdom, BuildingNation.Tribe };
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

        public static string Status { get; private set; } = "안 돌림";

        [MenuItem("GnorpWar/목책 실측 전투 (플레이 중)")]
        private static void Menu() => Run();

        private class Tracked
        {
            public float LastHp, Born;
        }

        private class Tally
        {
            public int Battles, Wins, Built, Destroyed, Removed, Overlaps, PeakAlive;
            public float Absorbed, Life, Seconds, AllyBaseHp, EnemyBaseHp;
        }

        // withEmpty = 빈칸 판도 돌린다(끄면 목책 판만 — 목책끼리 전/후 비교용)
        public static string Run(bool withEmpty = true)
        {
            if (!EditorApplication.isPlaying)
                return Status = "플레이 중이 아님";
            BattleConfig config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            BuildingDefinition palisadeDef = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Data/Buildings/Building_Palisade.asset");
            var cases = new List<(BuildingNation nation, bool palisade)>();
            for (int r = 0; r < Rounds; r++)
                foreach (BuildingNation n in Nations)
                {
                    cases.Add((n, true));
                    if (withEmpty)
                        cases.Add((n, false));
                }
            var tallies = new Dictionary<string, Tally>();
            Directory.CreateDirectory("Temp/Palisade");
            string path = $"Temp/Palisade/{System.DateTime.Now:HHmmss}.txt";
            var log = new StringBuilder();
            int index = -1;
            bool waitingStart = false;
            double wallStart = 0;
            float lastElapsed = 0f, allyHp = 1f, enemyHp = 1f;
            var tracked = new Dictionary<Palisade, Tracked>();
            Tally current = null;
            int enemyUnitMask = 1 << LayerMask.NameToLayer("EnemyUnit");

            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (!EditorApplication.isPlaying)
                {
                    log.AppendLine("플레이가 꺼져 멈춤");
                    Finish();
                    return;
                }
                bool fighting = BattleManager.Fighting;
                if (index >= 0 && waitingStart)
                {
                    if (fighting)
                    {
                        waitingStart = false;
                        Time.timeScale = Speed;
                    }
                    else if (EditorApplication.timeSinceStartup - wallStart > 30)
                        Next();
                    return;
                }
                if (index < 0)
                {
                    Next();
                    return;
                }
                if (fighting)
                {
                    Time.timeScale = Speed;
                    lastElapsed = BattleManager.Elapsed;
                    Base ally = Base.For(Team.Ally), enemy = Base.For(Team.Enemy);
                    if (ally != null)
                        allyHp = ally.Hp / ally.MaxHp;
                    if (enemy != null)
                        enemyHp = enemy.Hp / enemy.MaxHp;
                    Track(false);
                }
                bool over = RunState.Phase != RunState.RunPhase.Battle;
                if (!over && EditorApplication.timeSinceStartup - wallStart <= WallLimit)
                    return;
                Track(true);
                current.Battles++;
                if (RunState.LastOutcome == RunState.Outcome.Win)
                    current.Wins++;
                current.Seconds += lastElapsed;
                current.AllyBaseHp += Mathf.Max(0f, allyHp);
                current.EnemyBaseHp += Mathf.Max(0f, enemyHp);
                var c = cases[index];
                log.AppendLine($"{c.nation} {(c.palisade ? "목책" : "빈칸")} — {RunState.LastOutcome} · {lastElapsed:0}초 · 내 성 {allyHp * 100f:0}% · 적 성 {enemyHp * 100f:0}%");
                Next();
            };

            // 내 목책만 센다. 사라진 목책은 남은 체력까지 막아 낸 것으로(넘친 피해는 빼고)
            void Track(bool end)
            {
                var alive = new HashSet<Palisade>();
                int count = 0;
                foreach (Palisade p in Object.FindObjectsByType<Palisade>(FindObjectsSortMode.None))
                {
                    if (p.Team != Team.Ally || !p.IsAlive)
                        continue;
                    count++;
                    alive.Add(p);
                    if (!tracked.TryGetValue(p, out Tracked t))
                    {
                        tracked[p] = t = new Tracked { LastHp = p.Hp, Born = lastElapsed };
                        current.Built++;
                        var box = p.GetComponent<BoxCollider2D>();
                        if (Physics2D.OverlapBox(box.bounds.center, box.bounds.size * 0.95f, 0f, enemyUnitMask) != null)
                            current.Overlaps++;
                    }
                    if (p.Hp < t.LastHp)
                        current.Absorbed += t.LastHp - p.Hp;
                    t.LastHp = p.Hp;
                }
                current.PeakAlive = Mathf.Max(current.PeakAlive, count);
                var gone = new List<Palisade>();
                foreach (var pair in tracked)
                    if (!alive.Contains(pair.Key) || end)
                        gone.Add(pair.Key);
                foreach (Palisade p in gone)
                {
                    Tracked t = tracked[p];
                    // 사라진 것 — 상한으로 걷힌 것(Palisade.Remove)은 막은 피해에 더하지 않고, 부서진 것은 남은 체력까지 막았다
                    if (!alive.Contains(p))
                    {
                        if (p.Removed)
                            current.Removed++;
                        else
                        {
                            current.Absorbed += t.LastHp;
                            current.Destroyed++;
                        }
                    }
                    current.Life += lastElapsed - t.Born;
                    tracked.Remove(p);
                }
            }

            void Next()
            {
                index++;
                if (index >= cases.Count)
                {
                    Summarize();
                    Finish();
                    return;
                }
                var c = cases[index];
                string key = $"{c.nation} {(c.palisade ? "목책" : "빈칸")}";
                if (!tallies.TryGetValue(key, out current))
                    tallies[key] = current = new Tally();
                tracked.Clear();
                lastElapsed = 0f;
                allyHp = enemyHp = 1f;
                Setup(config, c.nation, c.palisade ? palisadeDef : null);
                waitingStart = true;
                wallStart = EditorApplication.timeSinceStartup;
                SceneManager.LoadScene("Battle");
            }

            void Summarize()
            {
                log.AppendLine("— 합계(판 평균) —");
                foreach (var pair in tallies)
                {
                    Tally t = pair.Value;
                    float n = Mathf.Max(1, t.Battles);
                    log.AppendLine($"[{pair.Key}] {t.Battles}판 승 {t.Wins} · 전투 {t.Seconds / n:0}초 · 내 성 {t.AllyBaseHp / n * 100f:0}% · 적 성 {t.EnemyBaseHp / n * 100f:0}%"
                                   + (t.Built > 0 ? $" · 목책 세움 {t.Built / n:0.#} · 부서짐 {t.Destroyed / n:0.#} · 걷힘 {t.Removed / n:0.#} · 막은 피해 {t.Absorbed / n:0} · 한 개 평균 서 있던 시간 {t.Life / Mathf.Max(1, t.Built):0}초 · 동시 최대 {t.PeakAlive} · 세울 때 적과 겹침 {t.Overlaps}" : ""));
                }
            }

            void Finish()
            {
                EditorApplication.update -= tick;
                Time.timeScale = 1f;
                File.WriteAllText(path, log.ToString());
                Status = log.ToString();
                Debug.Log("[목책 실측] 끝 — " + path + "\n" + log);
            }

            EditorApplication.update += tick;
            return Status = "도는 중 — 결과는 " + path;
        }

        // 거울전 — 양쪽 같은 기물(그 국가 + 공용 상점 기물, 소환 · 발동 계열 위주 Floors개, 시작 등급) · 업그레이드 없음. 내 탑 마지막 칸에 목책(또는 빈칸)
        private static void Setup(BattleConfig config, BuildingNation nation, BuildingDefinition palisade)
        {
            RunState.CheatNewRun();
            RunState.ChooseNation(nation);
            typeof(RunState).GetField("_record", Static).SetValue(null, new RunRecord(Path.Combine(RunRecord.Folder, "_목책검증"), config.AssetIds));
            RunState.CheatSetFloors(RunState.MaxFloors);
            OwnedBuilding[] picked = Picked(config, nation);
            for (int i = 0; i < RunState.Field.Length; i++)
                RunState.Field[i] = i < picked.Length ? picked[i] : null;
            if (palisade != null)
                RunState.Field[picked.Length] = new OwnedBuilding(palisade);
            var player = (TowerState)typeof(RunState).GetField("Player", Static).GetValue(null);
            player.Upgrades.Clear();
            RunState.CheatEnemyField = Picked(config, nation);
            int region = -1;
            foreach (int r in RunState.InvadableRegions())
                if (RunState.World.TowerOf(RunState.World.State.OwnerOf(r)) != null)
                {
                    region = r;
                    break;
                }
            RunState.Invade(region);
            var enemy = (TowerState)typeof(RunState).GetField("_opponentTower", Static).GetValue(null);
            enemy.Upgrades.Clear();
        }

        private static OwnedBuilding[] Picked(BattleConfig config, BuildingNation nation)
        {
            var pool = new List<BuildingDefinition>();
            foreach (BuildingDefinition b in config.ShopPool)
                if (b != null && (b.Nation == nation || b.Nation == BuildingNation.Common) && b.Unit != null && !pool.Contains(b))
                    pool.Add(b);
            pool.Sort((a, b) =>
            {
                int n = (b.Nation == nation).CompareTo(a.Nation == nation);
                return n != 0 ? n : a.Rarity.CompareTo(b.Rarity);
            });
            var field = new OwnedBuilding[Mathf.Min(Floors, pool.Count)];
            for (int i = 0; i < field.Length; i++)
                field[i] = new OwnedBuilding(pool[i]);
            return field;
        }
    }
}

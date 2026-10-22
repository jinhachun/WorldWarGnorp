using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 월드맵 규칙을 실제 에셋 · 실제 상점 AI로 돌려 보는 도구(메뉴 GnorpWar/월드 시뮬레이션) — 결과는 콘솔에 표로 남는다.
    // 플레이어 자리는 고정 승률 주사위다(실제 전투 없음). 영주의 힘은 그 영주가 상점에서 키운 탑의 전투력(World/TowerPower).
    // 상한: 판 수 MaxRuns · 판당 턴 수 maxTurns(넘으면 「미종료」) — 한 판은 영주 수십 명의 상점 × 턴 수라 수십 ms다
    internal static class WorldSimMenu
    {
        private const string WorldConfigPath = "Assets/Data/World/WorldConfig.asset";
        private const string BattleConfigPath = "Assets/Data/BattleConfig.asset";
        private const int MaxRuns = 500;

        // 땅 수 묶음 — 「땅이 늘면 힘이 얼마나 느나」를 보는 칸
        private static readonly int[] LandBuckets = { 1, 2, 3, 4, 6, 10 };

        [MenuItem("GnorpWar/월드 시뮬레이션/20판 돌리기")]
        private static void RunDefault() => Debug.Log(Simulate(20, 60, 0.8, 1));

        [MenuItem("GnorpWar/월드 시뮬레이션/탑 전투력 표")]
        private static void LogPowerTable() => Debug.Log(PowerTable(20, 8, 1));

        // 빌드 방향마다 탑 하나를 혼자(땅 1개의 수입으로) turns턴 키웠을 때의 전투력 — samples번의 평균. 생산 항 / 쓴 골드 보정 / 합
        public static string PowerTable(int samples, int turns, int seed)
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldConfig>(WorldConfigPath);
            var battle = AssetDatabase.LoadAssetAtPath<BattleConfig>(BattleConfigPath);
            samples = Mathf.Clamp(samples, 1, MaxRuns);
            bool quiet = EnemyShopper.Quiet;
            EnemyShopper.Quiet = true;
            var text = new StringBuilder($"[World] 탑 전투력 표 — 땅 1개 수입 {battle.ShopGold + world.GoldPerLand}골드/턴 · {samples}번 평균 · 쓴 골드 1 = 전투력 {world.PowerPerGold:0.##}\n");
            text.Append("빌드 방향 | 턴: 생산 항 + 보정 = 합 (탑의 기물 수)\n");
            try
            {
                Random.InitState(seed);
                RunState.StartNew(battle);
                var builds = new List<EnemyBuildProfile>();
                for (int b = 0; b < world.Map.BuildCount; b++)
                    if (!builds.Contains(world.Map.BuildOf(b)))
                        builds.Add(world.Map.BuildOf(b));
                foreach (EnemyBuildProfile build in builds)
                {
                    var production = new float[turns];
                    var support = new float[turns];
                    var filled = new float[turns];
                    for (int s = 0; s < samples; s++)
                    {
                        var tower = new TowerState(Team.Enemy);
                        tower.Reset(battle);
                        for (int turn = 0; turn < turns; turn++)
                        {
                            RunState.ShopAs(tower, build, battle.ShopGold + world.GoldPerLand, world.ShopTier(turn));
                            production[turn] += TowerPower.Production(tower.Field, battle);
                            support[turn] += TowerPower.SupportGold(tower.Field, battle) * world.PowerPerGold;
                            foreach (OwnedBuilding owned in tower.Field)
                                if (!OwnedBuilding.IsEmpty(owned))
                                    filled[turn]++;
                        }
                    }
                    text.Append($"{build.DisplayName} |");
                    for (int turn = 0; turn < turns; turn++)
                        text.Append($" {turn + 1}: {production[turn] / samples:0.0}+{support[turn] / samples:0.0}={(production[turn] + support[turn]) / samples:0.0}({filled[turn] / samples:0.0})");
                    text.Append('\n');
                }
            }
            finally
            {
                EnemyShopper.Quiet = quiet;
            }
            return text.ToString();
        }

        // runs판 — 플레이어는 매 턴 이어진 영주 중 땅이 가장 적은(biggestFirst면 가장 많은) 영주를 치고 playerWin 확률로 이긴다
        public static string Simulate(int runs, int maxTurns, double playerWin, int seed, bool biggestFirst = false)
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldConfig>(WorldConfigPath);
            var battle = AssetDatabase.LoadAssetAtPath<BattleConfig>(BattleConfigPath);
            runs = Mathf.Clamp(runs, 1, MaxRuns);
            int regions = world.Map.RegionCount;
            int builds = world.Map.BuildCount;
            const int Tempers = 4, Shown = 10;

            var clearTurns = new List<int>();
            int over = 0, unfinished = 0, coalitions = 0, battles = 0, coalitionBattles = 0, coalitionOver = 0;
            float coalitionTurn = 0f, coalitionMembers = 0f, coalitionLands = 0f, coalitionFloors = 0f, coalitionRatio = 0f;
            var merged = new List<Lord>();
            var lordsAfter = new float[Shown];
            var lordsSamples = new int[Shown];
            var powerSum = new float[Shown];
            var powerMin = new float[Shown];
            var powerMax = new float[Shown];
            var powerCount = new int[Shown];
            var supportShare = new float[Shown];
            // [묶음] = 같은 턴의 땅 1개 영주 평균 전투력 대비 배율의 합 · 표본 수 · 땅 수 합
            var bucketRatio = new float[LandBuckets.Length];
            var bucketCount = new int[LandBuckets.Length];
            var bucketLands = new float[LandBuckets.Length];
            float perLandSum = 0f;
            int perLandCount = 0;
            int[] buildStart = new int[builds], buildAlive3 = new int[builds], buildAlive5 = new int[builds], buildAttacks = new int[builds], buildWins = new int[builds], buildDefends = new int[builds], buildHeld = new int[builds];
            int[] temperStart = new int[Tempers], temperAlive3 = new int[Tempers], temperAlive5 = new int[Tempers], temperAttacks = new int[Tempers], temperWins = new int[Tempers];
            for (int i = 0; i < Shown; i++)
                powerMin[i] = float.MaxValue;

            bool quiet = EnemyShopper.Quiet;
            EnemyShopper.Quiet = true;
            RunState.StartNew(battle);   // 영주의 상점(RunState.ShopAs)이 런 설정을 읽는다
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                for (int r = 0; r < runs; r++)
                {
                    Random.InitState(seed + r);
                    var random = new System.Random(seed + r);
                    var run = new WorldRun(world, battle, random.Next(regions), random);
                    WorldState state = run.State;
                    foreach (Lord lord in state.Lords)
                        if (!lord.IsPlayer)
                        {
                            buildStart[lord.Build]++;
                            temperStart[(int)lord.Temper]++;
                        }
                    bool finished = false;
                    for (; state.Turn <= maxTurns && !finished; state.Turn++)
                    {
                        int t = state.Turn - 1;
                        run.ShopPhase();

                        // 이번 턴 상점 뒤의 탑 전투력 — 분포와 「땅 수 → 힘」
                        if (t < Shown)
                        {
                            float single = 0f;
                            int singles = 0;
                            foreach (Lord lord in state.Lords)
                                if (lord.Alive && !lord.IsPlayer && state.LandsOf(lord) == 1)
                                {
                                    single += run.PowerOf(lord);
                                    singles++;
                                }
                            foreach (Lord lord in state.Lords)
                            {
                                if (!lord.Alive || lord.IsPlayer)
                                    continue;
                                float power = run.PowerOf(lord);
                                TowerState tower = run.TowerOf(lord);
                                powerSum[t] += power;
                                powerMin[t] = Mathf.Min(powerMin[t], power);
                                powerMax[t] = Mathf.Max(powerMax[t], power);
                                powerCount[t]++;
                                if (power > 0f)
                                    supportShare[t] += TowerPower.SupportGold(tower.Field, battle) * world.PowerPerGold / power;
                                int lands = state.LandsOf(lord);
                                if (singles == 0 || single <= 0f)
                                    continue;
                                float ratio = power / (single / singles);
                                int bucket = LandBuckets.Length - 1;
                                while (bucket > 0 && lands < LandBuckets[bucket])
                                    bucket--;
                                bucketRatio[bucket] += ratio;
                                bucketLands[bucket] += lands;
                                bucketCount[bucket]++;
                                if (lands > 1)
                                {
                                    perLandSum += (ratio - 1f) / (lands - 1);
                                    perLandCount++;
                                }
                            }
                        }

                        // ② 플레이어 — 고정 승률 주사위
                        List<Lord> neighbors = state.Neighbors(state.Player);
                        if (neighbors.Count > 0)
                        {
                            Lord target = neighbors[0];
                            foreach (Lord other in neighbors)
                                if (biggestFirst ? state.LandsOf(other) > state.LandsOf(target) : state.LandsOf(other) < state.LandsOf(target))
                                    target = other;
                            // 침략하는 땅 = 그 영주의 땅 중 내 땅과 국경을 맞댄 첫 땅
                            int region = RegionOf(state, target);
                            bool coalitionBattle = target == state.Coalition;
                            bool won = random.NextDouble() < playerWin;
                            WorldTurn.Invade(state, state.Player, region, won);
                            battles++;
                            if (coalitionBattle)
                            {
                                coalitionBattles++;
                                if (state.GameOver)
                                    coalitionOver++;
                            }
                        }
                        if (state.GameOver)
                        {
                            over++;
                            finished = true;
                            break;
                        }
                        if (state.Unified)
                        {
                            clearTurns.Add(state.Turn);
                            finished = true;
                            break;
                        }

                        run.AiPhase((attacker, target, region, won) =>
                        {
                            buildAttacks[attacker.Build]++;
                            temperAttacks[(int)attacker.Temper]++;
                            buildDefends[target.Build]++;
                            if (won)
                            {
                                buildWins[attacker.Build]++;
                                temperWins[(int)attacker.Temper]++;
                            }
                            else
                                buildHeld[target.Build]++;
                        });
                        run.Settle();
                        // 최후의 연합 — 뭉친 시점 · 영주 수 · 땅 수와, 연합 탑이 뭉치기 직전 가장 센 영주의 탑보다 얼마나 센지
                        float strongest = 0f;
                        foreach (Lord lord in state.Lords)
                            if (lord.Alive && !lord.IsPlayer)
                                strongest = Mathf.Max(strongest, run.PowerOf(lord));
                        merged.Clear();
                        Lord coalition = run.FormCoalition(merged);
                        if (coalition != null)
                        {
                            coalitions++;
                            coalitionTurn += state.Turn;
                            coalitionMembers += merged.Count + 1;
                            coalitionLands += state.LandsOf(coalition);
                            coalitionFloors += run.TowerOf(coalition).Field.Length;
                            if (strongest > 0f)
                                coalitionRatio += run.PowerOf(coalition) / strongest;
                        }

                        if (t < Shown)
                        {
                            lordsAfter[t] += state.AiLords;
                            lordsSamples[t]++;
                        }
                        if (state.Turn == 3 || state.Turn == 5)
                            for (int id = 0; id < regions; id++)
                            {
                                Lord lord = state.Lords[id];
                                if (lord.IsPlayer || !lord.Alive)
                                    continue;
                                if (state.Turn == 3)
                                {
                                    buildAlive3[lord.Build]++;
                                    temperAlive3[(int)lord.Temper]++;
                                }
                                else
                                {
                                    buildAlive5[lord.Build]++;
                                    temperAlive5[(int)lord.Temper]++;
                                }
                            }
                    }
                    if (!finished)
                        unfinished++;
                }
            }
            finally
            {
                EnemyShopper.Quiet = quiet;
            }
            watch.Stop();

            clearTurns.Sort();
            float average = 0f;
            foreach (int turns in clearTurns)
                average += turns;
            var text = new StringBuilder($"[World] 월드 시뮬레이션 — {runs}판 · 판당 최대 {maxTurns}턴 · 플레이어 승률 {playerWin:0.##}({(biggestFirst ? "큰 영주부터" : "작은 영주부터")}) · 씨앗 {seed} · {watch.ElapsedMilliseconds}ms(판당 {watch.ElapsedMilliseconds / (float)runs:0}ms)\n");
            text.Append(clearTurns.Count == 0
                ? "통일 0판\n"
                : $"통일 {clearTurns.Count}판 — 턴 평균 {average / clearTurns.Count:0.0} · 최소 {clearTurns[0]} · 10% {clearTurns[(int)(clearTurns.Count * 0.1f)]} · 90% {clearTurns[Mathf.Min(clearTurns.Count - 1, (int)(clearTurns.Count * 0.9f))]} · 최대 {clearTurns[clearTurns.Count - 1]}\n");
            text.Append($"게임 오버 {over}판({over * 100f / runs:0.#}%, 그중 연합전 {coalitionOver}판) · 미종료 {unfinished}판 · 내 전투 판당 {battles / (float)runs:0.0}번(연합전 {coalitionBattles / (float)runs:0.0}번)\n");
            text.Append(coalitions == 0
                ? "최후의 연합 0판\n"
                : $"최후의 연합 {coalitions}판({coalitions * 100f / runs:0.#}%) — 뭉친 턴 평균 {coalitionTurn / coalitions:0.0} · 영주 {coalitionMembers / coalitions:0.0}명 · 땅 {coalitionLands / coalitions:0.0}개 · 탑 {coalitionFloors / coalitions:0.0}층 · 전투력 = 뭉치기 직전 가장 센 영주의 ×{coalitionRatio / coalitions:0.00}\n");
            text.Append("턴별 AI 영주 수(턴 끝):");
            for (int i = 0; i < Shown; i++)
                text.Append(lordsSamples[i] > 0 ? $" {lordsAfter[i] / lordsSamples[i]:0.0}" : " -");
            text.Append("\n턴별 탑 전투력(상점 뒤) 평균[최소~최대] · 보정 몫:");
            for (int i = 0; i < Shown; i++)
                if (powerCount[i] > 0)
                    text.Append($" | {i + 1}: {powerSum[i] / powerCount[i]:0.0}[{powerMin[i]:0.0}~{powerMax[i]:0.0}] {supportShare[i] * 100f / powerCount[i]:0}%");
            text.Append("\n땅 수 → 힘(같은 턴의 땅 1개 영주 평균 대비 배율 · 괄호 = 「땅 하나당 +15%」 가정의 값):");
            for (int b = 0; b < LandBuckets.Length; b++)
                if (bucketCount[b] > 0)
                {
                    float lands = bucketLands[b] / bucketCount[b];
                    text.Append($" | 땅 {LandBuckets[b]}{(b == LandBuckets.Length - 1 ? "+" : b + 1 < LandBuckets.Length && LandBuckets[b + 1] - LandBuckets[b] > 1 ? "~" + (LandBuckets[b + 1] - 1) : "")}(평균 {lands:0.0}개, 표본 {bucketCount[b]}): ×{bucketRatio[b] / bucketCount[b]:0.00} (×{1f + 0.15f * (lands - 1f):0.00})");
                }
            text.Append(perLandCount > 0 ? $"\n땅 하나가 늘 때마다의 힘 증가(땅 2개 이상 영주 평균): +{perLandSum * 100f / perLandCount:0.#}%\n" : "\n");
            text.Append("빌드 방향 | 시작 영주 수 · 3턴 생존 · 5턴 생존 · 침략 승률(이김/침략) · 막아 낸 비율(막음/침략당함)\n");
            for (int b = 0; b < builds; b++)
                text.Append($"{world.Map.BuildOf(b).DisplayName} | {buildStart[b]} · {Percent(buildAlive3[b], buildStart[b])} · {Percent(buildAlive5[b], buildStart[b])} · {Percent(buildWins[b], buildAttacks[b])}({buildWins[b]}/{buildAttacks[b]}) · {Percent(buildHeld[b], buildDefends[b])}({buildHeld[b]}/{buildDefends[b]})\n");
            text.Append("성향 | 시작 영주 수 · 3턴 생존 · 5턴 생존 · 침략 승률(이김/침략)\n");
            string[] temperNames = { "약탈자", "야심가", "무모한 자", "안전주의자" };
            for (int i = 0; i < Tempers; i++)
                text.Append($"{temperNames[i]} | {temperStart[i]} · {Percent(temperAlive3[i], temperStart[i])} · {Percent(temperAlive5[i], temperStart[i])} · {Percent(temperWins[i], temperAttacks[i])}({temperWins[i]}/{temperAttacks[i]})\n");
            return text.ToString();
        }

        // target의 땅 중 플레이어의 땅과 국경을 맞댄 첫 땅
        private static int RegionOf(WorldState state, Lord target)
        {
            for (int region = 0; region < state.Owner.Length; region++)
                if (state.OwnerOf(region) == target)
                    foreach (int next in state.Graph.Links[region])
                        if (state.OwnerOf(next) == state.Player)
                            return region;
            return -1;
        }

        private static string Percent(int part, int whole) => whole > 0 ? $"{part * 100f / whole:0}%" : "-";
    }
}

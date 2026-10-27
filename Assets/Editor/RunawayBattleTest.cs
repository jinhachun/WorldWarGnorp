using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GnorpWar.Editor
{
    // 폭주 시험(10/26) — 업그레이드를 최대치로 쌓은 탑끼리 실제 전투를 차례로 돌리고, 안전 상한(Battle/ChainGuard)이 정상 빌드에서는 안 걸리는지 · 폭주 빌드에서는 걸려 게임이 멈추지 않는지 잰다.
    // 실제 전투가 필요해서(물리 · 씬의 땅 · 탑) 테스트 러너가 아니라 플레이 중에 돈다: 플레이를 켠 뒤 메뉴 「GnorpWar → 폭주 시험 전투」(또는 RunawayBattleTest.Run()).
    // 판마다 새 런 → 탑 · 업그레이드를 덮어쓰고 침략 → Battle 씬을 빠르게(Speed배) → 끝나면 다음 판. 런 기록은 Ghosts/_개발검증에 남긴다.
    // 결과: 콘솔 [폭주 시험] 줄 + Temp/Runaway/<시각>.txt. 통과 = 에러 0 · 판마다 게임 시간 TimeLimit초 안에 끝남 · 유닛 수 상한 안 · 정상 빌드는 상한 경고 0
    public static class RunawayBattleTest
    {
        private const float Speed = 6f;
        private const float TimeLimit = 320f;       // 게임 시간 — 데스매치 180 · 기물 멈춤 190 · 교착 240초 뒤에도 끝나야 한다
        private const double WallLimit = 360.0;     // 실제 시간 — 한 판이 이보다 오래 걸리면 끊고 실패로 적는다(종료 조건)
        private const int Copies = 3;               // 업그레이드마다 얻는 횟수(전설 등급) — 합 상한(_max)은 Summarize가 자른다

        private class Case
        {
            public string Name;
            public bool Normal;   // 정상 빌드 — 상한 경고가 0이어야 한다
            public BuildingNation Ally, Enemy;
            public string[] AllyBuildings;   // 비우면 그 국가에서 소환 · 발동 계열 위주로 고른다
            public string[] AllyExtraUpgrades = new string[0];
        }

        private static readonly Case[] Cases =
        {
            new Case { Name = "킹덤 최대 vs 티크톡 최대", Normal = true, Ally = BuildingNation.Kingdom, Enemy = BuildingNation.TickTock },
            new Case { Name = "티크톡 최대 vs 트라이브 최대", Normal = true, Ally = BuildingNation.TickTock, Enemy = BuildingNation.Tribe },
            new Case { Name = "트라이브 최대 vs 킹덤 최대", Normal = true, Ally = BuildingNation.Tribe, Enemy = BuildingNation.Kingdom },
            // 10/25 사고 재현 — 짐승 몰이 100% + 군제 개혁·중장보병(경보병도 중기병) — 졸병이 다시 중기병으로 굴린다
            new Case
            {
                Name = "짐승 몰이 100% 재현", Normal = false, Ally = BuildingNation.Tribe, Enemy = BuildingNation.Kingdom,
                AllyBuildings = new[] { "TribeOutpost", "TribeBoneGrave", "TribeWarDrum", "TribeWarriorHut", "TribeBeastGrave", "TribeTundraMarch", "TribeLabyrinth", "TribeMammothRitual" },
                AllyExtraUpgrades = new[] { "Special/Special_ReformHeavyInfantry" },
            },
            // 죽음 연계 — 거대 괴수의 무덤 신화(중기병이 죽으면 전사 5기) + 군제 개혁·중장보병(경보병인 전사도 중기병). 연계는 한 번만(10/26) 전엔 죽음마다 5배로 불었다 — 이제 상한에 안 닿아야 한다
            new Case
            {
                Name = "무덤 신화 + 군제 개혁·중장보병", Normal = true, Ally = BuildingNation.Tribe, Enemy = BuildingNation.Kingdom,
                AllyBuildings = new[] { "TribeBeastGrave", "TribeOutpost", "TribeWarriorHut", "TribeBoneGrave", "TribeWarDrum", "TribeTundraMarch", "TribeMinotaurChief", "TribeWeapon" },
                AllyExtraUpgrades = new[] { "Special/Special_ReformHeavyInfantry" },
            },
            // 소환 수 쌓기 — 제물의 의식(신화: 바로 위 기물 소환 +2 · 쿨다운 +1초, 10/27 리메이크) 셋을 생산 기물 아래마다 붙이고 판도라의 상자를 끼운다
            new Case
            {
                Name = "제물의 의식 셋 + 판도라", Normal = false, Ally = BuildingNation.Tribe, Enemy = BuildingNation.TickTock,
                AllyBuildings = new[] { "TribeSacrifice", "TribeBoneGrave", "TribeSacrifice", "TribeWarriorHut", "TribeSacrifice", "TribeOutpost", "Pandora", "TribeWarDrum" },
            },
        };

        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

        public static string Status { get; private set; } = "안 돌림";

        [MenuItem("GnorpWar/폭주 시험 전투 (플레이 중)")]
        private static void Menu() => Run();

        // only = 판 이름에 이 글이 든 것만(비우면 전부)
        // loose = 상한을 이 값들로 잠시 넓혀(메모리에서만 — 에셋은 저장하지 않는다) 정상 빌드의 자연 최대를 잰다. 끝나면 되돌린다
        public static string Run(string only = null, bool loose = false)
        {
            if (!EditorApplication.isPlaying)
                return Status = "플레이 중이 아님 — 플레이를 켠 뒤 다시";
            var cases = new List<Case>();
            foreach (Case c in Cases)
                if (string.IsNullOrEmpty(only) || c.Name.Contains(only))
                    cases.Add(c);
            BattleConfig config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            string[] caps = { "_maxSummonDepth", "_maxStepSummons", "_maxUnits", "_maxFireDepth" };
            int[] looseCaps = { 32, 2000, 1500, 32 };
            var saved = new int[caps.Length];
            for (int i = 0; i < caps.Length; i++)
            {
                FieldInfo field = typeof(BattleConfig).GetField(caps[i], BindingFlags.NonPublic | BindingFlags.Instance);
                saved[i] = (int)field.GetValue(config);
                if (loose)
                    field.SetValue(config, looseCaps[i]);
            }
            Directory.CreateDirectory("Temp/Runaway");
            string path = $"Temp/Runaway/{System.DateTime.Now:HHmmss}.txt";
            var log = new StringBuilder();
            log.AppendLine($"폭주 시험 — 상한: 소환 깊이 {config.MaxSummonDepth} · 스텝 소환 {config.MaxStepSummons} · 유닛 {config.MaxUnits} · 발동 깊이 {config.MaxFireDepth} · 속도 ×{Speed}");

            int index = -1, errors = 0, failed = 0, countChecks = 0, countMismatch = 0;
            float nextCountCheck = 0f;
            bool waitingStart = false;
            double wallStart = 0;
            var errorLines = new List<string>();
            Application.LogCallback onLog = (text, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    errors++;
                    if (errorLines.Count < 5)
                        errorLines.Add(text.Split('\n')[0]);
                }
            };
            Application.logMessageReceived += onLog;

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
                    {
                        log.AppendLine($"[{cases[index].Name}] 전투가 열리지 않음");
                        failed++;
                        Next();
                    }
                    return;
                }
                if (index >= 0)
                {
                    bool over = RunState.Phase != RunState.RunPhase.Battle;
                    bool wallOut = EditorApplication.timeSinceStartup - wallStart > WallLimit;
                    if (fighting)
                    {
                        Time.timeScale = Speed;
                        LastElapsed = BattleManager.Elapsed;
                        // 유닛 수 세기 검산 — 상한이 세는 수(ChainGuard)와 실제로 살아 있는 유닛 수가 같아야 한다(죽음 · 부활 · 소모 · 변신을 지나도). 게임 1초마다
                        if (LastElapsed >= nextCountCheck)
                        {
                            nextCountCheck = LastElapsed + 1f;
                            int ally = 0, enemy = 0;
                            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                                if (unit.IsAlive)
                                {
                                    if (unit.Team == Team.Ally)
                                        ally++;
                                    else
                                        enemy++;
                                }
                            countChecks++;
                            countMismatch = Mathf.Max(countMismatch, Mathf.Abs(ally - ChainGuard.UnitsOf(Team.Ally)), Mathf.Abs(enemy - ChainGuard.UnitsOf(Team.Enemy)));
                        }
                    }
                    if (!over && !wallOut)
                        return;
                    Report(cases[index], over, (float)(EditorApplication.timeSinceStartup - wallStart));
                }
                Next();
            };

            void Next()
            {
                index++;
                if (index >= cases.Count)
                {
                    log.AppendLine(failed == 0 ? "통과" : $"실패 {failed}판");
                    Finish();
                    return;
                }
                errors = 0;
                errorLines.Clear();
                LastElapsed = 0f;
                nextCountCheck = 0f;
                countChecks = countMismatch = 0;
                Setup(config, cases[index]);
                waitingStart = true;
                wallStart = EditorApplication.timeSinceStartup;
                SceneManager.LoadScene("Battle");
            }

            void Report(Case c, bool over, float wall)
            {
                float elapsed = BattleManager.Fighting ? BattleManager.Elapsed : LastElapsed;
                bool ok = over && errors == 0 && elapsed <= TimeLimit && ChainGuard.PeakUnits <= config.MaxUnits && countMismatch == 0 && (!c.Normal || ChainGuard.Warnings.Count == 0);
                if (!ok)
                    failed++;
                log.AppendLine($"[{c.Name}] {(ok ? "통과" : "실패")} — 끝남 {over} · 결과 {RunState.LastOutcome} · 게임 {elapsed:0}초 · 실제 {wall:0}초 · 에러 {errors}"
                               + $" · 최대: 소환 깊이 {ChainGuard.PeakSummonDepth} · 스텝 소환 {ChainGuard.PeakStepSummons} · 유닛 {ChainGuard.PeakUnits} · 발동 깊이 {ChainGuard.PeakFireDepth} · 유닛 수 검산 {countChecks}번 중 최대 어긋남 {countMismatch}");
                foreach (string w in ChainGuard.Warnings)
                    log.AppendLine("    경고: " + w);
                foreach (string e in errorLines)
                    log.AppendLine("    에러: " + e);
                Debug.Log("[폭주 시험] " + log.ToString().TrimEnd().Split('\n')[^1]);
            }

            void Finish()
            {
                EditorApplication.update -= tick;
                Application.logMessageReceived -= onLog;
                for (int i = 0; i < caps.Length; i++)
                    typeof(BattleConfig).GetField(caps[i], BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, saved[i]);
                File.WriteAllText(path, log.ToString());
                Status = log.ToString();
                Debug.Log("[폭주 시험] 끝 — " + path + "\n" + log);
            }

            EditorApplication.update += tick;
            return Status = "도는 중 — 결과는 " + path;
        }

        // 전투가 끝나면 Elapsed가 0이 된다 — 싸우는 동안 마지막 값을 적어 둔다
        private static float LastElapsed;

        private static void Setup(BattleConfig config, Case c)
        {
            RunState.CheatNewRun();
            RunState.ChooseNation(c.Ally);
            typeof(RunState).GetField("_record", Static).SetValue(null, new RunRecord(Path.Combine(RunRecord.Folder, "_개발검증"), config.AssetIds));

            RunState.CheatSetFloors(RunState.MaxFloors);
            OwnedBuilding[] ally = c.AllyBuildings != null ? Named(c.AllyBuildings) : Picked(config, c.Ally, RunState.MaxFloors);
            for (int i = 0; i < RunState.Field.Length; i++)
                RunState.Field[i] = i < ally.Length ? ally[i] : null;
            var player = (TowerState)typeof(RunState).GetField("Player", Static).GetValue(null);
            Gain(config, player, c.Ally, c.AllyExtraUpgrades);

            RunState.CheatEnemyField = Picked(config, c.Enemy, RunState.MaxFloors);
            int region = -1;
            foreach (int r in RunState.InvadableRegions())
                if (RunState.World.TowerOf(RunState.World.State.OwnerOf(r)) != null)
                {
                    region = r;
                    break;
                }
            RunState.Invade(region);
            var enemy = (TowerState)typeof(RunState).GetField("_opponentTower", Static).GetValue(null);
            Gain(config, enemy, c.Enemy, new string[0]);
        }

        // 최대 등급(신화)으로
        private static OwnedBuilding Max(BuildingDefinition definition)
            => new OwnedBuilding(definition, (int)BuildingRarity.Mythic - (int)definition.Rarity, 0, 0, 1f);

        private static OwnedBuilding[] Named(string[] names)
        {
            var field = new OwnedBuilding[names.Length];
            for (int i = 0; i < names.Length; i++)
                field[i] = Max(AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/Data/Buildings/Building_{names[i]}.asset"));
            return field;
        }

        // 그 국가(+ 공용) 상점 기물 중 소환 · 발동 계열 위주로 floors개 — 점수(유닛을 낸다 · 이름에 Summon · Fire가 든 능력) 높은 것부터, 같으면 국가 것 · 높은 등급
        private static OwnedBuilding[] Picked(BattleConfig config, BuildingNation nation, int floors)
        {
            var pool = new List<BuildingDefinition>();
            foreach (BuildingDefinition b in config.ShopPool)
                if (b != null && (b.Nation == nation || b.Nation == BuildingNation.Common) && !pool.Contains(b))
                    pool.Add(b);
            pool.Sort((a, b) =>
            {
                int s = Score(b).CompareTo(Score(a));
                if (s != 0)
                    return s;
                int n = (b.Nation == nation).CompareTo(a.Nation == nation);
                return n != 0 ? n : b.Rarity.CompareTo(a.Rarity);
            });
            var field = new OwnedBuilding[Mathf.Min(floors, pool.Count)];
            for (int i = 0; i < field.Length; i++)
                field[i] = Max(pool[i]);
            return field;
        }

        private static int Score(BuildingDefinition b)
        {
            var parts = new List<Object>();
            parts.AddRange(b.Actions);
            parts.AddRange(b.MythicActions);
            parts.AddRange(b.Passives);
            parts.AddRange(b.MythicPassives);
            parts.AddRange(b.Effects);
            parts.AddRange(b.MythicEffects);
            int score = b.Unit != null ? 2 : 0;
            foreach (Object part in parts)
            {
                string type = part != null ? part.GetType().Name : "";
                if (type.Contains("Summon") || type.Contains("Fire"))
                    score++;
            }
            return score;
        }

        // 그 국가(+ 공용) 합치기 보상 업그레이드를 전부 전설 등급으로 Copies번씩(전설에 값이 없으면 값이 있는 가장 높은 등급) + 덧붙일 것(Upgrades 폴더 기준 경로)
        private static void Gain(BattleConfig config, TowerState tower, BuildingNation nation, string[] extra)
        {
            tower.Upgrades.Clear();
            foreach (UpgradeDefinition upgrade in config.Upgrades)
            {
                if (upgrade == null || !upgrade.InPool || (upgrade.Nation != BuildingNation.Common && upgrade.Nation != nation))
                    continue;
                BuildingRarity rarity = BuildingRarity.Legendary;
                while (rarity > BuildingRarity.Common && !upgrade.AvailableAt(rarity))
                    rarity--;
                for (int i = 0; i < Copies; i++)
                    tower.Upgrades.Add(new UpgradeGain { Definition = upgrade, Rarity = rarity });
            }
            foreach (string name in extra)
                tower.Upgrades.Add(new UpgradeGain { Definition = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>($"Assets/Data/Upgrades/{name}.asset"), Rarity = BuildingRarity.Mythic });
        }
    }
}

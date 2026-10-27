using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GnorpWar.Editor
{
    // 무적 비율 시험(플레이 중 — 메뉴 「GnorpWar → 무적 비율 시험 (플레이 중)」, 사용자 10/13 「상시 무적은 안 된다」).
    // 판마다 지정한 기물(신화)만 놓고 기본 맵에서 킹덤 vs 티크톡 최대로 Seconds초 싸운다. 업그레이드는 없다(기물 값만 본다).
    // 잰다: 내 경보병 하나하나의 「살아 있던 시간 중 무적인 시간」 비율(유닛 평균 · 90% 넘는 유닛 수 · 시간 가중) · 피해 입은 프레임 중 그 프레임에 무적이 켜진 비율(결사대 확률) ·
    // 한 프레임에 무적이 켜진 아군 수 최대(공주의 축복 대상 수) · 새 기사 수(이 빌드에서 기사는 승급으로만 나온다) · 전원 무적 프레임(반군의 깃발)의 최소 간격 · 병사의 체력 · 이동속도 배율.
    // 첫 승급 순간과 그 직후를 캡처한다(Captures/Royalist/). 결과는 Temp/InvulnerableShare/ — 종료 조건: 판마다 실제 시간 WallLimit초
    public static class InvulnerableShareTest
    {
        private const float Speed = 3f;
        private const double WallLimit = 120.0;
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

        private class Case
        {
            public string Name;
            public float Seconds = 60f;
            public string[] Buildings;
            public int[] Ups;   // 오른 횟수(비우면 신화까지)
            public bool Capture;
        }

        private static readonly string[] Full = { "RebelBarracks", "ForlornHope", "Accolade", "RebelBanner", "Blessing", "Oath" };

        private static readonly Case[] Cases =
        {
            new Case { Name = "막사 단독", Seconds = 20f, Buildings = new[] { "RebelBarracks" } },
            new Case { Name = "결사대 단독", Seconds = 60f, Buildings = new[] { "RebelBarracks", "ForlornHope" } },
            new Case { Name = "축복 희귀", Seconds = 20f, Buildings = new[] { "RebelBarracks", "RoyalConscription", "Blessing" }, Ups = new[] { 4, 4, 0 } },
            new Case { Name = "축복 영웅", Seconds = 20f, Buildings = new[] { "RebelBarracks", "RoyalConscription", "Blessing" }, Ups = new[] { 4, 4, 1 } },
            new Case { Name = "축복 전설", Seconds = 20f, Buildings = new[] { "RebelBarracks", "RoyalConscription", "Blessing" }, Ups = new[] { 4, 4, 2 } },
            new Case { Name = "축복 신화", Seconds = 20f, Buildings = new[] { "RebelBarracks", "RoyalConscription", "Blessing" } },
            new Case { Name = "전부 신화 1", Buildings = Full, Capture = true },
            new Case { Name = "전부 신화 2", Buildings = Full },
            new Case { Name = "전부 신화 3", Buildings = Full },
        };

        private class Track
        {
            public float Alive, Invulnerable;
            public float LastHp;
            public bool WasInvulnerable;
        }

        [MenuItem("GnorpWar/무적 비율 시험 (플레이 중)")]
        private static void Menu() => Run();

        public static string Run(string only = null)
        {
            if (!EditorApplication.isPlaying)
                return "플레이 중이 아님";
            var cases = new List<Case>();
            foreach (Case c in Cases)
                if (string.IsNullOrEmpty(only) || c.Name.Contains(only))
                    cases.Add(c);
            BattleConfig config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            UnitCategory light = AssetDatabase.LoadAssetAtPath<UnitCategory>("Assets/Data/Units/Category_LightInfantry.asset");
            UnitDefinition knight = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Unit_Knight.asset");
            UnitDefinition melee = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Unit_Melee.asset");
            Directory.CreateDirectory("Temp/InvulnerableShare");
            Directory.CreateDirectory("Captures/Royalist");
            string path = $"Temp/InvulnerableShare/{System.DateTime.Now:HHmmss}.txt";
            var log = new StringBuilder($"무적 비율 시험 — 속도 ×{Speed}\n");

            int index = -1;
            bool waiting = false;
            double wallStart = 0;
            var tracks = new Dictionary<(int, int), Track>();
            var knights = new HashSet<(int, int)>();
            int promotions = 0, damageFrames = 0, hopeFrames = 0, maxOnset = 0, captures = 0;
            float lastRally = -99f, minRallyGap = float.MaxValue, captureAt = -1f;
            int rallies = 0;

            EditorApplication.CallbackFunction tick = null;
            void Finish()
            {
                EditorApplication.update -= tick;
                Time.timeScale = 1f;
                File.WriteAllText(path, log.ToString());
                Debug.Log("[무적 비율] 끝 — " + path + "\n" + log);
            }

            void Next()
            {
                index++;
                if (index >= cases.Count)
                {
                    Finish();
                    return;
                }
                tracks.Clear();
                knights.Clear();
                promotions = damageFrames = hopeFrames = maxOnset = rallies = captures = 0;
                lastRally = -99f;
                minRallyGap = float.MaxValue;
                captureAt = -1f;
                Setup(cases[index]);
                waiting = true;
                wallStart = EditorApplication.timeSinceStartup;
                SceneManager.LoadScene("Battle");
            }

            void Report(Case c)
            {
                int units = 0, over90 = 0;
                float sumRatio = 0f, alive = 0f, invulnerable = 0f;
                foreach (Track t in tracks.Values)
                {
                    alive += t.Alive;
                    invulnerable += t.Invulnerable;
                    if (t.Alive < 1f)
                        continue;   // 1초도 못 산 유닛은 비율이 뜻이 없다
                    units++;
                    float r = t.Invulnerable / t.Alive;
                    sumRatio += r;
                    if (r > 0.9f)
                        over90++;
                }
                float hp = Unit.SpawnStat(Team.Ally, melee, UnitStat.MaxHp) / melee.MaxHp;
                float speed = Unit.SpawnStat(Team.Ally, melee, UnitStat.MoveSpeed) / melee.MoveSpeed;
                log.AppendLine($"[{c.Name}] 게임 {BattleManager.Elapsed:0}초 · 경보병 {units}기(1초 이상 산) · 무적 비율 유닛 평균 {(units > 0 ? sumRatio / units : 0f):P0} · 90% 넘는 유닛 {over90}기 · 시간 가중 {(alive > 0f ? invulnerable / alive : 0f):P0}"
                               + $" · 피해 프레임 {damageFrames} 중 그 프레임에 무적 켜짐 {hopeFrames}({(damageFrames > 0 ? (float)hopeFrames / damageFrames : 0f):P0})"
                               + $" · 한 프레임 무적 켜진 아군 최대 {maxOnset} · 새 기사(승급) {promotions} · 전원 무적(승급 프레임에 경보병 4기 이상 동시) {rallies}번 최소 간격 {(rallies > 1 ? minRallyGap.ToString("0.0") : "-")}초"
                               + $" · 병사 체력 ×{hp:0.##} 이동속도 ×{speed:0.##}");
            }

            tick = () =>
            {
                if (!EditorApplication.isPlaying)
                {
                    log.AppendLine("플레이가 꺼져 멈춤");
                    Finish();
                    return;
                }
                if (index >= 0 && waiting)
                {
                    if (BattleManager.Fighting)
                        waiting = false;
                    else if (EditorApplication.timeSinceStartup - wallStart > 30)
                    {
                        log.AppendLine($"[{cases[index].Name}] 전투가 열리지 않음");
                        Next();
                    }
                    return;
                }
                if (index >= 0)
                {
                    Case c = cases[index];
                    bool fighting = BattleManager.Fighting;
                    if (fighting && captureAt >= 0f && Time.unscaledTime >= captureAt)
                    {
                        // 승급 순간을 멈춰 두고 찍는다 — 한 장 찍으면 잠깐 흘려 보내고 한 장 더(무적 고리 · 기둥이 보이게)
                        ScreenCapture.CaptureScreenshot($"Captures/Royalist/promotion_{++captures}.png");
                        captureAt = captures < 2 ? Time.unscaledTime + 0.25f : -1f;
                        Time.timeScale = captures < 2 ? 0.3f : Speed;
                    }
                    else if (fighting && captureAt < 0f)
                        Time.timeScale = Speed;
                    if (fighting)
                    {
                        float dt = Time.deltaTime;
                        int onset = 0, lightOnset = 0;
                        bool promotedNow = false;
                        foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                        {
                            if (!unit.IsAlive || unit.Team != Team.Ally)
                                continue;
                            var key = (unit.GetInstanceID(), unit.Life);
                            if (unit.Definition == knight && knights.Add(key))
                            {
                                promotions++;
                                promotedNow = true;
                                if (c.Capture && captures == 0 && captureAt < 0f)
                                    captureAt = Time.unscaledTime;
                            }
                            bool isLight = UnitAliases.Is(Team.Ally, unit.Definition, light);
                            if (!tracks.TryGetValue(key, out Track t))
                            {
                                t = new Track { LastHp = unit.Hp, WasInvulnerable = unit.IsInvulnerable };
                                if (isLight)
                                    tracks[key] = t;
                                if (unit.IsInvulnerable)
                                    onset++;
                                continue;
                            }
                            if (unit.IsInvulnerable && !t.WasInvulnerable)
                            {
                                onset++;
                                lightOnset++;
                                if (unit.Hp < t.LastHp)
                                    hopeFrames++;
                            }
                            if (unit.Hp < t.LastHp && !t.WasInvulnerable)
                                damageFrames++;
                            t.Alive += dt;
                            if (unit.IsInvulnerable)
                                t.Invulnerable += dt;
                            t.LastHp = unit.Hp;
                            t.WasInvulnerable = unit.IsInvulnerable;
                        }
                        maxOnset = Mathf.Max(maxOnset, onset);
                        if (lightOnset >= 4 && promotedNow)
                        {
                            float now = BattleManager.Elapsed;
                            if (now - lastRally > 0.1f)
                            {
                                if (rallies > 0)
                                    minRallyGap = Mathf.Min(minRallyGap, now - lastRally);
                                rallies++;
                                lastRally = now;
                            }
                        }
                    }
                    bool over = RunState.Phase != RunState.RunPhase.Battle || (fighting && BattleManager.Elapsed >= c.Seconds);
                    if (!over && EditorApplication.timeSinceStartup - wallStart <= WallLimit)
                        return;
                    Report(c);
                }
                Next();
            };
            EditorApplication.update += tick;
            Next();
            return "도는 중 — 결과는 " + path;
        }

        private static void Setup(Case c)
        {
            BattleConfig config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            RunState.CheatNewRun();
            RunState.CheatSetMap(AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Data/Maps/Map_Plain.asset"));   // 맵 효과 없이(늪지는 쿨다운 없는 기물을 끈다)
            RunState.ChooseNation(BuildingNation.Kingdom);
            typeof(RunState).GetField("_record", Static).SetValue(null, new RunRecord(Path.Combine(RunRecord.Folder, "_개발검증"), config.AssetIds));
            RunState.CheatSetFloors(RunState.MaxFloors);
            for (int i = 0; i < RunState.Field.Length; i++)
            {
                RunState.Field[i] = null;
                if (i >= c.Buildings.Length)
                    continue;
                var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/Data/Buildings/Building_{c.Buildings[i]}.asset");
                int ups = c.Ups != null ? c.Ups[i] : (int)BuildingRarity.Mythic - (int)definition.Rarity;
                RunState.Field[i] = new OwnedBuilding(definition, Mathf.Min(ups, (int)BuildingRarity.Mythic - (int)definition.Rarity), 0, 0, 1f);
            }
            var player = (TowerState)typeof(RunState).GetField("Player", Static).GetValue(null);
            player.Upgrades.Clear();
            RunState.CheatEnemyField = TickTock(config);
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

        // 상대 — 티크톡 생산 기물 상위(전설)로 탑을 채운다
        private static OwnedBuilding[] TickTock(BattleConfig config)
        {
            var field = new List<OwnedBuilding>();
            foreach (BuildingDefinition b in config.ShopPool)
                if (b != null && b.Nation == BuildingNation.TickTock && b.Unit != null && field.Count < RunState.MaxFloors)
                    field.Add(new OwnedBuilding(b, Mathf.Max(0, (int)BuildingRarity.Legendary - (int)b.Rarity), 0, 0, 1f));
            return field.ToArray();
        }
    }
}

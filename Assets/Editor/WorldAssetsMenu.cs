using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 월드맵 에셋 만들기(메뉴 GnorpWar/월드 에셋 만들기) — WorldMapTable의 표에서 Assets/Data/World에 업그레이드 6종 · 맵 한 장 · 설정을 만든다.
    // 이미 있는 에셋은 건드리지 않는다(손으로 고친 값을 덮어쓰지 않게) — 표를 고쳐 다시 만들려면 그 에셋을 지우고 부른다
    internal static class WorldAssetsMenu
    {
        private const string Folder = "Assets/Data/World";
        private const string UpgradeFolder = Folder + "/Upgrades";

        // 칸 순서 = WorldMapTable.TerrainLetters(I 얼음 · F 숲 · P 기본 · G 초원 · S 늪지 · J 정글) — 지형과 그 땅 영주의 빌드 방향(기획 §3 표)
        private static readonly string[] TerrainAssets = { "Map_Ice", "Map_Forest", "Map_Plain", "Map_Grassland", "Map_Swamp", "Map_Jungle" };
        private static readonly string[] BuildAssets =
        {
            "EnemyBuild_Economy", "EnemyBuild_Tower", "EnemyBuild_HeavyInfantry", "EnemyBuild_Cavalry", "EnemyBuild_Priest", "EnemyBuild_Cog",
        };

        // 칸 순서 = RegionUpgradeKind (기획 §4 「지금 넣는 6종」 — 전부 공용)
        private static readonly (string File, string Name, float Value, RegionUpgradeUse Use)[] Upgrades =
        {
            ("Upgrade_Weapon", "무기 강화", 4f, RegionUpgradeUse.Battle),
            ("Upgrade_Armor", "방어구 강화", 4f, RegionUpgradeUse.Battle),
            ("Upgrade_Supply", "보급력 강화", 0.97f, RegionUpgradeUse.Battle),
            ("Upgrade_Tech", "기술 연구", 1f, RegionUpgradeUse.Operation),
            ("Upgrade_Tax", "세금 증산", 2f, RegionUpgradeUse.Operation),
            ("Upgrade_Nationalism", "민족주의", 10f, RegionUpgradeUse.Battle),
        };

        [MenuItem("GnorpWar/월드 에셋 만들기")]
        private static void Create()
        {
            var terrains = new MapDefinition[TerrainAssets.Length];
            var builds = new EnemyBuildProfile[BuildAssets.Length];
            for (int i = 0; i < terrains.Length; i++)
            {
                terrains[i] = AssetDatabase.LoadAssetAtPath<MapDefinition>($"Assets/Data/Maps/{TerrainAssets[i]}.asset");
                builds[i] = AssetDatabase.LoadAssetAtPath<EnemyBuildProfile>($"Assets/Data/EnemyBuilds/{BuildAssets[i]}.asset");
                if (terrains[i] == null || builds[i] == null)
                {
                    Debug.LogError($"[World] 에셋을 못 찾았다 — {TerrainAssets[i]} 또는 {BuildAssets[i]}. 아무것도 만들지 않았다");
                    return;
                }
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Data", "World");
            if (!AssetDatabase.IsValidFolder(UpgradeFolder))
                AssetDatabase.CreateFolder(Folder, "Upgrades");

            int made = 0;
            var upgrades = new RegionUpgradeDefinition[Upgrades.Length];
            for (int i = 0; i < upgrades.Length; i++)
            {
                int kind = i;
                upgrades[i] = LoadOrCreate<RegionUpgradeDefinition>($"{UpgradeFolder}/{Upgrades[i].File}.asset", ref made, so =>
                {
                    so.FindProperty("_displayName").stringValue = Upgrades[kind].Name;
                    so.FindProperty("_kind").enumValueIndex = kind;
                    so.FindProperty("_value").floatValue = Upgrades[kind].Value;
                    so.FindProperty("_scope").enumValueIndex = (int)RegionUpgradeScope.Common;
                    so.FindProperty("_use").enumValueIndex = (int)Upgrades[kind].Use;
                });
            }

            WorldMapDefinition map = LoadOrCreate<WorldMapDefinition>($"{Folder}/WorldMap_Main.asset", ref made, so =>
            {
                int[][] links = WorldMapTable.Links();
                SerializedProperty regions = so.FindProperty("_regions");
                regions.arraySize = WorldMapTable.RegionCount;
                for (int i = 0; i < regions.arraySize; i++)
                {
                    SerializedProperty region = regions.GetArrayElementAtIndex(i);
                    region.FindPropertyRelative(nameof(WorldMapDefinition.Region.Terrain)).objectReferenceValue = terrains[WorldMapTable.TerrainOf(i)];
                    region.FindPropertyRelative(nameof(WorldMapDefinition.Region.Upgrade)).objectReferenceValue = upgrades[WorldMapTable.UpgradeOf(i)];
                    SerializedProperty to = region.FindPropertyRelative(nameof(WorldMapDefinition.Region.Links));
                    to.arraySize = links[i].Length;
                    for (int k = 0; k < links[i].Length; k++)
                        to.GetArrayElementAtIndex(k).intValue = links[i][k];
                }
                SerializedProperty rows = so.FindProperty("_terrainBuilds");
                rows.arraySize = terrains.Length;
                for (int i = 0; i < terrains.Length; i++)
                {
                    SerializedProperty row = rows.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative(nameof(WorldMapDefinition.TerrainBuild.Terrain)).objectReferenceValue = terrains[i];
                    row.FindPropertyRelative(nameof(WorldMapDefinition.TerrainBuild.Build)).objectReferenceValue = builds[i];
                }
            });

            // 수치는 WorldConfig 클래스의 첫 값 그대로 — 맵만 잇는다
            LoadOrCreate<WorldConfig>($"{Folder}/WorldConfig.asset", ref made, so => so.FindProperty("_map").objectReferenceValue = map);

            AssetDatabase.SaveAssets();
            Debug.Log($"[World] 월드 에셋 {made}개를 만들었다({Folder}) — 이미 있던 것은 그대로 뒀다");
        }

        // 있으면 그대로 돌려주고, 없으면 만들어 fill로 채운다
        private static T LoadOrCreate<T>(string path, ref int made, System.Action<SerializedObject> fill) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            made++;
            return asset;
        }
    }
}

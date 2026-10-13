using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 월드맵 에셋 만들기(메뉴 GnorpWar/월드 에셋 만들기) — WorldMapTable의 표에서 Assets/Data/World에 맵 한 장 · 설정을 만든다.
    // 이미 있는 에셋은 건드리지 않는다(손으로 고친 값을 덮어쓰지 않게) — 표를 고쳐 다시 만들려면 그 에셋을 지우고 부른다
    internal static class WorldAssetsMenu
    {
        private const string Folder = "Assets/Data/World";

        // 칸 순서 = WorldMapTable.TerrainLetters(I 얼음 · F 숲 · P 기본 · G 초원 · S 늪지 · J 정글) — 지형과 그 땅 영주의 빌드 방향(기획 §3 표)
        private static readonly string[] TerrainAssets = { "Map_Ice", "Map_Forest", "Map_Plain", "Map_Grassland", "Map_Swamp", "Map_Jungle" };
        private static readonly string[] BuildAssets =
        {
            "EnemyBuild_Economy", "EnemyBuild_Tower", "EnemyBuild_HeavyInfantry", "EnemyBuild_Cavalry", "EnemyBuild_Priest", "EnemyBuild_Cog",
        };
        // 그 지형의 영주가 대신 가질 수 있는 다른 방향(없으면 null) — 얼음 · 초원에 트라이브 둘(사용자 결정 10/14: 영주마다 그 지형의 방향 중 하나를 고르게 뽑는다)
        private static readonly string[] MoreBuildAssets = { "EnemyBuild_TribeHill", null, null, "EnemyBuild_TribeMammoth", null, null };

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

            int made = 0;
            WorldMapDefinition map = LoadOrCreate<WorldMapDefinition>($"{Folder}/WorldMap_Main.asset", ref made, so =>
            {
                SerializedProperty regions = so.FindProperty("_regions");
                regions.arraySize = WorldMapTable.RegionCount;
                for (int i = 0; i < regions.arraySize; i++)
                {
                    regions.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(WorldMapDefinition.Region.Terrain)).objectReferenceValue = terrains[WorldMapTable.TerrainOf(i)];
                }
                SerializedProperty rows = so.FindProperty("_terrainBuilds");
                rows.arraySize = terrains.Length;
                for (int i = 0; i < terrains.Length; i++)
                {
                    SerializedProperty row = rows.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative(nameof(WorldMapDefinition.TerrainBuild.Terrain)).objectReferenceValue = terrains[i];
                    row.FindPropertyRelative(nameof(WorldMapDefinition.TerrainBuild.Build)).objectReferenceValue = builds[i];
                    SerializedProperty more = row.FindPropertyRelative(nameof(WorldMapDefinition.TerrainBuild.MoreBuilds));
                    more.arraySize = MoreBuildAssets[i] != null ? 1 : 0;
                    if (MoreBuildAssets[i] != null)
                        more.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyBuildProfile>($"Assets/Data/EnemyBuilds/{MoreBuildAssets[i]}.asset");
                }
            });
            // 새로 만든 맵에는 땅 모양 · 이웃 · 표시 자리를 굽는다
            if (made > 0)
                Bake(map);

            // 수치는 WorldConfig 클래스의 첫 값 그대로 — 맵만 잇는다
            LoadOrCreate<WorldConfig>($"{Folder}/WorldConfig.asset", ref made, so => so.FindProperty("_map").objectReferenceValue = map);

            AssetDatabase.SaveAssets();
            Debug.Log($"[World] 월드 에셋 {made}개를 만들었다({Folder}) — 이미 있던 것은 그대로 뒀다");
        }

        // 땅 번호 지도(그림 한 장)를 다시 그려 저장하고, 그 그림에서 뽑은 이웃 · 표시 자리를 맵 에셋에 적는다 — 지형은 건드리지 않는다.
        // 지도의 모양은 WorldMapBaker의 숫자가 정한다(늘 같은 그림이 나온다)
        [MenuItem("GnorpWar/월드 지도 굽기")]
        private static void BakeMain()
        {
            var map = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>($"{Folder}/WorldMap_Main.asset");
            if (map == null)
            {
                Debug.LogError("[World] WorldMap_Main.asset이 없다 — 「월드 에셋 만들기」를 먼저 부른다");
                return;
            }
            Bake(map);
            AssetDatabase.SaveAssets();
        }

        private static void Bake(WorldMapDefinition map)
        {
            var baker = new WorldMapBaker();
            const int width = WorldMapBaker.Width, height = WorldMapBaker.Height;
            var pixels = new Color32[width * height];
            // 텍스처는 아래 줄부터 든다
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    byte value = (byte)((baker.At(x, y) + 1) * WorldMapDefinition.RegionMapStep);
                    pixels[(height - 1 - y) * width + x] = new Color32(value, value, value, 255);
                }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            string path = $"{Folder}/{map.name}_Regions.png";
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            // 값을 그대로 읽어야 한다 — 색 공간 변환 · 압축 · 크기 보정 · 밉맵 없이, 읽기 켬
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var so = new SerializedObject(map);
            so.FindProperty("_regionMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            SerializedProperty regions = so.FindProperty("_regions");
            for (int i = 0; i < regions.arraySize; i++)
            {
                SerializedProperty region = regions.GetArrayElementAtIndex(i);
                SerializedProperty to = region.FindPropertyRelative(nameof(WorldMapDefinition.Region.Links));
                to.arraySize = baker.Links[i].Length;
                for (int k = 0; k < baker.Links[i].Length; k++)
                    to.GetArrayElementAtIndex(k).intValue = baker.Links[i][k];
                region.FindPropertyRelative(nameof(WorldMapDefinition.Region.Anchor)).vector2IntValue = new Vector2Int(baker.AnchorX[i], baker.AnchorY[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            int links = 0;
            foreach (int[] row in baker.Links)
                links += row.Length;
            Debug.Log($"[World] 지도를 구웠다 — {path} ({width}×{height}) · 땅 {regions.arraySize}개 · 이웃 {links / 2}쌍(국경 {WorldMapBaker.MinBorder}칸 이상)");
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

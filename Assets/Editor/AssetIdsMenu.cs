using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 에셋 ID 등록부(Run/AssetIds) 채우기 — 메뉴 「GnorpWar → 에셋 ID 등록부 갱신」 · 빌드 직전에 자동.
    // 스냅샷에 적힐 수 있는 종류(기물 · 업그레이드 · 기물 능력)를 프로젝트에서 전부 찾아 GUID와 함께 적는다. 등록부가 없으면 만든다
    internal class AssetIdsMenu : IPreprocessBuildWithReport
    {
        public const string Path = "Assets/Data/AssetIds.asset";
        public static readonly System.Type[] Kinds = { typeof(BuildingDefinition), typeof(UpgradeDefinition), typeof(BuildingPassive) };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Refresh();

        [MenuItem("GnorpWar/에셋 ID 등록부 갱신")]
        public static AssetIds Refresh()
        {
            AssetIds registry = AssetDatabase.LoadAssetAtPath<AssetIds>(Path);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<AssetIds>();
                AssetDatabase.CreateAsset(registry, Path);
            }
            registry.Set(Collect());
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
            Debug.Log($"[AssetIds] 등록부 갱신 — {registry.Entries.Count}개");
            return registry;
        }

        // GUID 순 — 갱신할 때마다 줄 순서가 흔들리지 않게
        public static List<AssetIds.Entry> Collect()
        {
            var entries = new List<AssetIds.Entry>();
            var seen = new HashSet<string>();
            foreach (System.Type kind in Kinds)
                foreach (string guid in AssetDatabase.FindAssets($"t:{kind.Name}", new[] { "Assets" }))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null && kind.IsInstanceOfType(asset) && seen.Add(guid))
                        entries.Add(new AssetIds.Entry { Id = guid, Asset = asset });
                }
            entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return entries;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 바뀌지 않는 에셋 ID 등록부 — 탑 스냅샷(TowerSnapshot)이 기물 · 업그레이드 · 기물 능력 에셋을 ID로 적고 다시 찾는다.
    // ID = 에셋의 GUID(.meta). 이름을 바꾸거나 옮겨도 그대로다(.meta를 잃으면 바뀐다). 런타임에는 GUID를 못 읽으므로 에디터가 여기 적어 둔다 —
    // 메뉴 「GnorpWar → 에셋 ID 등록부 갱신」 · 빌드 직전에 자동(Editor/AssetIdsMenu). 등록부에 없는 에셋을 적거나 읽으면 예외로 알린다
    [CreateAssetMenu(menuName = "GnorpWar/Asset Ids")]
    public class AssetIds : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Id;
            public ScriptableObject Asset;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        [System.NonSerialized] private Dictionary<string, ScriptableObject> _byId;
        [System.NonSerialized] private Dictionary<ScriptableObject, string> _byAsset;

        public IReadOnlyList<Entry> Entries => _entries;

        public string IdOf(ScriptableObject asset)
        {
            Index();
            if (asset != null && _byAsset.TryGetValue(asset, out string id))
                return id;
            throw new TowerSnapshot.InvalidException($"등록부에 없는 에셋 「{(asset != null ? asset.name : "null")}」 — 메뉴 「GnorpWar → 에셋 ID 등록부 갱신」");
        }

        public T Find<T>(string id) where T : ScriptableObject
        {
            Index();
            if (id != null && _byId.TryGetValue(id, out ScriptableObject asset) && asset is T found)
                return found;
            throw new TowerSnapshot.InvalidException($"모르는 {typeof(T).Name} ID 「{id}」");
        }

        private void Index()
        {
            if (_byId != null)
                return;
            _byId = new Dictionary<string, ScriptableObject>();
            _byAsset = new Dictionary<ScriptableObject, string>();
            foreach (Entry entry in _entries)
                if (entry.Asset != null)
                {
                    _byId[entry.Id] = entry.Asset;
                    _byAsset[entry.Asset] = entry.Id;
                }
        }

#if UNITY_EDITOR
        public void Set(List<Entry> entries)
        {
            _entries = entries;
            _byId = null;
            _byAsset = null;
        }
#endif
    }
}

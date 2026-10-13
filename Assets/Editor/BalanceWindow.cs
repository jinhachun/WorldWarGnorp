using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 밸런싱 창(메뉴 GnorpWar/밸런싱) — 왼쪽 메뉴 트리에서 에셋을 고르면 오른쪽에 그 인스펙터가 나온다(Odin Inspector). 고친 값은 바로 .asset 파일에 쓴다.
    // 묶음은 여섯(유닛 · 기물 · 전투·경제 설정 · 맵 · 적 빌드 · 월드) — 유형으로 찾으므로(FindAssets) 에셋이 늘면 목록도 따라온다. 「유닛」 · 「기물」 묶음 자체를 누르면 표.
    // 기물 하나를 누르면 그 기물과 딸린 액션 · 유닛 효과 · 패시브 에셋이 한 화면에 이어서 나온다(BuildingPage).
    // 에셋(ScriptableObject)을 고치는 것이라 플레이 중에 고쳐도 플레이를 끈 뒤 값이 남는다
    internal class BalanceWindow : OdinMenuEditorWindow
    {
        private const string UnitGroup = "유닛";
        private const string BuildingGroup = "기물";
        private const string HideNonBalanceKey = "GnorpWar.Balance.HideNonBalance";

        private static readonly (string Title, string[] Types)[] Groups =
        {
            (UnitGroup, new[] { nameof(UnitDefinition) }),
            ("전투·경제 설정", new[] { nameof(BattleConfig), nameof(BaseDefinition) }),
            ("맵", new[] { nameof(MapDefinition) }),
            ("적 빌드", new[] { nameof(EnemyBuildProfile) }),
            ("월드", new[] { nameof(WorldConfig) }),
            ("업그레이드", new[] { nameof(UpgradeDefinition) }),
        };

        // 유닛 표 — 유닛을 줄로, 자주 고치는 수치를 칸으로. 칸에서 고치면 그 유닛 에셋에 바로 쓴다
        private class UnitTable
        {
            [TableList(IsReadOnly = true, AlwaysExpanded = true, ShowPaging = false, HideToolbar = true)]
            [HideLabel, ShowInInspector]
            public List<UnitRow> Rows = new List<UnitRow>();
        }

        // 표의 한 줄 — 런타임 클래스에 setter를 뚫지 않고 SerializedObject로 읽고 쓴다(되돌리기 · dirty는 Unity가 한다)
        private class UnitRow
        {
            internal readonly UnitDefinition Unit;
            private readonly SerializedObject _serialized;

            public UnitRow(UnitDefinition unit)
            {
                Unit = unit;
                _serialized = new SerializedObject(unit);
            }

            [ShowInInspector, DisplayAsString, VerticalGroup("이름"), HideLabel, TableColumnWidth(90)]
            public string Name => Unit.DisplayName;
            [ShowInInspector, DisplayAsString, VerticalGroup("병종"), HideLabel, TableColumnWidth(70)]
            public string Category => Unit.Category != null ? Unit.Category.DisplayName : "";
            [ShowInInspector, VerticalGroup("체력"), HideLabel]
            public float MaxHp { get => Get("_maxHp"); set => Set("_maxHp", value); }
            [ShowInInspector, VerticalGroup("공격력"), HideLabel]
            public float AttackDamage { get => Get("_attackDamage"); set => Set("_attackDamage", value); }
            [ShowInInspector, VerticalGroup("공격 간격(초)"), HideLabel]
            public float AttackInterval { get => Get("_attackInterval"); set => Set("_attackInterval", value); }
            [ShowInInspector, VerticalGroup("사거리(칸)"), HideLabel]
            public float AttackRange { get => Get("_attackRange"); set => Set("_attackRange", value); }
            [ShowInInspector, VerticalGroup("이동속도"), HideLabel]
            public float MoveSpeed { get => Get("_moveSpeed"); set => Set("_moveSpeed", value); }
            [ShowInInspector, VerticalGroup("미는 힘"), HideLabel]
            public float PushPower { get => Get("_pushPower"); set => Set("_pushPower", value); }
            [ShowInInspector, VerticalGroup("넉백 속도"), HideLabel]
            public float HitKnockback { get => Get("_hitKnockback"); set => Set("_hitKnockback", value); }
            [ShowInInspector, VerticalGroup("층 순위"), HideLabel]
            public int StackRank
            {
                get => Property("_stackRank").intValue;
                set
                {
                    Property("_stackRank").intValue = value;
                    _serialized.ApplyModifiedProperties();
                }
            }

            private SerializedProperty Property(string field)
            {
                _serialized.Update();
                return _serialized.FindProperty(field);
            }

            private float Get(string field) => Property(field).floatValue;

            private void Set(string field, float value)
            {
                Property(field).floatValue = value;
                _serialized.ApplyModifiedProperties();
            }
        }

        // 기물 표 — 기물을 줄로. 가격은 등급이 정하므로(BattleConfig의 등급가 표) 보여 주기만 한다
        private class BuildingTable
        {
            [TableList(IsReadOnly = true, AlwaysExpanded = true, ShowPaging = false, HideToolbar = true)]
            [HideLabel, ShowInInspector]
            public List<BuildingRow> Rows = new List<BuildingRow>();
        }

        private class BuildingRow
        {
            internal readonly BuildingDefinition Building;
            private readonly SerializedObject _serialized;
            private readonly BattleConfig _config;
            private readonly bool _inShop;

            public BuildingRow(BuildingDefinition building, BattleConfig config, bool inShop)
            {
                Building = building;
                _serialized = new SerializedObject(building);
                _config = config;
                _inShop = inShop;
            }

            [ShowInInspector, DisplayAsString, VerticalGroup("이름"), HideLabel, TableColumnWidth(110)]
            public string Name => Building.DisplayName;
            [ShowInInspector, DisplayAsString, VerticalGroup("국가"), HideLabel, TableColumnWidth(50)]
            public string Nation => Building.Nation.DisplayName();
            [ShowInInspector, DisplayAsString, VerticalGroup("세력"), HideLabel, TableColumnWidth(70)]
            public string Faction => Building.Faction.DisplayName();
            [ShowInInspector, DisplayAsString, VerticalGroup("상점"), HideLabel, TableColumnWidth(50)]
            public string Shop => _inShop ? "나옴" : "안 나옴";
            [ShowInInspector, VerticalGroup("등급"), HideLabel, ValueDropdown(nameof(Rarities)), TableColumnWidth(70)]
            public BuildingRarity Rarity
            {
                get => (BuildingRarity)Property("_rarity").enumValueIndex;
                set
                {
                    Property("_rarity").enumValueIndex = (int)value;
                    _serialized.ApplyModifiedProperties();
                }
            }
            [ShowInInspector, DisplayAsString, VerticalGroup("가격(골드)"), HideLabel, TableColumnWidth(60)]
            public int Price => _config != null ? _config.PriceOf(Rarity) : 0;
            [ShowInInspector, VerticalGroup("쿨다운(초)"), HideLabel]
            public float Cooldown
            {
                get => Property("_cooldown").floatValue;
                set
                {
                    Property("_cooldown").floatValue = value;
                    _serialized.ApplyModifiedProperties();
                }
            }
            [ShowInInspector, DisplayAsString, VerticalGroup("소환 유닛"), HideLabel, TableColumnWidth(90)]
            public string Unit => Building.Unit != null ? Building.Unit.DisplayName : "";
            [ShowInInspector, VerticalGroup("소환 수(기)"), HideLabel, EnableIf(nameof(Produces))]
            public int UnitCount
            {
                get => Property("_unitCount").intValue;
                set
                {
                    Property("_unitCount").intValue = value;
                    _serialized.ApplyModifiedProperties();
                }
            }
            [ShowInInspector, DisplayAsString, VerticalGroup("딸린 에셋"), HideLabel, TableColumnWidth(120)]
            public string Attached => $"액션 {Building.Actions.Length} · 효과 {Building.Effects.Length} · 패시브 {Building.Passives.Length}";

            private bool Produces => Building.Unit != null;

            private static ValueDropdownList<BuildingRarity> Rarities()
            {
                var list = new ValueDropdownList<BuildingRarity>();
                foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
                {
                    // 신화는 전설 둘을 합쳐야만 나오는 등급이라 기물 정의의 등급으로 쓰지 않는다
                    if (rarity == BuildingRarity.Mythic) continue;
                    list.Add(rarity.DisplayName(), rarity);
                }
                return list;
            }

            private SerializedProperty Property(string field)
            {
                _serialized.Update();
                return _serialized.FindProperty(field);
            }
        }

        // 기물 하나의 화면 — 기물 에셋과, 그 기물이 가리키는 액션 · 유닛 효과 · 패시브 에셋(그 에셋이 다시 가리키는 것까지)을 위에서 아래로 이어 그린다.
        // 에셋마다 제 인스펙터(Odin)이고, 고친 값은 그 에셋에 쓴다. 여러 기물이 같이 쓰는 에셋은 제목 아래에 적는다 — 하나를 고치면 같이 바뀐다
        private class BuildingPage
        {
            internal readonly List<Part> Parts = new List<Part>();

            internal class Part
            {
                public ScriptableObject Asset;
                public string Title, Subtitle;
                public PropertyTree Tree;
            }

            [OnInspectorGUI]
            private void Draw()
            {
                foreach (Part part in Parts)
                {
                    SirenixEditorGUI.Title(part.Title, part.Subtitle, TextAlignment.Left, true);
                    if (part.Tree == null)
                    {
                        part.Tree = PropertyTree.Create(part.Asset);
                        // 접힌 묶음(스탯 보정 값 등) 안의 수치가 한 번에 보이게 처음부터 펼친다
                        part.Tree.UpdateTree();
                        foreach (InspectorProperty property in part.Tree.EnumerateTree(true))
                            property.State.Expanded = true;
                    }
                    part.Tree.Draw(false);
                    GUILayout.Space(14f);
                }
            }

            internal void Dispose()
            {
                foreach (Part part in Parts)
                    part.Tree?.Dispose();
            }
        }

        private readonly List<BuildingPage> _pages = new List<BuildingPage>();

        private void DisposePages()
        {
            foreach (BuildingPage page in _pages)
                page.Dispose();
            _pages.Clear();
        }

        private static bool Attached(Object asset) => asset is BuildingAction || asset is UnitEffect || asset is BuildingPassive;

        private static string KindOf(ScriptableObject asset)
            => asset is BuildingDefinition ? "기물" : asset is BuildingAction ? "액션" : asset is UnitEffect ? "유닛 효과" : "패시브";

        // asset이 가리키는 액션 · 유닛 효과 · 패시브 에셋을 필드 순서대로(그 에셋이 다시 가리키는 것까지) found에 모은다
        private static void CollectAttached(ScriptableObject asset, List<ScriptableObject> found)
        {
            SerializedProperty property = new SerializedObject(asset).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || !Attached(property.objectReferenceValue))
                    continue;
                var child = (ScriptableObject)property.objectReferenceValue;
                if (found.Contains(child))
                    continue;
                found.Add(child);
                CollectAttached(child, found);
            }
        }

        // 「기물」 묶음 — 표 하나와, 국가별 하위 묶음 아래 기물마다 화면 하나(세력 · 등급 · 이름 순). 상점에 안 나오는 기물은 이름 뒤에 적는다
        private void AddBuildings(OdinMenuTree tree)
        {
            DisposePages();
            // 컴파일로 창의 상태가 날아가기 전에도 치운다(안 치우면 Odin이 경고한다)
            AssemblyReloadEvents.beforeAssemblyReload -= DisposePages;
            AssemblyReloadEvents.beforeAssemblyReload += DisposePages;

            BattleConfig config = null;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BattleConfig), new[] { "Assets" }))
                config ??= AssetDatabase.LoadAssetAtPath<BattleConfig>(AssetDatabase.GUIDToAssetPath(guid));
            var buildings = new List<BuildingDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BuildingDefinition), new[] { "Assets" }))
            {
                var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (building != null)
                    buildings.Add(building);
            }
            buildings.Sort((a, b) =>
            {
                int order = a.Nation.CompareTo(b.Nation);
                if (order == 0) order = a.Faction.CompareTo(b.Faction);
                if (order == 0) order = a.Rarity.CompareTo(b.Rarity);
                return order != 0 ? order : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });

            // 딸린 에셋마다 그것을 쓰는 기물 수
            var attached = new Dictionary<BuildingDefinition, List<ScriptableObject>>();
            var users = new Dictionary<ScriptableObject, int>();
            foreach (BuildingDefinition building in buildings)
            {
                var found = new List<ScriptableObject>();
                CollectAttached(building, found);
                attached[building] = found;
                foreach (ScriptableObject asset in found)
                    users[asset] = users.TryGetValue(asset, out int count) ? count + 1 : 1;
            }

            var table = new BuildingTable();
            tree.Add(BuildingGroup, table);
            foreach (BuildingDefinition building in buildings)
            {
                bool inShop = config != null && System.Array.IndexOf(config.ShopPool, building) >= 0;
                table.Rows.Add(new BuildingRow(building, config, inShop));

                var page = new BuildingPage();
                page.Parts.Add(new BuildingPage.Part { Asset = building, Title = $"기물 — {NameOf(building)}", Subtitle = building.name + (inShop ? "" : " · 상점에 안 나옴") });
                foreach (ScriptableObject asset in attached[building])
                    page.Parts.Add(new BuildingPage.Part
                    {
                        Asset = asset,
                        Title = $"{KindOf(asset)} — {asset.name}",
                        Subtitle = asset.GetType().Name + (users[asset] > 1 ? $" · 다른 기물 {users[asset] - 1}개도 사용 — 고치면 같이 바뀐다" : ""),
                    });
                _pages.Add(page);
                string path = $"{BuildingGroup}/{building.Nation.DisplayName()}/{NameOf(building)}{(inShop ? "" : " (상점 밖)")}";
                if (building.Icon != null)
                    tree.Add(path, page, Enlarged(building.Icon.texture));
                else
                    tree.Add(path, page);
            }
        }

        // 왼쪽 트리의 기물 이름 글자색 = 그 기물의 등급 색(게임의 기물 테두리 색 — BuildingRarity.FrameColor). 등급마다 메뉴 스타일 하나
        private OdinMenuStyle[] _rarityStyles;

        // 그릴 때마다 지금 등급으로 맞춘다 — 표나 상세에서 등급을 바꾸면 트리 색이 바로 따라온다
        protected override void DrawMenu()
        {
            if (MenuTree != null)
            {
                if (_rarityStyles == null)
                {
                    _rarityStyles = new OdinMenuStyle[System.Enum.GetValues(typeof(BuildingRarity)).Length];
                    for (int i = 0; i < _rarityStyles.Length; i++)
                    {
                        Color color = ((BuildingRarity)i).FrameColor();
                        OdinMenuStyle style = MenuTree.DefaultMenuStyle.Clone();
                        style.DefaultLabelStyle = new GUIStyle(style.DefaultLabelStyle) { normal = { textColor = color } };
                        style.SelectedLabelStyle = new GUIStyle(style.SelectedLabelStyle) { normal = { textColor = color } };
                        _rarityStyles[i] = style;
                    }
                }
                foreach (OdinMenuItem item in MenuTree.EnumerateTree())
                    if (item.Value is BuildingPage page)
                        item.Style = _rarityStyles[(int)((BuildingDefinition)page.Parts[0].Asset).Rarity];
            }
            base.DrawMenu();
        }

        [MenuItem("GnorpWar/밸런싱")]
        private static void Open() => GetWindow<BalanceWindow>("밸런싱");

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(false);
            tree.Config.DrawSearchToolbar = true;
            // 아이콘이 8×8 픽셀 그림이라 기본 크기로는 안 보인다
            tree.DefaultMenuStyle.Height = 28;
            tree.DefaultMenuStyle.IconSize = IconSize;
            // 기물 이름 뒤의 「(상점 밖)」까지 보이는 폭
            MenuWidth = 240f;

            var table = new UnitTable();
            tree.Add(UnitGroup, table);
            AddBuildings(tree);
            foreach ((string title, string[] types) in Groups)
            {
                foreach (string type in types)
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                        if (asset == null)
                            continue;
                        if (asset is UnitDefinition unit)
                            table.Rows.Add(new UnitRow(unit));
                        string path = $"{title}/{NameOf(asset)}";
                        Sprite icon = IconOf(asset);
                        if (icon != null)
                            tree.Add(path, asset, Enlarged(icon.texture));
                        else
                            tree.Add(path, asset);
                    }
                }
            }
            return tree;
        }

        private const int IconSize = 24;
        private static readonly Dictionary<Texture2D, Texture2D> EnlargedIcons = new Dictionary<Texture2D, Texture2D>();

        // 메뉴 아이콘용 — 작은 픽셀 그림(8×8 …)을 아이콘 칸에 들어가는 가장 큰 정수배로 또렷하게 키운 사본. 트리는 텍스처를 원래 크기로만 그리고,
        // Sprite로 넘기면 미리보기가 준비되기 전(컴파일 직후)에 아이콘이 빈 채로 굳는다. 그림마다 한 번만 만든다
        private static Texture2D Enlarged(Texture2D source)
        {
            if (EnlargedIcons.TryGetValue(source, out Texture2D made) && made != null)
                return made;
            int scale = Mathf.Max(1, IconSize / Mathf.Max(source.width, source.height));
            RenderTexture copy = RenderTexture.GetTemporary(source.width * scale, source.height * scale, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, copy);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = copy;
            made = new Texture2D(copy.width, copy.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            made.ReadPixels(new Rect(0f, 0f, copy.width, copy.height), 0, 0);
            made.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            EnlargedIcons[source] = made;
            return made;
        }

        // 표시 이름(한국어)이 있으면 그것, 없으면 파일 이름
        private static string NameOf(ScriptableObject asset)
        {
            SerializedProperty display = new SerializedObject(asset).FindProperty("_displayName");
            return display != null && display.stringValue != "" ? display.stringValue : asset.name;
        }

        // 유닛 = 프리팹의 몸 그림(가장 뒤에 그리는 SpriteRenderer — 기사는 탄 gnorp — 적 전용 유닛은 적 프리팹에서) · 맵 = 뒷배경의 첫 그림 · 적 빌드 = 값이 가장 큰 기물의 아이콘
        private static Sprite IconOf(ScriptableObject asset)
        {
            switch (asset)
            {
                case UnitDefinition unit:
                    Unit prefab = unit.PrefabFor(Team.Ally) != null ? unit.PrefabFor(Team.Ally) : unit.PrefabFor(Team.Enemy);
                    SpriteRenderer body = null;
                    if (prefab != null)
                        foreach (SpriteRenderer part in prefab.GetComponentsInChildren<SpriteRenderer>())
                            if (part.sprite != null && (body == null || part.sortingOrder < body.sortingOrder))
                                body = part;
                    return body != null ? body.sprite : null;
                case MapDefinition map:
                    foreach (MapDefinition.BackdropLayer layer in map.Backdrop)
                        foreach (Sprite sprite in layer.Sprites)
                            if (sprite != null)
                                return sprite;
                    return null;
                case EnemyBuildProfile build:
                    SerializedProperty picks = new SerializedObject(build).FindProperty("_picks");
                    BuildingDefinition best = null;
                    float bestWeight = float.MinValue;
                    for (int i = 0; i < picks.arraySize; i++)
                    {
                        SerializedProperty pick = picks.GetArrayElementAtIndex(i);
                        var building = pick.FindPropertyRelative(nameof(EnemyBuildProfile.Pick.Building)).objectReferenceValue as BuildingDefinition;
                        float weight = pick.FindPropertyRelative(nameof(EnemyBuildProfile.Pick.Weight)).floatValue;
                        if (building != null && weight > bestWeight)
                        {
                            best = building;
                            bestWeight = weight;
                        }
                    }
                    return best != null ? best.Icon : null;
                default:
                    return null;
            }
        }

        // 설정 란 — 오른쪽 화면 맨 위에 늘 있다. 값은 EditorPrefs에(창을 닫아도 남는다).
        // 숨김은 이 창이 그리는 동안만 건다(BalanceView) — 일반 인스펙터에서는 그림·프리팹을 배선할 수 있게 늘 전부 보인다
        protected override void OnBeginDrawEditors()
        {
            bool hide = EditorPrefs.GetBool(HideNonBalanceKey, false);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("설정", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(false));
            bool picked = GUILayout.Toggle(hide, "밸런스와 무관한 값 숨기기", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (picked != hide)
                EditorPrefs.SetBool(HideNonBalanceKey, picked);
            BalanceView.HideNonBalance = picked;
            base.OnBeginDrawEditors();
        }

        // 고친 값(되돌리기 포함)을 바로 파일에 쓴다 — 저장을 따로 누르지 않아도 .asset이 바뀐다
        protected override void OnEndDrawEditors()
        {
            base.OnEndDrawEditors();
            BalanceView.HideNonBalance = false;
            if (MenuTree == null)
                return;
            foreach (object value in MenuTree.Selection.SelectedValues)
            {
                if (value is Object asset)
                    Save(asset);
                else if (value is BuildingTable buildings)
                    foreach (BuildingRow row in buildings.Rows)
                        Save(row.Building);
                else if (value is BuildingPage page)
                    foreach (BuildingPage.Part part in page.Parts)
                        Save(part.Asset);
                else if (value is UnitTable table)
                    foreach (UnitRow row in table.Rows)
                        Save(row.Unit);
            }
        }

        // 그리던 도중에 끊겨(ExitGUI 등) 숨김이 켜진 채 창이 닫혀도 일반 인스펙터에 남지 않게
        protected override void OnDestroy()
        {
            base.OnDestroy();
            BalanceView.HideNonBalance = false;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposePages;
            DisposePages();
        }

        private static void Save(Object asset)
        {
            if (asset != null && EditorUtility.IsDirty(asset))
                AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}

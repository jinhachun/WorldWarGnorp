using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 밸런싱 창(메뉴 GnorpWar/밸런싱) — 왼쪽 메뉴 트리에서 에셋을 고르면 오른쪽에 그 인스펙터가 나온다(Odin Inspector). 고친 값은 바로 .asset 파일에 쓴다.
    // 묶음은 넷(유닛 · 전투·경제 설정 · 맵 · 적 빌드) — 유형으로 찾으므로(FindAssets) 에셋이 늘면 목록도 따라온다. 「유닛」 묶음 자체를 누르면 유닛 표.
    // 에셋(ScriptableObject)을 고치는 것이라 플레이 중에 고쳐도 플레이를 끈 뒤 값이 남는다
    internal class BalanceWindow : OdinMenuEditorWindow
    {
        private const string UnitGroup = "유닛";
        private const string HideNonBalanceKey = "GnorpWar.Balance.HideNonBalance";

        private static readonly (string Title, string[] Types)[] Groups =
        {
            (UnitGroup, new[] { nameof(UnitDefinition) }),
            ("전투·경제 설정", new[] { nameof(BattleConfig), nameof(BaseDefinition) }),
            ("맵", new[] { nameof(MapDefinition) }),
            ("적 빌드", new[] { nameof(EnemyBuildProfile) }),
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

        [MenuItem("GnorpWar/밸런싱")]
        private static void Open() => GetWindow<BalanceWindow>("밸런싱");

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(false);
            tree.Config.DrawSearchToolbar = true;
            // 아이콘이 8×8 픽셀 그림이라 기본 크기로는 안 보인다
            tree.DefaultMenuStyle.Height = 28;
            tree.DefaultMenuStyle.IconSize = IconSize;

            var table = new UnitTable();
            tree.Add(UnitGroup, table);
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
        }

        private static void Save(Object asset)
        {
            if (asset != null && EditorUtility.IsDirty(asset))
                AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}

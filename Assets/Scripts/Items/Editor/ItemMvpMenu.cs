using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShinySTG.Items.Editor
{
    public static class ItemMvpMenu
    {
        [MenuItem("Tools/STG/Items/Create MVP Definitions")]
        static void CreateDefinitions()
        {
            // 每次创建独立目录，不覆盖用户已经调过的配置。
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/ItemMvp");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var kinds = new[] { ItemKind.SmallPower, ItemKind.LargePower, ItemKind.Score, ItemKind.Bomb, ItemKind.OneUp };
            var colors = new[] { Color.red, new Color(1f, 0.35f, 0.35f), Color.blue, Color.green, Color.magenta };
            var entries = new DropProfile.Entry[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.Kind = kinds[i];
                item.Tint = colors[i];
                item.Size = kinds[i] == ItemKind.LargePower ? 0.45f : 0.3f;
                AssetDatabase.CreateAsset(item, folder + "/" + kinds[i] + ".asset");
                EditorUtility.SetDirty(item);
                entries[i] = new DropProfile.Entry { Item = item, Count = i == 0 ? 10 : 1 };
            }
            var profile = ScriptableObject.CreateInstance<DropProfile>();
            profile.Entries = entries;
            AssetDatabase.CreateAsset(profile, folder + "/SampleDrops.asset");
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log("[Items] 已创建五类道具及示例掉落：" + folder + "。资产创建不支持场景 Undo；如不需要可删除此独立目录。");
        }

        [MenuItem("Tools/STG/Items/Add Scene Service")]
        static void AddService()
        {
            var existing = Object.FindObjectOfType<ItemDropService>();
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            var go = new GameObject("Item Drop Service");
            Undo.RegisterCreatedObjectUndo(go, "Add Item Drop Service");
            Undo.AddComponent<ItemDropService>(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/STG/Items/Spawn Selected Profile (Play Mode)")]
        static void SpawnSelected()
        {
            var player = ShinySTG.Player.Player.Instance;
            Vector2 position = player != null ? (Vector2)player.transform.position + Vector2.up * 3f : Vector2.zero;
            ItemDropService.Spawn(Selection.activeObject as DropProfile, position);
        }

        [MenuItem("Tools/STG/Items/Spawn Selected Profile (Play Mode)", true)]
        static bool CanSpawnSelected() => Application.isPlaying && Selection.activeObject is DropProfile;

        [MenuItem("Tools/STG/Items/Create MVP Definitions", true)]
        [MenuItem("Tools/STG/Items/Add Scene Service", true)]
        static bool CanEdit() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

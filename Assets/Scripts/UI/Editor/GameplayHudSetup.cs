using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ShinySTG.UI.Editor
{
    internal static class GameplayHudSetup
    {
        const string PrefabPath = "Assets/Prefabs/UI/GameplayHud.prefab";

        [MenuItem("STG/UI/Create Gameplay HUD")]
        static void Create()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null
                || !scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<GameplayHudPresenter>(true);
                if (existing != null)
                {
                    Selection.activeGameObject = existing.gameObject;
                    Debug.LogWarning("[HUD] 场景已有 HUD，跳过重复创建。", existing);
                    return;
                }
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[HUD] 找不到 " + PrefabPath);
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Gameplay HUD");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create Gameplay HUD");
                CreateBossHud(instance);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = instance;
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        static void CreateBossHud(GameObject root)
        {
            var existing = root.GetComponentInChildren<BossHudPresenter>(true);
            if (existing != null) return;
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null) throw new System.InvalidOperationException("Gameplay HUD prefab 缺少 Canvas。");

            var bossRoot = new GameObject("BossHud", typeof(RectTransform), typeof(CanvasGroup),
                typeof(BossHudView), typeof(BossHudPresenter));
            Undo.RegisterCreatedObjectUndo(bossRoot, "Create Boss HUD");
            bossRoot.transform.SetParent(canvas.transform, false);
            var bossRect = (RectTransform)bossRoot.transform;
            bossRect.anchorMin = new Vector2(.08f, 1f);
            bossRect.anchorMax = new Vector2(.92f, 1f);
            bossRect.pivot = new Vector2(.5f, 1f);
            bossRect.anchoredPosition = new Vector2(0f, -42f);
            bossRect.sizeDelta = new Vector2(0f, 46f);

            var background = CreateImage("Background", bossRoot.transform, new Color(.035f, .045f, .075f, .9f), false);
            var fill = CreateImage("Fill", background.transform, new Color(.95f, .22f, .45f, 1f), true);
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            var count = new GameObject("BarCount", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(count, "Create Boss HUD Count");
            count.transform.SetParent(bossRoot.transform, false);
            var countRect = (RectTransform)count.transform;
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.pivot = new Vector2(0f, .5f);
            countRect.anchoredPosition = new Vector2(12f, 0f);
            countRect.sizeDelta = new Vector2(110f, 0f);
            var text = count.GetComponent<TextMeshProUGUI>();
            text.text = "剩余 0 管";
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;
            text.font = TMP_Settings.defaultFontAsset;

            var view = bossRoot.GetComponent<BossHudView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_fill").objectReferenceValue = fillImage;
            viewSo.FindProperty("_barCount").objectReferenceValue = text;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            Selection.activeGameObject = bossRoot;
        }

        static GameObject CreateImage(string name, Transform parent, Color color, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create Boss HUD Image");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = stretch ? Vector2.zero : Vector2.zero;
            rect.anchorMax = stretch ? Vector2.one : Vector2.one;
            rect.offsetMin = stretch ? new Vector2(2f, 2f) : Vector2.zero;
            rect.offsetMax = stretch ? new Vector2(-2f, -2f) : Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }
    }
}

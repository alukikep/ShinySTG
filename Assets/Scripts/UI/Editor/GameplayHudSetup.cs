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
            AddTimer(view);
            AddSegments(view);
            Selection.activeGameObject = bossRoot;
        }

        [MenuItem("STG/UI/Add Boss HUD Timer")]
        static void AddSelectedTimer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var selected = Selection.activeGameObject;
            var view = selected != null ? selected.GetComponentInChildren<BossHudView>(true) : null;
            if (view == null && selected != null) view = selected.GetComponentInParent<BossHudView>();
            if (view == null || !view.gameObject.scene.IsValid())
            {
                Debug.LogWarning("[HUD] 请先选中场景中的 BossHud 或其父节点。");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Boss HUD Timer");
            try
            {
                AddTimer(view);
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        internal static void AddTimer(BossHudView view)
        {
            var serialized = new SerializedObject(view);
            if (serialized.FindProperty("_timer").objectReferenceValue != null) return;
            var fill = serialized.FindProperty("_fill").objectReferenceValue as Image;
            var bar = fill != null ? fill.transform.parent as RectTransform : null;
            if (bar == null) throw new System.InvalidOperationException("请先绑定血条填充和背景 RectTransform。");
            // Anchor to the actual bar, so existing manually positioned HUD roots also work.
            var go = new GameObject("Timer", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "Add Boss HUD Timer");
            go.transform.SetParent(bar, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(12f, 0f);
            rect.sizeDelta = new Vector2(48f, 0f);
            var text = go.GetComponent<TextMeshProUGUI>();
            var count = serialized.FindProperty("_barCount").objectReferenceValue as TMP_Text;
            text.font = count != null ? count.font : TMP_Settings.defaultFontAsset;
            text.fontSize = count != null ? count.fontSize : 18f;
            text.color = count != null ? count.color : Color.white;
            text.alignment = TextAlignmentOptions.Midline;
            text.raycastTarget = false;
            text.text = "99";
            // Keep both labels on the right without overlapping. This is an explicit editor operation.
            if (count != null)
            {
                var countRect = count.rectTransform;
                Undo.SetTransformParent(countRect, bar, "Position Boss HUD Count");
                Undo.RecordObject(countRect, "Position Boss HUD Count");
                countRect.anchorMin = new Vector2(1f, 0f);
                countRect.anchorMax = Vector2.one;
                countRect.pivot = new Vector2(0f, .5f);
                countRect.anchoredPosition = new Vector2(72f, 0f);
                countRect.sizeDelta = new Vector2(Mathf.Max(40f, countRect.sizeDelta.x), 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(countRect);
            }
            serialized.FindProperty("_timer").objectReferenceValue = text;
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            EditorUtility.SetDirty(view);
        }

        [MenuItem("STG/UI/Add Boss HUD Segments")]
        static void AddSelectedSegments()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var selected = Selection.activeGameObject;
            var view = selected != null ? selected.GetComponentInChildren<BossHudView>(true) : null;
            if (view == null && selected != null) view = selected.GetComponentInParent<BossHudView>();
            if (view == null || !view.gameObject.scene.IsValid()) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Boss HUD Segments");
            try
            {
                AddSegments(view);
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        internal static void AddSegments(BossHudView view)
        {
            var serialized = new SerializedObject(view);
            if (serialized.FindProperty("_segmentRoot").objectReferenceValue != null) return;
            var fill = serialized.FindProperty("_fill").objectReferenceValue as Image;
            if (fill == null) throw new System.InvalidOperationException("请先绑定原血条填充 Image。");
            var root = new GameObject("Segments", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Add Boss HUD Segments");
            root.transform.SetParent(fill.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            serialized.FindProperty("_segmentRoot").objectReferenceValue = rect;
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            EditorUtility.SetDirty(view);
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

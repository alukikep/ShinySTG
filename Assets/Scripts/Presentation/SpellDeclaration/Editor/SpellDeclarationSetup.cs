using ShinySTG.GameActions;
using ShinySTG.Level.Encounter;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShinySTG.Presentation.SpellDeclaration.Editor
{
    public static class SpellDeclarationSetup
    {
        const string PrefabPath = "Assets/Prefabs/UI/SpellDeclaration.prefab";
        const string SamplePath = "Assets/SO/Presentation/SpellDeclarationSample.asset";

        [MenuItem("STG/UI/Create Spell Declaration Sample Assets %#&d")]
        static void CreateSampleAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var root = BuildView();
                try
                {
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    if (prefab == null) throw new System.InvalidOperationException("无法保存符卡宣言 prefab。");
                }
                finally { Object.DestroyImmediate(root); }
            }
            var sample = AssetDatabase.LoadAssetAtPath<SpellDeclarationDefinition>(SamplePath);
            if (sample == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/SO/Presentation"))
                    AssetDatabase.CreateFolder("Assets/SO", "Presentation");
                sample = ScriptableObject.CreateInstance<SpellDeclarationDefinition>();
                sample.DisplayName = "Spell Card - Butterfly Dream";
                AssetDatabase.CreateAsset(sample, SamplePath);
            }
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[Spell Declaration] 示例资产已就绪；现有资产保留不覆盖。", prefab);
        }

        [MenuItem("STG/UI/Create Spell Declaration")]
        static void Create()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null
                || !scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<SpellDeclarationService>(true);
                if (existing == null) continue;
                Selection.activeGameObject = existing.gameObject;
                Debug.LogWarning("[Spell Declaration] 场景已有宣言服务，跳过重复创建。", existing);
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Spell Declaration");
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene) : BuildView();
                Undo.RegisterCreatedObjectUndo(instance, "Create Spell Declaration");
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

        [MenuItem("Assets/STG/Add Sample Spell Declaration to First Phase", true)]
        static bool CanAttach() => !EditorApplication.isPlayingOrWillChangePlaymode
            && Selection.activeObject is BossEncounterDefinition;

        [MenuItem("Assets/STG/Add Sample Spell Declaration to First Phase")]
        static void Attach()
        {
            var encounter = Selection.activeObject as BossEncounterDefinition;
            var sample = AssetDatabase.LoadAssetAtPath<SpellDeclarationDefinition>(SamplePath);
            if (encounter == null || sample == null) return;
            var presentations = encounter.PhasePresentations ?? System.Array.Empty<PhasePresentation>();
            var phase = System.Array.Find(presentations, p => p != null && p.PhaseIndex == 0);
            if (phase?.EnterActions?.Actions != null && phase.EnterActions.Actions.Length > 0)
            {
                Debug.LogWarning("[Spell Declaration] 首阶段已有进入动作，请手动添加宣言，避免覆盖现有演出。", encounter);
                return;
            }
            Undo.RecordObject(encounter, "Add Spell Declaration");
            if (phase == null)
            {
                phase = new PhasePresentation { PhaseIndex = 0 };
                System.Array.Resize(ref presentations, presentations.Length + 1);
                presentations[presentations.Length - 1] = phase;
                encounter.PhasePresentations = presentations;
            }
            phase.EnterActions = new ActionSequence
            {
                Actions = new GameAction[]
                {
                    new InvincibilityScopeAction
                    {
                        ProtectOwner = true, ProtectPlayer = false,
                        Sequence = new ActionSequence { Actions = new GameAction[]
                            { new PlaySpellDeclarationAction { Declaration = sample } } }
                    }
                }
            };
            EditorUtility.SetDirty(encounter);
        }

        public static GameObject BuildView()
        {
            var root = new GameObject("SpellDeclaration", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                root.SetActive(false);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1200f, 900f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var group = root.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = group.blocksRaycasts = false;
                var portrait = Child("Portrait", root.transform, new Vector2(.12f, .15f), new Vector2(.7f, .9f)).gameObject.AddComponent<Image>();
                portrait.raycastTarget = false;
                portrait.preserveAspect = true;
                portrait.enabled = false;
                var banner = Child("Banner", root.transform, new Vector2(.08f, .2f), new Vector2(.92f, .34f));
                var background = banner.gameObject.AddComponent<Image>();
                background.color = new Color(.07f, .035f, .15f, .9f);
                background.raycastTarget = false;
                var title = Child("Title", banner, new Vector2(.04f, .08f), new Vector2(.96f, .92f)).gameObject.AddComponent<TextMeshProUGUI>();
                title.font = TMP_Settings.defaultFontAsset;
                title.fontSize = 36f;
                title.enableAutoSizing = true;
                title.fontSizeMin = 18f;
                title.fontSizeMax = 36f;
                title.alignment = TextAlignmentOptions.Center;
                title.raycastTarget = false;
                title.text = string.Empty;
                var view = root.AddComponent<SpellDeclarationView>();
                SetReference(view, "_root", group);
                SetReference(view, "_title", title);
                SetReference(view, "_banner", banner);
                SetReference(view, "_portrait", portrait);
                SetReference(root.AddComponent<SpellDeclarationService>(), "_view", view);
                root.SetActive(true);
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        static RectTransform Child(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void SetReference(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

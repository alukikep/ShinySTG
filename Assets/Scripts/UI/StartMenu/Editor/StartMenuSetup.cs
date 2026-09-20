using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShinySTG.UI.Editor
{
    public static class StartMenuSetup
    {
        [MenuItem("STG/UI/Create Start Menu")]
        public static void Create()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid()
                || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            foreach (var obj in scene.GetRootGameObjects())
            {
                var existing = obj.GetComponentInChildren<StartMenuController>(true);
                if (existing == null) continue;
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            var root = Build();
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create Start Menu");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
        }

        public static GameObject Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/StartMenu/NotoSansCJKsc-Regular.otf");
            if (font == null) throw new System.InvalidOperationException("开始菜单中文字体尚未导入。");
            var root = new GameObject("StartMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.SetActive(false);
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1200, 900);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var background = Box("Background", root.transform, Vector2.zero, Vector2.zero, new Color(.035f,.045f,.085f));
                background.anchorMin = Vector2.zero;
                background.anchorMax = Vector2.one;
                Box("RightPanel", root.transform, new Vector2(390,0), new Vector2(420,900), new Color(.055f,.065f,.115f));
                Box("GoldRule", root.transform, new Vector2(-90,153), new Vector2(740,2), new Color(.65f,.49f,.25f));
                Label("Subtitle", root.transform, new Vector2(-110,303), new Vector2(700,50), "SHINYSTG  /  BULLET HELL", 20, new Color(.72f,.57f,.33f), font);
                Label("Title", root.transform, new Vector2(-110,224), new Vector2(700,110), "ShinySTG", 76, new Color(.94f,.93f,.91f), font);
                Label("SideMark", root.transform, new Vector2(390,0), new Vector2(320,280), "弹幕幻想", 48, new Color(.22f,.24f,.34f), font, TextAnchor.MiddleCenter);
                var menu = Rect("Menu", root.transform, new Vector2(-180,-50), new Vector2(540,290));
                var labels = new Text[3];
                string[] names = { "开始游戏", "设置", "退出游戏" };
                for (int i = 0; i < labels.Length; i++)
                    labels[i] = Label(((StartMenuOption)i).ToString(), menu, new Vector2(20,90-i*90), new Vector2(380,70), names[i], 36, new Color(.68f,.7f,.79f), font);
                var indicator = Label("Indicator", menu, new Vector2(-207,90), new Vector2(45,70), ">", 36, new Color(1f,.83f,.46f), font, TextAnchor.MiddleCenter);
                var feedback = Label("Feedback", root.transform, new Vector2(0,-300), new Vector2(1020,65), "", 23, new Color(.88f,.76f,.53f), font, TextAnchor.MiddleCenter);
                Label("Controls", root.transform, new Vector2(0,-380), new Vector2(1000,45), "↑ ↓  选择     /     Z、Enter  确定", 21, new Color(.55f,.58f,.68f), font, TextAnchor.MiddleCenter);
                var view = root.AddComponent<StartMenuView>();
                var serialized = new SerializedObject(view);
                var options = serialized.FindProperty("_options");
                options.arraySize = 3;
                for (int i = 0; i < 3; i++) options.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
                serialized.FindProperty("_indicator").objectReferenceValue = indicator.rectTransform;
                serialized.FindProperty("_feedback").objectReferenceValue = feedback;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<StartMenuController>();
                root.AddComponent<StartMenuInput>();
                return BuildFlow(root, font);
            }
            catch { if (root != null) Object.DestroyImmediate(root); throw; }
        }

        static GameObject BuildFlow(GameObject menu, Font font)
        {
            var root = new GameObject("FrontEnd", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.SetActive(false);
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1200, 900);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                menu.transform.SetParent(root.transform, false);
                Stretch((RectTransform)menu.transform);
                // 外层 CanvasScaler 负责统一缩放；保留菜单上的序列化组件。
                menu.GetComponent<CanvasScaler>().enabled = false;
                menu.SetActive(true);
                var page = Rect("CharacterSelectPage", root.transform, Vector2.zero, Vector2.zero);
                Stretch(page);
                var background = Box("Background", page, Vector2.zero, Vector2.zero, new Color(.035f,.045f,.085f));
                Stretch(background);
                Label("Subtitle", page, new Vector2(-110,303), new Vector2(700,50), "SHINYSTG  /  CHARACTER SELECT", 20, new Color(.72f,.57f,.33f), font);
                Label("Title", page, new Vector2(-110,224), new Vector2(700,110), "角色选择", 58, new Color(.94f,.93f,.91f), font);
                Box("GoldRule", page, new Vector2(-90,153), new Vector2(740,2), new Color(.65f,.49f,.25f));
                Box("PortraitPlaceholder", page, new Vector2(-255,-65), new Vector2(300,360), new Color(.08f,.095f,.15f));
                var portraitLabel = Label("PortraitLabel", page, new Vector2(-255,-65), new Vector2(280,130), "立绘占位", 28, new Color(.4f,.43f,.53f), font, TextAnchor.MiddleCenter);
                var list = Label("CharacterList", page, new Vector2(185,-30), new Vector2(460,240), "", 30, new Color(.85f,.8f,.68f), font, TextAnchor.MiddleCenter);
                Label("Controls", page, new Vector2(0,-380), new Vector2(1000,45), "↑ ↓  切换角色    /    Z、Enter  开始    /    X、Esc  返回", 21, new Color(.55f,.58f,.68f), font, TextAnchor.MiddleCenter);
                var description = Label("Description", page, new Vector2(0,-290), new Vector2(1000,100), "", 24, new Color(.85f,.8f,.68f), font, TextAnchor.MiddleCenter);
                var portrait = Box("Portrait", page, new Vector2(-255,-65), new Vector2(300,360), Color.white).GetComponent<Image>();
                portrait.enabled = false;
                var selection = page.gameObject.AddComponent<CharacterSelectView>();
                SetReference(selection, "_list", list);
                SetReference(selection, "_description", description);
                SetReference(selection, "_portraitLabel", portraitLabel);
                SetReference(selection, "_portrait", portrait);
                page.gameObject.SetActive(false);
                var curtain = Box("ScreenWipe", root.transform, Vector2.zero, Vector2.zero, Color.black);
                Stretch(curtain);
                curtain.gameObject.SetActive(false);
                var transition = root.AddComponent<ScreenWipeTransition>();
                SetReference(transition, "_curtain", curtain.GetComponent<Image>());
                var flow = root.AddComponent<FrontEndFlowController>();
                SetReference(flow, "_menu", menu.GetComponent<StartMenuController>());
                SetReference(flow, "_menuPage", menu);
                SetReference(flow, "_characterPage", page.gameObject);
                SetReference(flow, "_transition", transition);
                root.SetActive(true);
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void SetReference(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value,
            int fontSize, Color color, Font font, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }
    }
}

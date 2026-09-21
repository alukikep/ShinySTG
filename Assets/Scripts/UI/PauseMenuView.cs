using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI
{
    /// <summary>运行时创建，不要求已有游戏场景重新配置。</summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        Text _title;
        Text _hint;
        readonly Text[] _options = new Text[3];
        Font _font;

        void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 32);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 960);
            scaler.matchWidthOrHeight = 0.5f;
            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(transform, false);
            var rect = (RectTransform)shade.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(.025f, .035f, .07f, .88f);
            _title = Label("Title", 180, 48);
            for (int i = 0; i < 3; i++) _options[i] = Label("Option" + i, 65 - i * 80, 32);
            _hint = Label("Controls", -245, 20);
            gameObject.SetActive(false);
        }

        Text Label(string name, float y, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(1000, 75);
            rect.anchoredPosition = new Vector2(0, y);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        public void Show(bool gameOver, int selection)
        {
            gameObject.SetActive(true);
            _title.text = gameOver ? "满身疮痍" : "游戏暂停";
            _options[0].text = gameOver ? "获得 2 个残机复活" : "继续游戏";
            _options[1].text = "回到主菜单";
            _options[2].text = "从头开始";
            for (int i = 0; i < 3; i++)
            {
                _options[i].color = i == selection ? new Color(1f, .83f, .46f) : new Color(.68f, .7f, .79f);
                if (i == selection) _options[i].text = ">  " + _options[i].text;
            }
            _hint.text = "↑ / ↓  选择     Z / Enter  确认" + (gameOver ? "" : "     Esc / X  继续");
        }

        public void Hide() => gameObject.SetActive(false);
        void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}

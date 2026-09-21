using ShinySTG.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI
{
    /// <summary>逐关成绩展示；只读取结算快照，确认与换关由 GameFlow 管理。</summary>
    public sealed class StageResultView : MonoBehaviour
    {
        Text _stage;
        Text _score;
        Text _total;
        Text _hint;
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
            scaler.matchWidthOrHeight = .5f;
            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(transform, false);
            var rect = (RectTransform)shade.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(.025f, .035f, .07f, .88f);
            Label("Title", 205, 48).text = "关卡通关";
            _stage = Label("Stage", 120, 26);
            _score = Label("Score", 20, 38);
            _score.color = new Color(1f, .83f, .46f);
            _total = Label("Total", -65, 30);
            _hint = Label("Hint", -205, 24);
            Hide();
        }

        Text Label(string label, float y, int size)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Text));
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

        public void Show(StageResult result, bool finalStage)
        {
            _stage.text = $"STAGE {result.StageIndex + 1}   {result.StageId}";
            _score.text = $"本关得分    {result.Score:N0}";
            _total.text = $"累计分数    {result.TotalScore:N0}";
            _hint.text = finalStage ? "按 Z 查看总成绩" : "按 Z 进入下一关";
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
        void OnDestroy()
        {
            if (_font == null) return;
            if (Application.isPlaying) Destroy(_font);
            else DestroyImmediate(_font);
        }
    }
}

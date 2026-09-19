using UnityEngine;

namespace ShinySTG.UI
{
    /// <summary>HUD 编辑入口与引用。布局完全由 CanvasScaler / RectTransform 管理，无运行时写操作。</summary>
    public sealed class GameplayViewportLayout : MonoBehaviour
    {
        [SerializeField, Tooltip("编辑窗口的 UI 坐标根节点。")]
        RectTransform _layoutRoot;
        [SerializeField, Tooltip("默认编辑的信息面板。")]
        RectTransform _sidebar;
        public RectTransform LayoutRoot => _layoutRoot;
        public RectTransform Sidebar => _sidebar;
    }
}

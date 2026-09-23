using UnityEngine;

namespace ShinySTG.Background
{
    public enum BackgroundImageLayer { Lower, Upper }
    public enum BackgroundImageFit { Cover, Stretch }

    [CreateAssetMenu(menuName = "STG/Background/Image", fileName = "BackgroundImage")]
    public sealed class BackgroundImageDefinition : ScriptableObject
    {
        [Tooltip("背景 Sprite，支持 Multiple 切图。图集请关闭 Tight Packing 和 Allow Rotation。")]
        public Sprite Sprite;
        [Tooltip("在 Sprite 自身区域内平铺 UV；关闭时将 UV 限制在边缘。")]
        public bool Repeat;
        [Tooltip("颜色乘数，Alpha 控制最终透明度。")]
        public Color Tint = Color.white;
        [Tooltip("Cover 等比铺满并居中裁切；Stretch 拉伸填满背景相机视口。")]
        public BackgroundImageFit Fit = BackgroundImageFit.Cover;
        [Tooltip("在适配画面后的 UV 上应用的缩放；必须大于零。")]
        public Vector2 UvScale = Vector2.one;
        [Tooltip("贴图初始 UV 偏移。")]
        public Vector2 UvOffset;
        [Tooltip("每秒 UV 偏移，跟随背景暂停和游戏时间缩放。")]
        public Vector2 ScrollSpeed;

        internal bool IsValid => Sprite != null && (!Sprite.packed ||
            Sprite.packingMode == SpritePackingMode.Rectangle && Sprite.packingRotation == SpritePackingRotation.None)
            && Finite(Tint.r) && Finite(Tint.g)
            && Finite(Tint.b) && Finite(Tint.a) && Tint.a >= 0f && Tint.a <= 1f
            && (Fit == BackgroundImageFit.Cover || Fit == BackgroundImageFit.Stretch)
            && Finite(UvScale.x) && Finite(UvScale.y) && UvScale.x > 0f && UvScale.y > 0f
            && Finite(UvOffset.x) && Finite(UvOffset.y) && Finite(ScrollSpeed.x) && Finite(ScrollSpeed.y);

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

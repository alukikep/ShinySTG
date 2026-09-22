using UnityEngine;

namespace ShinySTG.Items
{
    public enum ItemKind { SmallPower, LargePower, Score, Bomb, OneUp }

    [CreateAssetMenu(menuName = "STG/Items/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("道具效果；小 P 固定 +0.01，大 P 固定 +1，Bomb 和 1UP 固定 +1。")]
        public ItemKind Kind;
        [Tooltip("道具图片；留空时显示带颜色的方块。")]
        public Sprite Sprite;
        [Tooltip("显示颜色。")]
        public Color Tint = Color.white;
        [Min(0.01f), Tooltip("道具显示宽度，世界单位。")]
        public float Size = 0.3f;
        [Min(0), Tooltip("拾取该道具获得的分数；0 表示不计分。")]
        public int ScoreValue = 100;
    }
}

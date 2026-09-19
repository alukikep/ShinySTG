using ShinySTG.Audio;
using UnityEngine;

namespace ShinySTG.Presentation.SpellDeclaration
{
    [CreateAssetMenu(menuName = "STG/Spell Declaration", fileName = "SpellDeclaration")]
    public sealed class SpellDeclarationDefinition : ScriptableObject
    {
        [Tooltip("宣言横幅显示的符卡名称。")]
        public string DisplayName;
        [Tooltip("宣言立绘；留空时只显示横幅。")]
        public Sprite Portrait;
        [Tooltip("宣言开始时播放的非循环短音效；取消视觉时声音自然结束。")]
        public SfxCue Sfx;
        [Min(0f), Tooltip("立绘与横幅入场秒数。")]
        public float EnterDuration = .35f;
        [Min(0f), Tooltip("完整显示的停留秒数。")]
        public float HoldDuration = 1.2f;
        [Min(0f), Tooltip("淡出与退场秒数。")]
        public float ExitDuration = .35f;
        [Min(0f), Tooltip("以 Canvas 参考分辨率计的水平滑动距离。")]
        public float SlideDistance = 240f;
    }
}

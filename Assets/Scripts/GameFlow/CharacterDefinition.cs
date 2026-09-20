using UnityEngine;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow
{
    [CreateAssetMenu(menuName = "STG/Character", fileName = "Character")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Tooltip("稳定的角色标识。")]
        public string Id;
        [Tooltip("菜单显示名称。")]
        public string DisplayName;
        [TextArea, Tooltip("角色简介。")]
        public string Description;
        [Tooltip("可选角色立绘。")]
        public Sprite Portrait;
        [Tooltip("根节点带 Player 的玩家 prefab，包含移动、主弹与子机配置。")]
        public PlayerController PlayerPrefab;
    }
}

using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.GameFlow
{
    [CreateAssetMenu(menuName = "STG/Stage", fileName = "Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        [Tooltip("稳定的关卡标识。")]
        public string Id;
        [Tooltip("已加入 Build Settings 的场景完整路径，例如 Assets/Scenes/Gameplay.unity。")]
        public string ScenePath;
        [Tooltip("时间轴和音乐配置；初始背景由目标场景配置。")]
        public LevelDefinition Level;
        [Tooltip("玩家的世界空间出生位置。")]
        public Vector3 SpawnPosition = new Vector3(0f, -3f, 0f);
    }
}

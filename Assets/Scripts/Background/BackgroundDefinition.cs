using UnityEngine;

namespace ShinySTG.Background
{
    [CreateAssetMenu(menuName = "STG/Background/Definition", fileName = "BackgroundDefinition")]
    public sealed class BackgroundDefinition : ScriptableObject
    {
        [Tooltip("完整循环布景 Prefab，根节点须有 LoopingBackgroundStrip；不要包含相机或控制器。")]
        public GameObject ContentPrefab;
        [Tooltip("初始镜头局部位置。")]
        public Vector3 CameraPosition = new Vector3(0f, 8f, -10f);
        [Tooltip("初始镜头局部欧拉角。")]
        public Vector3 CameraEulerAngles = new Vector3(30f, 0f, 0f);
        [Range(1f, 179f), Tooltip("背景相机视野角。")]
        public float FieldOfView = 50f;
        [Min(0f), Tooltip("新背景滚动速度。")]
        public float ScrollSpeed = 8f;
        [Tooltip("开启后换景时使用下方底色并切换为 Solid Color；关闭则保留当前相机底色与清屏模式。")]
        public bool OverrideClearColor = false;
        [Tooltip("仅在覆盖背景底色开启时生效；建议与布景雾色一致，不会修改材质。")]
        public Color ClearColor = new Color(0.06f, 0.08f, 0.12f, 1f);
    }
}

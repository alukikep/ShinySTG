using UnityEngine;

namespace ShinySTG.Background
{
    [CreateAssetMenu(menuName = "STG/Background/Cue", fileName = "BackgroundCue")]
    public sealed class BackgroundCue : ScriptableObject
    {
        [Tooltip("CameraRig 相对父物体的目标位置。")]
        public Vector3 LocalPosition = new Vector3(0f, 8f, -10f);
        [Tooltip("CameraRig 的目标局部欧拉角；按最短旋转路径过渡。")]
        public Vector3 LocalEulerAngles = new Vector3(30f, 0f, 0f);
        [Range(1f, 179f), Tooltip("背景透视相机的目标垂直视野角。")]
        public float FieldOfView = 50f;
        [Min(0f), Tooltip("循环布景的目标滚动速度。")]
        public float ScrollSpeed = 8f;
        [Min(0f), Tooltip("过渡秒数；0 表示立即应用。")]
        public float Duration = 2f;
        [Tooltip("横轴为归一化时间，纵轴为进度。需从 (0,0) 到 (1,1)，进度限制在 0~1。")]
        public AnimationCurve Easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}

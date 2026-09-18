using System;
using UnityEngine;

namespace ShinySTG.Background
{
    [CreateAssetMenu(menuName = "STG/Background/Loop Cue", fileName = "BackgroundLoopCue")]
    public sealed class BackgroundLoopCue : ScriptableObject
    {
        [Min(0f), Tooltip("从当前镜头首次进入节点 0 的秒数；0 表示立即进入。")]
        public float EntryDuration = 2f;
        [Tooltip("首次进入节点 0 的缓动曲线。")]
        public AnimationCurve EntryEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Min(0f), Tooltip("进入期间过渡到此滚动速度，循环期间保持。")]
        public float ScrollSpeed = 8f;
        [Tooltip("按顺序循环；最后一个节点平滑回到节点 0。支持拖动排序。")]
        public Node[] Nodes = { new Node(), new Node() };

        [Serializable]
        public sealed class Node
        {
            [Tooltip("镜头目标局部位置。")]
            public Vector3 LocalPosition = new Vector3(0f, 8f, -10f);
            [Tooltip("镜头目标局部欧拉角，按最短旋转路径过渡。")]
            public Vector3 LocalEulerAngles = new Vector3(30f, 0f, 0f);
            [Range(1f, 179f), Tooltip("目标垂直视野角。")]
            public float FieldOfView = 50f;
            [Min(0f), Tooltip("从前一个节点到达此节点的秒数；首次进入节点 0 使用 Entry Duration。")]
            public float Duration = 2f;
            [Min(0f), Tooltip("到达此节点后停留的秒数。")]
            public float HoldDuration;
            [Tooltip("到达此节点的缓动曲线，需从 (0,0) 到 (1,1)。")]
            public AnimationCurve Easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }
}

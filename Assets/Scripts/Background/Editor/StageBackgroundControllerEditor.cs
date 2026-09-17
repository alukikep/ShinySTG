using UnityEditor;
using UnityEngine;

namespace ShinySTG.Background.Editor
{
    [CustomEditor(typeof(StageBackgroundController))]
    internal sealed class StageBackgroundControllerEditor : UnityEditor.Editor
    {
        BackgroundCue _previewCue;
        BackgroundPlaybackHandle _lastAttempt;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                DrawDefaultInspector();
            EditorGUILayout.Space();
            _previewCue = (BackgroundCue)EditorGUILayout.ObjectField("Preview Cue", _previewCue, typeof(BackgroundCue), false);
            var controller = (StageBackgroundController)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.isActiveAndEnabled || _previewCue == null))
                if (GUILayout.Button("Play Cue")) _lastAttempt = controller.Play(_previewCue);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button(controller.IsPaused ? "Resume Background" : "Pause Background"))
                {
                    if (controller.IsPaused) controller.Resume(); else controller.Pause();
                }
                if (GUILayout.Button("Cancel Playback")) controller.CancelPlayback();
                if (GUILayout.Button("Reset Entire Background")) controller.ResetBackground();
            }
            EditorGUILayout.LabelField("Playback", controller.CurrentPlayback?.Status.ToString() ?? "Idle");
            EditorGUILayout.LabelField("Paused", controller.IsPaused ? "Yes" : "No");
            if (_lastAttempt?.Failure != null)
                EditorGUILayout.HelpBox(_lastAttempt.Failure, MessageType.Warning);
            else if (controller.CurrentPlayback?.Failure != null)
                EditorGUILayout.HelpBox(controller.CurrentPlayback.Failure, MessageType.Warning);
            EditorGUILayout.HelpBox("暂停保留进度；取消保留当前画面和速度；完整重置恢复启动状态。禁用控制器取消过渡，道路仍独立滚动。", MessageType.Info);
        }
    }
}

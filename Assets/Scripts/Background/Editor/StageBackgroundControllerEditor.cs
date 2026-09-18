using UnityEditor;
using UnityEngine;

namespace ShinySTG.Background.Editor
{
    [CustomEditor(typeof(StageBackgroundController))]
    internal sealed class StageBackgroundControllerEditor : UnityEditor.Editor
    {
        BackgroundCue _previewCue;
        BackgroundLoopCue _previewLoop;
        BackgroundPlaybackHandle _lastAttempt;
        BackgroundDefinition _previewBackground;
        float _fadeOut = 1f;
        float _fadeIn = 1f;

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
            EditorGUILayout.Space();
            _previewLoop = (BackgroundLoopCue)EditorGUILayout.ObjectField("Loop Cue", _previewLoop, typeof(BackgroundLoopCue), false);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.isActiveAndEnabled || _previewLoop == null))
                if (GUILayout.Button("Play Loop")) _lastAttempt = controller.PlayLoop(_previewLoop);
            EditorGUILayout.Space();
            _previewBackground = (BackgroundDefinition)EditorGUILayout.ObjectField("Next Background", _previewBackground, typeof(BackgroundDefinition), false);
            _fadeOut = EditorGUILayout.FloatField("Fade Out Seconds", _fadeOut);
            _fadeIn = EditorGUILayout.FloatField("Fade In Seconds", _fadeIn);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.isActiveAndEnabled || _previewBackground == null))
                if (GUILayout.Button("Switch Background"))
                    _lastAttempt = controller.SwitchBackground(_previewBackground, _fadeOut, _fadeIn);
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
            EditorGUILayout.LabelField("Playback Kind", controller.PlaybackKind);
            EditorGUILayout.LabelField("Paused", controller.IsPaused ? "Yes" : "No");
            if (_lastAttempt?.Failure != null)
                EditorGUILayout.HelpBox(_lastAttempt.Failure, MessageType.Warning);
            else if (controller.CurrentPlayback?.Failure != null)
                EditorGUILayout.HelpBox(controller.CurrentPlayback.Failure, MessageType.Warning);
            EditorGUILayout.HelpBox("暂停保留进度；取消保留当前画面和速度；完整重置恢复启动状态。禁用控制器取消过渡，道路仍独立滚动。", MessageType.Info);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    [CustomEditor(typeof(BossController))]
    public sealed class BossControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var boss = (BossController)target;
            EditorGUILayout.HelpBox("阶段、条件、血管与演出统一在 Boss Encounter SO 配置。", MessageType.Info);
            var owner = boss.GetComponent<Boss>();
            if (owner != null && owner.Encounter != null && GUILayout.Button("选择 Encounter 配置"))
                Selection.activeObject = owner.Encounter;
            if (Application.isPlaying) DrawRuntime(boss, boss.GetComponent<BossHealth>());
        }

        static void DrawRuntime(BossController boss, BossHealth health)
        {
            EditorGUILayout.LabelField("运行时观察", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"阶段 {boss.CurrentPhaseIndex + 1} · {boss.PhaseElapsedSeconds:0.00}s · 过渡 {boss.IsTransitioning} · 保护 {boss.IsBarTransitionProtected}");
            if (boss.Phases == null || boss.CurrentPhaseIndex < 0 || boss.CurrentPhaseIndex >= boss.Phases.Length) return;
            var phase = boss.Phases[boss.CurrentPhaseIndex];
            if (phase == null) return;
            if (health != null && health.UsesBarStates && !health.IsDead)
            {
                var bar = health.Bars[health.CurrentBarIndex];
                EditorGUILayout.LabelField("当前血管", bar.Name);
                EditorGUILayout.LabelField("当前倒计时", boss.TryGetBarRemainingSeconds(out float seconds) ? $"{seconds:0.00}s" : "无时限");
                EditorGUILayout.LabelField("剩余血量", $"{health.CurrentBarPercent:0.##}%");
                var states = health.UsesSegments ? health.CurrentSegment.States : bar.States;
                if (health.UsesSegments)
                    EditorGUILayout.LabelField("当前分段", $"{health.CurrentSegment.Name} · {health.CurrentSegmentPercent:0.##}% · {boss.SegmentElapsedSeconds:0.00}s");
                bool last = states[states.Length - 1] == phase;
                string scope = health.UsesSegments ? "本段" : "本管";
                EditorGUILayout.LabelField("下一状态", last ? $"保持到{scope}结束" : phase.AdvanceMode == BossPhase.StateAdvanceMode.Time
                    ? $"状态时间达到 {phase.AdvanceAfterSeconds}s" : $"{scope}血量 ≤ {phase.AdvanceAtPercent}%");
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}

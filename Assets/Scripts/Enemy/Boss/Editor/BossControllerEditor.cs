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
            EditorGUILayout.LabelField("退出条件", boss.IsTransitioning ? "等待过渡（不检测）" : phase.ShouldExit(boss) ? "满足" : "未满足");
            if (phase.ExitMode != PhaseExitMode.Conditions || phase.ExitConditions == null) return;
            foreach (var condition in phase.ExitConditions)
            {
                if (condition == null) { EditorGUILayout.LabelField("空条件"); continue; }
                string value = "不可用";
                if (condition.Kind == PhaseExitConditionKind.PhaseTimeAtLeast) value = $"{boss.PhaseElapsedSeconds:0.00}s";
                else if (condition.Kind == PhaseExitConditionKind.TotalHpPercentAtMost && health != null) value = $"{health.TotalHpPercent:0.##}%";
                else if (condition.Kind == PhaseExitConditionKind.Signal) value = boss.GetSignal(condition.SignalTrigger?.SignalIndex ?? -1)?.CurrentValue.ToString("0.##") ?? value;
                else if (health != null && health.TryGetBarPercent(condition.BarId, out float percent)) value = $"{percent:0.##}%";
                EditorGUILayout.LabelField(condition.Kind.ToString(), $"{value} · {(condition.IsSatisfied(boss) ? "满足" : "未满足")}");
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}

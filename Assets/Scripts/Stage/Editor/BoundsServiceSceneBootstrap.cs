using ShinySTG.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinySTG.Stage.Editor
{
    /// <summary>
    /// 场景启动时自动挂 <see cref="BoundsService"/> —— 美术不需要手动配置。
    ///
    /// 触发时机:<see cref="EditorSceneManager.sceneOpened"/>(场景被打开)。
    /// 防重复:已经存在 BoundsService 组件的 GameObject 直接跳过。
    ///
    /// 为什么用 [InitializeOnLoad] 而不是改 .unity YAML:
    ///   - 改 YAML 需要硬编 BoundsService.cs.meta 的 GUID,而 .meta 是 Unity 首次导入时生成的,
    ///     在那之前 GUID 不存在,人工写会跟 Unity 实际生成的 GUID 不一致 → 引用断裂。
    ///   - 用代码挂载,无论 GUID 怎么变都能正确引用。
    ///   - 美术如果主动在场景里挂了 BoundsService,代码不会重复创建(IsAlreadyPresent 检测)。
    ///   - 与 LevelSceneGizmos / BoundsServiceHandles 同一套路,Editor-only,不影响运行时构建。
    ///
    /// 默认值采用 BoundsService 的字段初始值:
    ///   - PlayableArea = (-3.5, -4.5, 7, 9)   与 PlayerMovement 旧 MinX/MaxX/MinY/MaxY 完全一致
    ///   - CullingArea  = (-10, -10, 20, 20)   与 Bullet 旧硬编码 ±10/±20 完全一致
    /// 行为完全保留;后续美术可在 Inspector 调整 → 实时看到 Scene Gizmo 变化。
    /// </summary>
    [InitializeOnLoad]
    internal static class BoundsServiceSceneBootstrap
    {
        const string BOUNDS_GO_NAME = "BoundsService"; // 自动挂载的 GameObject 名(便于人工识别)

        static BoundsServiceSceneBootstrap()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (mode == OpenSceneMode.Additive) return; // Additive 打开不强制挂,避免污染调用方场景
            EnsureBoundsServiceInScene(scene);
        }

        /// <summary>
        /// 菜单入口(供美术手动触发):STG → Stage → Ensure BoundsService in Active Scene。
        /// </summary>
        [MenuItem("STG/Stage/Ensure BoundsService in Active Scene")]
        static void EnsureBoundsServiceInActiveScene()
        {
            EnsureBoundsServiceInScene(SceneManager.GetActiveScene());
        }

        static void EnsureBoundsServiceInScene(Scene scene)
        {
            // 1) 场景里已有 BoundsService 组件 → 跳过(尊重美术手动配置)
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<BoundsService>(true) != null) return;
            }

            // 2) 没有 → 新建 GameObject + BoundsService,挂到场景根
            var go = new GameObject(BOUNDS_GO_NAME);
            var bs = go.AddComponent<BoundsService>();

            // 标记场景 dirty,让美术保存改动前能看到 * 提示
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[BoundsService] 自动挂载到场景 '{scene.name}'(根 GameObject: {BOUNDS_GO_NAME})。\n" +
                      $"默认 PlayableArea={bs.PlayableArea}, CullingArea={bs.CullingArea}。\n" +
                      $"如不需要,请删除 GameObject 或在 Inspector 调整数值。", bs);
        }
    }
}

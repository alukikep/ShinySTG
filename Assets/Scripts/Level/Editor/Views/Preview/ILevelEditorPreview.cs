using ShinySTG.Level;
namespace ShinySTG.Level.Editor.Views.Preview
{
    /// <summary>
    /// 关卡编辑器 Preview 接口(编辑模式下模拟一遍生成)。
    /// 主窗口持有 ILevelEditorPreview 实例,OnEditorUpdate 调 Tick 推进。
    ///
    /// 设计要点:
    ///   - 在 Editor(非 Play)模式跑 → 不能依赖 Time.deltaTime,改用 EditorApplication.timeSinceStartup 差值
    ///   - 临时生成的 GameObject 全部挂到 _previewRoot 下,Stop 时统一 Destroy(避免污染场景)
    ///   - SetTime(seconds) 是回退游标接口:清空 _fired + 已生成实例 + Tick 到 seconds
    /// </summary>
    public interface ILevelEditorPreview
    {
        bool IsPlaying { get; }
        float CurrentTime { get; }

        void Start(LevelDefinition def, float startTime = 0f);
        void Stop();
        void Pause(bool paused);
        void SetTime(float seconds);

        void Tick(double deltaSeconds);
    }
}
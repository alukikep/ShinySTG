# 舞台边界系统

> BoundsService 单例 + PlayableArea + CullingArea

> 本板块对应 ARCHITECTURE § 12. 舞台边界系统(BoundsService)(原 ARCHITECTURE.md 第 1230–1303 行)。
> 本文档面向项目维护者,不复制实现细节,字段 / 数值 / 默认值以源文件为准。

---

## 12. 舞台边界系统(BoundsService)

> 把"玩家活动边界" + "子弹自动回收边界"**集中到一个场景单例**,不再散落在 PlayerMovement 和 Bullet 的代码里。Inspector 改数值 + Scene 视图实时可视化,设计意图与代码同步。

### 12.1 为什么需要

旧实现两套边界各自为政,各自有 magic number:

| 系统 | 旧位置 | 旧值 | 问题 |
|---|---|---|---|
| 玩家活动区 | `PlayerMovement.MinX/MaxX/MinY/MaxY` 4 个 float | ±3.5 / ±4.5 | 美术想改得打开 Player prefab;且无 Scene 可视化 |
| 子弹回收区 | `Bullet.cs:243` 硬编码 | `Mathf.Abs(x)>10 \|\| Abs(y)>20` | 注释直接写"先实现,后续再优化";无配置入口、无可视化 |

两个值也没有任何关联 —— 玩家能到的区域(±3.5/±4.5)比子弹回收区(±10/±20)小,但这是巧合不是设计。

### 12.2 职责分工

- **`BoundsService`(场景单例,`MonoBehaviour`)** ——
  - 暴露两块独立的世界坐标 `Rect`:
    - `PlayableArea`:玩家活动区(矩形 clamp)。
    - `CullingArea`:子弹回收区(飞出即 `BulletPool.Return`)。
  - 提供便捷只读接口:`PlayableMin/Max`、`CullingMin/Max`、`ContainsPlayable(p)`、`ContainsCulling(p)`、`ClampToPlayable(p)`。
  - `OnDrawGizmos` 画两块彩色 WireCube(Green = Playable,Orange = Culling),无需选中也能看到。
  - 默认值(`PlayableArea=(-3.5,-4.5,7,9)` / `CullingArea=(-10,-10,20,20)`)与旧值 100% 等价。

- **`BoundsServiceHandles`(Editor-only,`[InitializeOnLoad]`)** ——
  - 订阅 `SceneView.duringSceneGui`(与 `LevelSceneGizmos` 同套路)。
  - 在 Scene 视图里画 4 边的 `PositionHandle`(顶/底/左/右各一个),拖动改 `Rect`。
  - 拖动期间 `Undo.RecordObject` + `EditorGUI.BeginChangeCheck`,松开自动 MarkDirty。

- **`BoundsServiceSceneBootstrap`(Editor-only,`[InitializeOnLoad]`)** ——
  - 订阅 `EditorSceneManager.sceneOpened`,场景打开时检查是否有 `BoundsService` 组件,**没有则自动挂一个到根 GameObject(`BoundsService`)**。
  - **不强制场景手改** —— 美术什么都不用做,打开 SampleScene 即可看到 BoundsService 组件;菜单 `STG → Stage → Ensure BoundsService in Active Scene` 可手动触发。

### 12.3 协作边界

- **`PlayerMovement`** ——
  - 删除 4 个 float 字段(MinX/MaxX/MinY/MaxY)。
  - `Update` 里优先读 `BoundsService.Instance.ClampToPlayable(...)`;**无单例时 fallback 到内置 `±3.5/±4.5`(与旧值一致)**。
  - 加 `using ShinySTG.Stage;` —— 是 PlayerMovement 对 BoundsService 唯一的耦合点。

- **`Bullet`** ——
  - `Update` 里把硬编码的 `Mathf.Abs(x)>10 || Abs(y)>20` 改成读 `BoundsService.Instance.ContainsCulling(pos)`。
  - **无单例时 fallback 到原硬编码值**(保留历史行为)。
  - 不 `using ShinySTG.Stage;`,用全限定名 `ShinySTG.Stage.BoundsService.Instance`(对齐 `CollisionService.cs:5-7` 的 namespace-同名陷阱规避策略)。

- **其他系统** ——
  - 任何未来需要"边界语义"的系统(例如 Boss 演出区、擦弹半径、关卡编辑器选区)都通过 `BoundsService.Instance` 读,不再各自硬编码。
  - 本组件不读 / 不写 Player / Bullet 的位置字段,只暴露 Rect + 便捷函数,**纯读侧基础设施**。

### 12.4 与既有层的关系

| 既有层 | 怎么用 BoundsService | 是否修改它 |
|---|---|---|
| `PlayerMovement` | `ClampToPlayable(p)` 替代旧的 4 float | ✅ 改动但只删字段 + 加 using,行为 fallback 兼容 |
| `Bullet` | `ContainsCulling(p)` 替代硬编码 | ✅ 改动但只改 1 段判断 + 加全限定名引用,fallback 兼容 |
| `HitboxComponent` | 0 改动(Gizmo 与本组件无关) | ❌ 完全不动 |
| `CollisionService` / 网格 | 0 改动(本组件不参与碰撞查询) | ❌ 完全不动 |
| 关卡编辑器 | 0 改动 | ❌ 完全不动 |

### 12.5 扩展指南

- **新加边界类型**(例如 Boss 战区域、擦弹检测区):在 BoundsService 加 `Rect` 字段 + 对应的 `ContainsXxx` / `ClampToXxx` 接口 + 在 `OnDrawGizmos` / `DrawResizableRect` 各加一行;无需新建组件(集中管理是设计意图)。
- **改默认值**:直接改 `BoundsService.cs` 字段初始值;旧 `.unity` 场景不会受影响(新挂载的 BoundsService 用新默认值)。
- **完全关闭可视化**:BoundsService 组件 Inspector 的 `AlwaysDraw = false`,或在玩家性能吃紧时通过 `BoundsService.Enabled = false` 整体禁用。
- **自定义美术边界**(例如剧情演出时的"屏幕震动墙"):在对应关卡的 BoundsService 上手动调 Rect 即可,代码 0 改动。

详见 `Assets/Scripts/Stage/BoundsService.cs` + `Assets/Scripts/Stage/Editor/BoundsServiceHandles.cs` + `Assets/Scripts/Stage/Editor/BoundsServiceSceneBootstrap.cs`。

详见 [`Assets/Scripts/Audio/README.md`](../../Assets/Scripts/Audio/README.md)(配置说明)和
[`LEVEL_EDITOR.md`](../../LEVEL_EDITOR.md#音频集成)(关卡编辑器使用)。


---


## 与其他板块的关系

本板块与其他板块的依赖 / 协作关系(简单文字说明):

- [bullet](./arch-bullet.md) — Bullet 出界回收 + 反弹都在此区域内判定
- [player](./arch-player.md) — PlayerMovement 玩家活动边界
- [hitbox](./arch-hitbox.md) — Hitbox 网格尺寸由 CullingArea 派生(可选)

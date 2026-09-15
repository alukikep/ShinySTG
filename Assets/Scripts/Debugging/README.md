# 常驻坐标轴工具(Coordinate Axis Overlay)

> 给「和坐标相关的设置」提供 Scene 视图参考。配 FirePattern.PositionOffset /
> SpawnPosition / BoundsService 等带 Vector2 字段的资产时,直接在 Scene 视图里量出目标坐标对应的世界位置。

---

## 启用方式

Unity 菜单栏 -> **Window -> STG -> Coordinate Axis Overlay**

打开 Inspector 窗口后,**Enabled 默认就是 true** -- Scene 视图立刻出现红绿主轴 +
整数刻度 + 数字标签 + 灰色网格 + PlayableArea 4 角标。

> 注意:只在 **Scene 视图** 生效,Game 视图不画(避免污染玩家视角)。

## 字段说明

| 字段 | 默认 | 含义 |
|---|---|---|
| **Enabled** | `true` | 总开关。勾掉 -> Scene 视图完全停止画所有内容。 |
| **Axis Extent** | `20` | 主轴线长度,对称 +-extent。世界坐标范围。 |
| **Grid Extent** | `10` | 参考网格覆盖范围,对称 +-extent。`0` = 不画网格。 |
| **Major Step** | `1` | 主刻度间距(数字标签步长)。 |
| **Minor Step** | `0.5` | 次刻度间距。`0` = 不画次刻度;必须 < Major Step。 |
| **Show Grid** | `true` | 是否画灰色细网格(辅助目测远处的点)。 |
| **Show Labels** | `true` | 是否画刻度数字 + 原点 (0, 0) 标签 + 边界尺寸文字。 |
| **Show Stage Bounds Corners** | `true` | 是否联动 BoundsService 画 PlayableArea 4 角标。 |
| **X/Y/Grid/Label/Corner Color** | 红/绿/灰/白/黄 | 各图层颜色,Alpha 可调透明度。 |

所有字段通过 `EditorPrefs` 持久化(key 前缀 `ShinySTG.CoordAxis.*`),关 Unity
重启保留配置;点窗口底部 **Reset to Defaults** 一键清掉。

## 与 BoundsService 的关系

工具会自动读取场景里的 `BoundsService.Instance`:

- **场景挂了 BoundsService** -> 用它的 `PlayableArea` / `CullingArea` 在 4 角画小三角形 + 尺寸文字。
- **没挂 BoundsService** -> fallback 到默认 `PlayableArea=(-3.5,-4.5,7,9)` / `CullingArea=(-10,-10,20,20)`(与 `PlayerMovement.cs` 默认值一致)。

> 这与 `BoundsService.cs:16-19` 的 fallback 策略完全对齐。

## 实现要点(给后续维护者)

- **纯编辑器工具**:`Assets/Scripts/Debugging/Editor/` 下的 `Editor/` 文件夹让 Unity 自动归
  `Assembly-CSharp-Editor`,**不进 build**。零运行时成本。
- **不挂 GameObject**:订阅 `SceneView.duringSceneGui`(与 `BoundsServiceHandles.cs` /
  `LevelSceneGizmos.cs` 同套路)。
- **不引入 asmdef**:与项目其它代码保持一致(项目本身无任何 asmdef)。
- **代码风格**:对照 `CONTRIBUTING.md §1`(PascalCase / `_camelCase` / `[Tooltip]` 中文)。
- **不画 Game 视图**:Game 视图钩子依赖具体渲染管线(URP/HDRP/Built-in),且会污染玩家
  视角,故不实现。需要时可加 `Camera.onPostRender` 订阅。

## 常见用法

1. **配 FirePattern.PositionOffset**:把值填进 Inspector,看 Scene 视图里
   `(PositionOffset.x, PositionOffset.y)` 落在主轴哪个位置,直观判断发射点相对敌人体素。
2. **配 Boss.SpawnPosition**:设 `(0, 3.5)`,看是否压在 PlayableArea 上边界(+-3.5)上。
3. **配 BoundsService.PlayableArea**:拖窗口 / 改数字时,直接读工具画的 4 角尺寸文字
   (`Playable: -3.5~3.5 x -4.5~4.5`)做交叉验证。
4. **配弹幕角度偏移**:用主轴当视觉参考,数 (0,0) -> (x,y) 落在第几象限,目测 BaseAngle。

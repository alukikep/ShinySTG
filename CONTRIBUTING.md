# ShinySTG 开发约定

> 给"写代码的人 + 和 LLM 协作的人"。
> 读架构看 [ARCHITECTURE.md](./ARCHITECTURE.md);动手改代码 / 启动任务前看这里。

---

## 目录

1. [编码规范](#1-编码规范)
2. [架构扩展入口](#2-架构扩展入口)
3. [与 LLM 协作的约定](#3-与-llm-协作的约定)
4. [工具链踩坑笔记](#4-工具链踩坑笔记)

---

## 1. 编码规范

### 1.1 文件编码

- **所有文本文件:UTF-8 无 BOM**(`.cs` / `.md` / `.json` / `.asset` / `.flow` / `.prefab`)。
- BOM 会被 Unity `.meta` 系统误识别,导致 .meta 被错误重新生成(看起来"什么都没改"但 git diff 一大片)。
- PowerShell 写文件必须显式:

  ```powershell
  $utf8NoBom = New-Object System.Text.UTF8Encoding($False)
  [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
  ```

### 1.2 命名 / 风格

- **类 / 公开方法 / 属性**:PascalCase(`BulletPool`、`Get`、`CurrentBar`)。
- **私有字段**:`_camelCase`(`_poolByPrefab`、`_elapsedInCurrent`)。
- **公开字段**:camelCase(`Speed`、`AngularSpeed`)。
- **常量**:`UPPER_SNAKE_CASE`。
- **接口**:`I<Name>`。
- **缩进**:4 空格,不用 tab。
- **namespace**:`ShinySTG.*`。

### 1.3 Git / 文件管理

- **`*.meta` 永远要进库**。漏了 .meta,Unity 在另一台机器上会重新生成 GUID,所有引用断裂。
- **临时文件**(`*.bak` / `*.tmp` / 协作过程产生的 `*.ps1`)放进 `Temp/`(Unity 默认 .gitignore),不要丢在根目录。
- **大改前先备份**:`copy <file> <file>.bak` 即可,无需建分支(除非预期会反复横跳)。

---

## 2. 架构扩展入口

> 完整决策树见 [ARCHITECTURE.md §6](./ARCHITECTURE.md#6-扩展指南)。这里只给"文件放哪"。

| 想加什么 | 在哪个文件夹新建 | 备注 |
|---|---|---|
| 新弹幕形态(螺旋 / 樱花 / ...) | `Assets/Scripts/Bullet/FirePattern/` | 子类继承 `FirePattern`,生成子弹必须走基类 `SpawnBullet` helper(否则 modifier 不挂) |
| 新发射扩展(BaseAngle / 瞄准玩家 / 瞄准 Boss / 每发旋转 / 振荡 ...) | `Assets/Scripts/Bullet/FireExtension/` | 子类继承 `FireExtension` + 加 `[SRName("FireExtension/<名字>")]`,自动出现在所有 FirePattern 资产的下拉菜单。**Pipeline 模型**:每个模块实现 `ProcessAngle(from, baseRotationRad, currentAngleRad)`,数组按顺序串成"角度管道"(详见 ARCHITECTURE §3.1)。**位置偏移**:只在 Base 模块上,通过 `PositionOffset: Vector2` 字段在世界坐标系下设置炮口偏移(由 Resolver 在 pipeline 入口处应用) |
| 新敌人行为(动画 / 隐身 / 加血) | `Assets/Scripts/Enemy/AI/Actions/` | 子类继承 `EnemyAction`,加 `[SRName("Action/<名字>")]` |
| 新移动方式(贝塞尔 / 圆形 / 追踪) | `Assets/Scripts/Enemy/AI/MoveBehaviours/` | 子类继承 `MoveBehaviour`,加 `[SRName("Move/<名字>")]` |
| **新子弹逻辑效果(加速 / 转向 / 减速 / 分裂 / 追踪)** | `Assets/Scripts/Bullet/` | 子类继承 `BulletModifier`,加 `[SRName("Modifier/<名字>")] + [Serializable]`,在 `Modify(Bullet, dt)` 里改 `b.Speed` / `b.SteerAngle` / `b.AngularSpeed` 等飞行字段。引用类型字段要 override `Clone()` 深拷 |
| **新子弹视觉效果(染色 / 描边 / 残影 / 自发光)** | C# modifier 放 `Assets/Scripts/Bullet/`;配套 shader 放 `Assets/Shaders/`;配套 material `.mat` 放 `Assets/Shaders/` 或 `Assets/Materials/` | modifier 走 **MaterialPropertyBlock**(不走 `Renderer.color` / `material.instance`,详见 ARCHITECTURE §2.4.1 + §2.5 末段)。shader 文件命名 `STG/<名字>`,必须有 `Fallback "Sprites/Default"` 防编译失败黑屏 |
| 新"行为流"资产(符卡 / 小怪模式) | Project 视图右键 → Create → STG → Behavior Flow | SO 资产,无需写代码 |
| 新 Boss 阶段 | `Assets/Scripts/Enemy/Boss/Phases/` | 子类继承 `BossPhase` |
| 新 Boss 阶段切换条件 | `Assets/Scripts/Enemy/Boss/Signals/` | 子类继承 `BossSignal` |
| 新玩家子机位置形态 | `Assets/Scripts/Player/Options/Forms/` | 子类继承 `OptionPositionForm` |
| 新 SpawnEntry 编辑器画法 | `Assets/Scripts/Level/Editor/Drawers/` | 子类继承 `ISpawnEntryDrawer`(**abstract class**,必须 `override Handles` 声明接管类型) |
| 新关卡编辑器 Preview 实现 | `Assets/Scripts/Level/Editor/Views/Preview/` | 实现 `ILevelEditorPreview` 6 个方法 |
| 新 SFX 处理规则(随机抽 clip / pitch 抖动 / cooldown / 自定义) | `Assets/Scripts/Audio/Sfx/` | 子类继承 `SfxRule` + 加 `[SRName("Rule/<名字>")] + [Serializable]`,在 `Process(SfxRequest)` 里改 `req.Clip / req.Volume / req.Pitch` 等。**Pipeline 模型**:SfxCue.Rules 是 `SfxRule[]` 数组,按顺序串行处理(与 FireExtension 同套路)。所有引用类型字段要 override `Clone()` 深拷(若加),否则多颗子弹共享同 SfxCue 时状态会污染 |
| 新 BGM 资产 / 总线 / Cue 分组 | Project 视图右键 → Create → STG → Audio → BGM Track / BGM Playlist / Audio Bus / SFX Cue / SFX Bank / Level Audio Binding | SO 资产,无需写代码 |
| 新 FirePattern 开火音模块(单 cue / 叠多 cue / 按状态发声) | `Assets/Scripts/Bullet/FireExtension/` | 子类继承 `FireSound` + 加 `[SRName("FireSound/<名字>")] + [Serializable]`,在 `OnFireTriggered(position, ownerHitbox)` 里调 `AudioMix.PlaySfx(...)`。**与 FireExtension 的区别**:`FireSound[]` 是"并行触发器"(各模块独立),不是"角度管道"。由 `BulletPool.FireGroup` 在 `pattern.Fire(...)` 之前自动调 `PlayFireSounds()`,Composite 子 pattern 不重复触发 |


**"用 SerializeReference 下拉"的多态扩展点**,三步套路是固定的:

1. 在对应文件夹新建 `<你的>类名.cs`
2. 继承抽象基类,加 `[Serializable, SRName("<下拉菜单路径>")]`
3. override 该 override 的方法

详见 ARCHITECTURE.md §7。

---

## 3. 与 LLM 协作的约定

> 给 LLM 看的清单 + 给"委托 LLM 干活的人"看的检查表。

### 3.1 任务粒度

- **一次任务只改一个完整功能**。不要让 LLM 同时改"行为流 + Boss + 玩家"——三件事耦合度低但代码量大,LLM 在中间回合可能走偏。
- **改 > 100 行的任务,先要 LLM 给风险清单**:会动到哪些文件、是否破坏现有 .prefab / .asset 引用、是否要新建 SO 类型。
- **跨架构层的大改**(例如新增子系统)先在 plan mode 对齐方案,再切 act mode 执行。

### 3.2 LLM 给出的代码必须由人工 review

- 不要直接复制粘贴 LLM 整段生成的代码;至少要:
  - **看懂核心逻辑**(尤其是 SerializeReference / `[SRName]` / `[SerializeField]` 这种属性)
  - **跑通一遍再 commit**(进 Play Mode,验证关键路径)
  - 检查 **字段命名是否对齐项目惯例**(`_camelCase` 私有、PascalCase 类)
- LLM 经常**风格不一致**:同一项目里有的类用 `_pool`,有的用 `m_Pool`。这种混用要人工纠正。

### 3.3 LLM 给出的"实现细节文档"要警惕

-LLM只读代码即可，不要读其他场景数据或者配置之类的东西，很浪费token
- LLM 喜欢写**带伪代码、字段表、调用链**的"实现层文档"。这种文档**寿命很短**——字段一改名就过时。
- 本项目的 ARCHITECTURE.md 只讲**架构与扩展套路**,不复刻源码。**不要让 LLM 把伪代码 / 字段表塞回架构文档**;如果需要,写到对应子目录的 `README.md`。
- 让 LLM 改 ARCHITECTURE.md 时,**先确认它只动了职责 / 类型 / 边界 / 扩展点四块**,没有动源码细节。

### 3.4 任务结束后的清理清单

- [ ] 检查 git status,确认改动范围符合预期(尤其不要有 LLM 误改的 .meta / .csproj)
- [ ] 删除 `Temp/*.ps1` / `*.bak` 等临时文件
- [ ] ARCHITECTURE.md 有结构性变动 → 让 LLM 在对话里**总结新增/删除了哪些章节**,方便人工 review

---

## 4. 工具链踩坑笔记

> 专门记录"会让协作出错的环境 / 工具坑"。
> 每条踩坑都附:**症状 / 原因 / 怎么避免**。
> 后续遇到新踩坑,请补充到这里,别让同一个人(或同一个 LLM)再踩一次。

### 4.1 PowerShell 必须走 `.ps1` + `-File`,不要走 `-Command` 内联

- **症状**:PowerShell 报 `An empty pipe element is not allowed`、`You must provide a value expression following the '+' operator`,或命令静默地什么也没做。
- **原因**:LLM 工具链的外层 shell 可能不是 PowerShell(常见是 bash / Git Bash / VS Code 适配层)。这些 shell 会把字符串里的 `$var` 当作未定义变量替换为空字符串,然后才把字符串交给 PowerShell。结果 PowerShell 收到一段被吃了变量的残缺代码。
- **怎么避免**:
  - PowerShell 多行命令 / 含变量 / 含 here-string → **一律写成 .ps1 文件 + `powershell -ExecutionPolicy Bypass -File ".\path.ps1"` 执行**。
  - 单行无变量的字面量命令可以走 `-Command "..."`,但只要路径含空格或有任何 `$`,就果断走 `-File`。
  - LLM 看到报错像"半个命令",先怀疑这一条。

### 4.2 临时脚本放 `Temp/`,不要放项目根

- **症状**:LLM 协作完成后,git status 看到一堆 `do_trim.ps1`、`verify.ps1`、`new_sections.txt`、`ARCHITECTURE.md.bak`,看起来很乱。
- **原因**:LLM 在执行大任务(比如重写 ARCHITECTURE.md)时会生成辅助 PowerShell 脚本。这些脚本默认会落到"当前工作目录",也就是项目根。
- **怎么避免**:
  - Unity 项目根的 `Temp/` 目录**已经被 Unity 默认 .gitignore 屏蔽**(`Temp/` 在标准 Unity .gitignore 第 1 行),是天然的临时脚本归宿。
  - LLM 在创建 .ps1 之前,**默认路径应该是 `Temp/<task_name>.ps1`**,不是项目根。
  - 执行完任务,**LLM 应主动 `del Temp\*.ps1` 清理**(写进任务的最后一步)。

### 4.3 文本文件必须 UTF-8 无 BOM

- **症状**:PowerShell 读中文输出乱码(`鏂囦欢浣嶇疆:` 这种);或者 Unity 的 .meta 文件被 Git 标记为修改,但代码本身一字未改。
- **原因**:
  - PowerShell 默认 `[Console]::OutputEncoding` 是系统代码页(中文 Windows 是 GBK),不是 UTF-8。
  - .NET `File.WriteAllText` 默认带 BOM。UTF-8 BOM (`EF BB BF`) 会被 Unity .meta 系统误识别为"文件变了",重新生成 .meta。
- **怎么避免**:
  - PowerShell 脚本开头固定写:
    ```powershell
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
    ```
  - 写文件统一用:
    ```powershell
    $utf8NoBom = New-Object System.Text.UTF8Encoding($False)
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
    ```
  - 项目内一切文本按 UTF-8 无 BOM 处理。

### 4.4 文档行号校验:以 PowerShell 的 `Get-Content` 为准,不要相信 LLM 工具自带的 read 缓存

- **症状**:LLM 反复修改文档,中间用 `read_files` / `findstr` 校验"老字符串是否还在",但 `old_text` 匹配失败。原因看起来是"我搞错了行号"。
- **原因**:LLM 的 `read_files` 工具在长对话里可能返回带缓存的行号;且 `findstr` 在 PowerShell 环境下处理 UTF-8 中文行偶尔会错位。
- **怎么避免**:
  - 校验文档结构 / 行号,**用 PowerShell 直接读**:
    ```powershell
    Get-Content .\ARCHITECTURE.md -Encoding UTF8 | Select-String -Pattern '^#{1,3} '
    ```
  - 把这次的行号当作"绝对真值",再决定 `editor` 的 old_text 怎么写。
  - 如果 `editor` 的 old_text 反复匹配不到,**先怀疑文档真实状态**,再怀疑自己的拼写。

### 4.5 文档"瘦身"类任务:先备份再分章节替换

- **症状**:LLM 想把一份 > 1000 行的文档砍到 200 行,但中间出了 1–2 次"残留半截字符串"或"旧章节没删干净"。
- **原因**:LLM 工具的 `editor` 单次替换的 `old_text` 字符量上限约 6000,对于大文档只能分段改;而分段改容易"切到一半忘了删后半段"。
- **怎么避免**:
  - **第一步永远是备份**:`copy <file> <file>.bak`(项目根即可,临时文件会一并清)。
  - **逐章节替换**:用每个章节的 `## N. <标题>` 作为锚点 `old_text`,一次只动一节。
  - **收尾用 PowerShell 做整体切片**:把"保留头" + "保留 §N" + "新追加"拼起来再写回文件,比逐段删更可靠。
  - 完工后 `git diff <file>.bak <file>` 全文对比,人工 review 一遍再 commit。

### 4.6 C# Tooltip 字符串禁止嵌套未转义的 `"`

- **症状**:Unity Console 报 `CS1003: Syntax error, ',' expected`,报错行通常在 `[Tooltip("...")]` 字符串里。
- **原因**:C# 字符串直接量里写 `"` 会提前关闭字符串,编译器把后续文字当成新 token;`@""` verbatim 字符串里 `"` 也必须用 `""` 双写转义。
- **正确写法**:嵌套引号一律 `\"` 转义:
  ```csharp
  [Tooltip("勾上 → 绝对世界坐标(如\"飞到屏幕中央 (0,0)\")。")]
  ```
- **错误示例**:
  ```csharp
  [Tooltip("勾上 → 绝对世界坐标(如"飞到屏幕中央 (0,0)")。")]   // ← CS1003
  ```
- **预防**:
  - 在 Tooltip 里举例子时,优先用中文「」书名号(或『』)替代英文引号,避免引号嵌套。
  - 见 `Assets/Scripts/Enemy/AI/MoveBehaviours/BezierMove.cs:54` 和 `HomingMove.cs:41` 的同款修复(均改用「」)。

### 4.7 namespace 与类同名陷阱(不要 using 后写类成员)

- **症状**:Unity Console 报
  ```
  error CS0234: The type or namespace name 'Instance' does not exist
  in the namespace 'ShinySTG.Player'
  ```
  且实际代码里只写了 `Player.Instance`。
- **原因**:本项目的 `ShinySTG.Player` 是 **namespace**,同时 namespace 里又有一个叫 `Player` 的类(玩家主控)。如果在外层文件写:
  ```csharp
  using ShinySTG.Player;
  ...
  Player.Instance  // ← C# 编译器把 Player 解析为 namespace,找不到 .Instance 成员
  ```
- **正确写法**:**不要 using**,直接用全限定名(第二个 `Player` 是类名):
  ```csharp
  Transform p = ShinySTG.Player.Player.Instance != null
      ? ShinySTG.Player.Player.Instance.transform
      : null;
  ```
- **已踩过此坑的现有代码**(供对照参考):
  - `Assets/Scripts/Hitbox/CollisionService.cs:5-7` — 顶层注释明确说明
  - `Assets/Scripts/Bullet/FireExtension/FireExtension.cs:137-139` — 用了全限定名
  - `Assets/Scripts/Enemy/AI/MoveBehaviours/HomingMove.cs` — 本次会话踩坑后修复
- **预防**:写"引用玩家单例 / 玩家组件"的代码时,先在文件头部确认有没有 `using ShinySTG.Player;`,有就改全限定名。LLM 看到这个 namespace 名要警觉。

### 4.8 Ease 曲线作用于"剩余比例"不是"已走比例"

- **症状**:`PatrolMove`(以及旧版 `EaseMove`)配了 `InOutSine` 曲线后,敌人**完全不动**(t=2s 还在入场点)。
- **原因**:Ease 函数的语义是 "t=0 返回 0,t=1 返回 1,中段最大"。如果 `t = 1 - dist/initialDist`(已走比例),则起步时 `t=0`,`Ease(InOutSine, 0) = 0`,`speedFactor = 0`,step = 0 → **永远不动**。
- **正确写法**(以 `PatrolMove` 为例):让 `t = dist / initialDist`(**剩余距离比例**,1=远,0=到),并对 `Linear` 模式做特例 —— 返回常数 1(匀速),不要返回 `t`,否则变成"距离衰减 ODE"指数渐近永远到不了:
  ```csharp
  case EaseMode.Linear:    return 1f; // 匀速,无缓动
  case EaseMode.InOutSine: return InOutSine(t); // 起步 0.5,中段 1.0,收尾 0.5
  ```
- **新 `EaseMove`(子弹式「角度方向 + 时长」重构,2026-09 改)**:`t` 改为**剩余时间比例**(`1 - _elapsed/Duration`),不是距离比例 —— 概念一样(1=起步全速,0=到时停下),但来源不同:`EaseMove` 与敌人入场位置完全解耦,只关心方向与时长,资产跨场景可复用(详见 `Assets/Scripts/Enemy/AI/MoveBehaviours/EaseMove.cs` 注释)。
- **配套字段**:`EaseMove` / `PatrolMove` 必须有 `MinSpeedFactor` 字段,InOutSine / OutBack 曲线在 `tRemain=0`(收尾)时 `Ease(...) = 0`,需要 `MinSpeedFactor > 0` 才能在收尾段继续动(默认 0,Tooltip 提示用户调)。
- **预防**:写"距离/时间 → 速度因子"的代码时,先画一个数轴:t=1(起步)对应"全速"还是"零速"?**全速对应 t=1** 才符合"起步 = 快"的直觉。

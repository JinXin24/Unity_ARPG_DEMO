# Unity ARPG Demo — 编码规范

> **AI 必须在每次动手改文件前先读本文件，读到本条视为确认。违反时立即停止并说"违反了 CLAUDE.md 第 1 条"。**

## 优先使用已有代码库

写任何新代码前，先检查以下目录：

- `Assets/Scripts/JinXinFramework/` — 个人框架（Singleton 等）
- `Assets/Plugins/AI_Tools/` — AI 生成的编辑器工具（ExcelToSO、骨骼诊断、根骨骼清零）
- `Assets/Scripts/Character/` — InputSystemController、InputTest
- `Assets/Scripts/Combat/` — CombatStateMachine
- `Assets/Scripts/AI_Config/` — ConfigManager（运行时 SO 加载）
- `Assets/Scripts/Gen/` — ExcelToSO 生成的 C# 类
- `Assets/Scripts/Data/` — 手写的 SO 配置类

## 输入系统

- 使用旧版 Input Manager（`Input.GetKeyDown` / `Input.GetAxis`），不用 Input System Package
- 原因：新 Input System `.inputactions` 缓存频繁损坏，生成代码不稳定
- `InputSystemController : Singleton<InputSystemController>` 是输入入口
- 业务代码通过 `InputSystemController.Instance.GetAttackPressed()` 读输入
- 按键映射：
  - 攻击：鼠标左键
  - 移动：WASD（Horizontal/Vertical 轴）
  - 走/跑切换：左 Shift
  - 技能键：E
- `InputSystemController` 用旧 API 封装，外部接口不变，方便后续切换
- **ProjectSettings activeInputHandler 必须设为 2（Both）**，否则旧版 Input API 不可用

## 动画

- Animator 挂在 Unit 子物体上，代码通过 `[SerializeField] private Animator animator` 引用
- 禁止 `GetComponent<Animator>()`
- 状态切换用 `Animator.CrossFade()`，过渡固定 1 帧（`0.016f`），不用 `.Play()`
- AC 里每个 Attack 段是独立 State，不需要 Transition
- 所有帧数据基于 60fps 逻辑帧

## 配置表

- Excel 文件放 `Assets/Excel/`
- 表头三行：字段名 / 类型 / 中文注释
- 读表时列宽至少设 40 字符，避免截断
- 通过 `ExcelToSO` 导出（Tools → Excel 转 ScriptableObject，或右键 → 导出配置）
- 运行时通过 `ConfigManager.Instance` 加载
- ExcelToSO 依赖：Python 3 + openpyxl
- 数组类型：类型行写 `int[]` `float[]` `string[]`，值用分号分隔（`12;15;18;21;24`）
- bool 值：用 `TRUE` / `FALSE`（Excel 默认大写）
- **导出 List SO**：整张表打包进一个 `{表名}SOList.asset` 容器，元素用 `AddObjectToAsset` 挂为子资产
  - 好处：加新行不用重新拖 SO，引用不断
  - 流程：`生成 C# List 容器类`（仅首次）→ 编译 → 每次改表点 `导出 List SO`

## 状态机写法规范

### 第 1 步：配 Excel 表

先建状态机配置表，定义所有状态和过渡。表结构参照 `Assets/Excel/StateMachineConfig.xlsx`：

状态表 `state`，列定义如下：

| 列 | 类型 | 含义 | 例子 |
|------|------|------|------|
| `CharacterId` | int | 角色ID | 1001 |
| `UseCommon` | bool | 是否通用状态 | FALSE |
| `State_id` | int | 状态唯一ID，数字引用 | 1001 |
| `Info` | string | 中文说明 | 待机 |
| `Anm_name` | string | AC 里的动画名 | Stand |
| `On_anm_end` | int | 播完切哪个状态（空=循环） | 空 |
| `On_move` | float[] | 移动窗口：`[窗口末, 窗口始, 目标状态ID]` | `0.3;0.7;1002` |
| `On_Atk` | float[] | 攻击窗口：`[窗口末, 窗口始, 目标状态ID]` | `1;1;10021` |
| `On_Skill` | float[] | 技能窗口：`[窗口末, 窗口始, 目标状态ID]` | `1;1;20021` |
| `On_stop` | int | 停止移动时切回哪个状态 | 1001 |

状态之间通过 `State_id` 数字引用，不依赖字符串。

`On_move` 目前为 3 个值：`[窗口末, 窗口始, 目标StateId]`。移动由 1D Blend Tree（参数 `Speed`，范围 0~1）处理。FSM 只负责 Stand ↔ Move 切换，不管理方向。Speed 写入用 `Mathf.SmoothDamp` 平滑插值，速度变量跨帧保留。

### 第 2 步：设置 InputSystemController

确保场景中有 `InputSystemController` 单例，`.inputactions` 已生成 C# 类。FSM 通过 `InputSystemController.Instance` 读输入，不自己创建 Action。

### 第 3 步：实现 FSM 代码 ✅ 已完成

**移动系统**：
- AC 里只有一个 Move State，Entry 直连，内置 **1D Blend Tree**
- Blend Tree 参数 `Speed`（范围 0~1，0=待机、0.5=走、1=跑）
- 代码不给 Blend Tree 做 CrossFade，只通过 `SetFloat(Speed)` 驱动
- 参数写入用 `Mathf.SmoothDamp`，smoothTime = 0.2s，速度变量跨帧保留
- 输入: `GetMoveInput().magnitude` → 目标 Speed
- 移动→待机：输入归零 → SmoothDamp 拉 Speed 到 0 → Blend Tree 自然过渡到待机 pose
- **Shift 切换走/跑**：`GetSprintToggled()` 切换 `runMode`，走 `maxSpeed=0.5`，跑 `maxSpeed=1.0`

**旋转系统**（参照 `D:\computer\project\Demo_3D_RPG_\Assets\Script\Player\FSM.cs` 的 `DORotate()`）：
- `Mathf.Atan2(inputDir.x, inputDir.z) * Rad2Deg + 相机 Y 轴旋转` = 世界空间目标角度
- `Mathf.SmoothDampAngle` 平滑旋转，smoothTime = 0.025s
- **相机必须在角色身后**，否则方向会反。如果方向反了，调整 `inputDir` 的 XY 正负号
- 位置：[CharacterState.cs:63-79](Assets/Scripts/FSM/CharacterState.cs#L63-L79)

**状态表**（`StateMachineConfig.xlsx` → `state` sheet）：
- 目前只有一行：`StateId=1001, AnimName=Move`
- 1D Blend Tree 参数 `Speed`（0=待机, 0.5=走, 1=跑）
- 角色旋转：`DORotate()` 相机相对旋转（`SmoothDampAngle` 0.025s）
- `OnMove` = `[窗口末, 窗口始, 目标StateId]`，移动窗口检测
- `OnStop` = 回 Stand 的 StateId
- 状态通过 `State_id` 数字引用，不依赖字符串

**CharacterState 组件**：
- 挂 Unit 上，`[SerializeField]` 拖 Animator 和 `StateSOList`（List 容器）
- `PlayerState`：运行时数据（Id、Config、BeginTime）
- `stateData`：`Dictionary<int, PlayerState>`，用 StateId 做 key
- `ToNext(int stateId)`：从字典取出 → 当前状态 End 事件 → 切过去 → CrossFade（0.016f）→ 新状态 Begin 事件
- `AddListener(stateId, eventType, callback)` / `DOStateEvent(stateId, eventType)`：事件系统
- 事件类型：`Begin`（进入）、`Update`（每帧）、`End`（退出）、`OnAnmEnd`（动画播完）
- **技能虚函数**：`OnSkillTriggered(int targetStateId)` `protected virtual`，按 E 检测 `OnSkill` 窗口后调用

**AimisiCharacter（双形态子类）**：
- 双 Animator + 双模型（人类/机甲），共用根节点 CharacterController
- `SwitchToHuman()` / `SwitchToMech()`：切换 Animator + 模型显隐 + 碰撞体参数
- `FormCollider`：每形态的 height / radius / center，[SerializeField] 可调
- `OnSkillTriggered` 重写：按目标 StateId 首位判断（1=人类，2=机甲）自动切形态
- Inspector 有 `currentForm` 运行时标识方便观察

**强化E与双人同屏**：
- `OnEnhanceSkill` 列：float[]，格式 `[离场状态ID, 进场状态ID]`
- `UnlockEnhance` 列：bool，进入该状态时启动 4 秒强化期
- 流程：进入普攻4 → `OnStateBegin` → `enhanceTimeLeft = 4s` → 强化期内按E → `OnEnhanceSkillTriggered(离场, 进场)` → 双人同屏，离场动画播完自动关模型
- `CanUseEnhanceSkill()`：强化期内返回 true，屏蔽普通E技能
- 位置：[AimisiCharacter.cs](Assets/Scripts/FSM/AimisiCharacter.cs)

**输入**：
- `InputSystemController.Instance.GetMoveInput()` → Vector2
- `InputSystemController.Instance.GetAttackPressed()` → bool
- `InputSystemController.Instance.GetSkillPressed()` → bool（E 键）

**技能与形态切换**：
- `CharacterState.OnSkill()`：`protected virtual`，按 E 触发，子类可重写
- `AimisiCharacter.OnSkill()`：重写为切换人/机甲形态 + 碰撞体参数
- 位置：[CharacterState.cs](Assets/Scripts/FSM/CharacterState.cs) / [AimisiCharacter.cs](Assets/Scripts/FSM/AimisiCharacter.cs)

### 第 4 步：攻击系统

**Excel 配置**（`StateMachineConfig.xlsx` → `state` sheet）：
- `OnAtk` 列：`float[]`，格式 `[窗口末, 窗口始, 目标StateId]`
  - 例 `1;1;10021`：全程可取消，按攻击键切到 State `10021`
  - 在时间窗口内（`t ≤ config[0] || t ≥ config[1]`）检测到 `GetAttackPressed()` 时 → `ToNext(targetStateId)`
- 攻击链：`1001(Move) → 10021(Attack1) → 10022(Attack2) → 10023(Attack3) → 10024(Attack4) → 循环回 Attack1`
- 每个攻击段 `OnAnimEnd` = `1001`（播完自动回 Move）

**代码**（参照 Demo_3D_RPG_ 的 `OnAtk` / `CheckConfig`）：
- `CheckConfig(float[] config)`：归一化时间窗口检查
- `GetNormalizedTime()`：`animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f`
- `OnAtk()`：检测 `GetAttackPressed()` → 窗口内 → `ToNext(config[2])`
- 在 `Start()` 中遍历所有 `StateSO`，有 `OnAtk` 的自动注册 `Update` 事件监听
- 位置：[CharacterState.cs:68-93](Assets/Scripts/FSM/CharacterState.cs#L68-L93)

### 第 5 步：位移系统

**架构**：攻击位移用 ScriptableObject 配置（AnimationCurve），不用 Excel（Excel 存不了曲线）。每轴独立曲线。

| 文件 | 作用 |
|------|------|
| [StateMotionSO.cs](Assets/Scripts/Combat/StateMotionSO.cs) | SO 容器，`StateId` + `List<PhysicsConfig>` |
| `PhysicsConfig` | `triggerSec` / `endSec`（秒）、`force`（强度，非最终米数）、`curveX/Y/Z`（每轴独立速度曲线）、`ignoreGravity`、`stopDst` |

**曲线含义**：
- 横轴 = 时间进度（0~1），纵轴 = 速度倍率（1=全速，0=停）
- `force` 不是最终位移米数，是跟曲线配合调的强度值，需要肉眼校准
- 每轴独立曲线：Z 管前冲、Y 管跳跃、X 管侧移，互不干扰
- 默认曲线 `EaseOutCurve()`：`(0,1)→(1,0)` 先快后慢

**使用方式**：
1. Unit 上挂 `CharacterController`（代码 `GetComponent`）
2. `CharacterState` 拖入 `StateMotionSO`
3. Inspector 里给每个攻击状态配位移（右键 → Create → 配置 → 状态位移配置）
4. `triggerSec`/`endSec` 直接填秒数，看动画窗口的时间轴，不需要管帧率

**流程**（参照 Demo_3D_RPG_ PhysicsService）：
- `OnPhysicsBegin`：重置已执行标记、清空当前位移
- `OnPhysicsUpdate`：秒→归一化换算 → 检查 trigger → `velocity = force / duration` → 逐帧 `Vector3.Scale(velocity, (curveX, curveY, curveZ)) * dt` → `characterController.Move()`
- `OnPhysicsEnd`：清空当前位移
- `GetNormalizedTime()` 在 CrossFade 期间读 `GetNextAnimatorStateInfo` 避免拿到旧状态的时间
- 位置：[CharacterState.cs:142-203](Assets/Scripts/FSM/CharacterState.cs#L142-L203)

### 第 6 步：动画数据提取与坐标系陷阱

**坐标系映射**：
- DCC 工具（Blender/Maya）：**Z 朝上，Y 朝前**
- Unity：**Y 朝上，Z 朝前**
- 从 DCC 导出的 FBX 动画里，**Y 轴存的是角色的前后位移，不是在跳**。Unity 里看 Root.Y 的曲线以为是上下颠簸，实际是前进方向
- 部分模型有 **Armature Scale = 100** 的全局缩放，位置曲线值要 ×100 才是真实米数

**AI 提取位移数据**：
- 直接把 `.anim` 文件（Unity 导出 YAML 格式）丢给 AI 分析
- AI 能扫描 `m_PositionCurves` 段、找到 Root 骨骼路径、提取所有关键帧的 XYZ 值
- 结合 Scale 系数和坐标系映射，算出每帧真实位移量和速度变化
- 根据速度变化点自动生成 AnimationCurve 关键帧

**根骨骼位移清零**：
- `Apply Root Motion` 关了但动画曲线还在 = 角色会瞬移到动画位置再瞬移回来（闪烁 bug）
- 必须用 [RootBonePositionCleaner.cs](Assets/Plugins/AI_Tools/Editor/RootBonePositionCleaner.cs) 把根骨骼 Position 曲线归零
- 菜单：Tools → 根骨骼位移清零器 → 扫描 → 清零

参照 `D:\computer\project\Demo_3D_RPG_\Assets\Script\Player\FSM.cs` 的模式：

**数据结构**：
- `PlayerState` 类：存 Excel 配置行 + `StateEntity`（SO 配置）+ `SkillEntity` + `clipLength` + `begin`（进入时间）
- `Dictionary<int, PlayerState> stateData`：用 `State_id` 做 key 索引所有状态
- `currentState`：当前状态引用

**核心方法**：
- `ToNext(int stateId)`：从字典取出目标状态 → 当前状态 `OnEnd` → 切过去 → 新状态 `OnBegin`
- `CheckConfig(float[] config)`：归一化时间窗口检查 — `t <= config[0] || t >= config[1]`
- `AnimationOnPlayEnd()`：动画播完时调用，读 `on_anm_end` 决定下一个状态（-1=停，0=重新开始，其他=跳转）

**事件系统**（参照 Demo_3D_RPG_ 的 `AddListener` / `DOStateEvent`）：
- `StateEventType` 枚举：`begin` / `update` / `end` / `onAnmEnd`
- `AddListener(stateId, eventType, callback)`：给特定状态的特定事件注册回调
- `DOStateEvent(stateId, eventType)`：触发该状态该事件的所有回调
- 在 `StateInit()` 中根据配置表的字段（`on_move`、`on_atk`、`on_stop` 等）给对应的 `stateId` 注册对应的 `update` / `begin` / `end` 事件

**服务层**（参照 `FSMServiceBase`）：
- 每个服务（Animation / Physics / Hit / Effect）继承 `FSMServiceBase`，实现 `OnBegin` / `OnUpdate` / `OnEnd` / `OnAnimationEnd`
- FSM 在状态切换和 Update 时按顺序调用所有服务
- 如果状态中途被切换（`currentState.id` 变了），后续服务跳过

## 战斗系统

- 不使用 FSM 类层级（CharacterState 基类那套）
- 使用单一 MonoBehaviour（如 CombatStateMachine），内部管理段位
- 攻击取消窗口：代码读 `CancelOpen` 数组（60fps 基准），配合输入缓冲
- 攻击段循环：`(segment + 1) % Attacks.Length`

## 命名

- C# 类名：PascalCase
- 序列化字段：camelCase + `[SerializeField] private`
- 不要 `m_` 前缀、不要下划线前缀
- 中文注释解释业务，英文命名字段

## 禁止

- 不要引入 Luban 或第三方 FSM 框架
- 不要在 Update 里用 `GetComponent`
- 不要用 `animator.SetTrigger/SetBool` 做状态切换——用 `CrossFade`
- 输入统一走 `InputSystemController`，不要在各处分散读键
- **禁止未经用户明确同意修改环境配置**：`manifest.json`、`ProjectSettings`、`Package Manager`、`.inputactions`、`activeInputHandler` 等——只能提建议，不能动手改

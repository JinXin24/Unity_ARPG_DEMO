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

- 使用 Input System Package（`com.unity.inputsystem`）
- 版本由用户在 Package Manager 中管理，**AI 禁止修改 `manifest.json` 中的 inputsystem 版本**
- `.inputactions` 资源 → 生成 C# 包装类
- `InputSystemController : Singleton<InputSystemController>` 是输入入口
- 业务代码通过 `InputSystemController.Instance.GetAttackPressed()` 读输入
- 使用 `WasPressedThisFrame()` 而非 `performed` 回调
- 禁止用旧 Input API（`Input.GetKeyDown` 等）
- 禁止手写 `new InputAction()`

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
- 挂 Unit 上，`[SerializeField]` 拖 Animator 和 StateSO[]
- `PlayerState`：运行时数据（Id、Config、BeginTime）
- `stateData`：`Dictionary<int, PlayerState>`，用 StateId 做 key
- `ToNext(int stateId)`：从字典取出 → 当前状态 End 事件 → 切过去 → CrossFade（0.016f）→ 新状态 Begin 事件
- `AddListener(stateId, eventType, callback)` / `DOStateEvent(stateId, eventType)`：事件系统
- 事件类型：`Begin`（进入）、`Update`（每帧）、`End`（退出）、`OnAnmEnd`（动画播完）

**输入**：
- `InputSystemController.Instance.GetMoveInput()` → Vector2
- `InputSystemController.Instance.GetAttackPressed()` → bool

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

**架构**：攻击位移用 ScriptableObject 配置（AnimationCurve），不用 Excel（Excel 存不了曲线）。

| 文件 | 作用 |
|------|------|
| [StateMotionSO.cs](Assets/Scripts/Combat/StateMotionSO.cs) | SO 容器，`List<StateMotionData>`，每行含 `StateId` + `List<PhysicsConfig>` |
| `PhysicsConfig` | `trigger` / `time` / `force` / `curve` / `ignoreGravity` / `stopDst` |

**使用方式**：
1. Unit 上挂 `CharacterController`（代码会 `GetComponent`）
2. `CharacterState` 拖入 `StateMotionSO`
3. Inspector 里给每个攻击状态配位移曲线（右键 → Create → 配置 → 状态位移配置）

**流程**（参照 Demo_3D_RPG_ PhysicsService）：
- `OnPhysicsBegin`：重置已执行标记、清空当前位移
- `OnPhysicsUpdate`：检查 trigger → 计算 `velocity = force / duration` → 每帧 `curve.Evaluate(progress)` 加权 → `characterController.Move()`
- `OnPhysicsEnd`：清空当前位移
- 位置：[CharacterState.cs:142-203](Assets/Scripts/FSM/CharacterState.cs#L142-L203)

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
- 不要手写 `new InputAction()`
- 不要用 `animator.SetTrigger/SetBool` 做状态切换——用 `CrossFade`

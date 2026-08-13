using UnityEngine;

/// <summary>
/// 状态机服务基类。由 CharacterState 在状态切换 / 每帧统一调度，
/// 各服务实现自己的 OnBegin / OnUpdate / OnEnd 处理业务（位移 / 武器显隐 / 特效 / 命中…）。
/// 优点：业务从 CharacterState 剥离开，顺序可控、中断统一清理、可单独复用替换。
/// </summary>
public abstract class FSMServiceBase
{
    protected CharacterState Owner { get; private set; }

    /// <summary>由 CharacterState 注入持有者引用</summary>
    public void SetOwner(CharacterState owner) => Owner = owner;

    /// <summary>初始化配置（Start 时调用，构建字典等）</summary>
    public virtual void Init() { }

    /// <summary>状态进入时（新状态）</summary>
    public virtual void OnBegin() { }

    /// <summary>每帧（仅当前状态）</summary>
    public virtual void OnUpdate() { }

    /// <summary>状态退出 / 中断时（旧状态）</summary>
    public virtual void OnEnd() { }
}

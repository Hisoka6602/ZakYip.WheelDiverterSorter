namespace ZakYip.WheelDiverterSorter.Host.StateMachine;

/// <summary>
/// 系统状态管理器接口
/// </summary>
/// <remarks>
/// 负责管理系统状态转换，提供状态查询和状态切换API。
/// 所有状态转移都会进行合法性校验，非法转移会抛出异常并给出中文提示。
/// </remarks>
public interface ISystemStateManager
{
    /// <summary>
    /// 获取当前系统状态
    /// </summary>
    SystemState CurrentState { get; }

    /// <summary>
    /// 尝试切换系统状态
    /// </summary>
    /// <param name="targetState">目标状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果，包含是否成功和错误消息</returns>
    /// <remarks>
    /// <para>状态转移规则：</para>
    /// <list type="bullet">
    /// <item>Booting → Ready（启动完成）</item>
    /// <item>Ready → Running（启动系统）</item>
    /// <item>Running → Paused（暂停系统）</item>
    /// <item>Paused → Running（恢复运行）</item>
    /// <item>Running/Paused/Ready → Ready（停止系统）</item>
    /// <item>任何状态 → EmergencyStop（急停）</item>
    /// <item>EmergencyStop → Ready（急停解除）</item>
    /// <item>任何状态 → Faulted（故障发生）</item>
    /// <item>Faulted → Ready（故障恢复）</item>
    /// </list>
    /// <para>非法转移会返回失败结果并包含中文错误消息。</para>
    /// </remarks>
    Task<StateChangeResult> ChangeStateAsync(SystemState targetState, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断是否允许从当前状态转换到目标状态
    /// </summary>
    /// <param name="targetState">目标状态</param>
    /// <returns>是否允许转换</returns>
    bool CanTransitionTo(SystemState targetState);

    /// <summary>
    /// 获取状态机的状态转移历史
    /// </summary>
    /// <param name="count">获取最近的转移记录数量</param>
    /// <returns>状态转移历史记录</returns>
    IReadOnlyList<StateTransitionRecord> GetTransitionHistory(int count = 10);
}

/// <summary>
/// 状态切换结果
/// </summary>
public class StateChangeResult
{
    /// <summary>操作是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误消息（中文）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>切换前的状态</summary>
    public SystemState? PreviousState { get; init; }

    /// <summary>切换后的状态</summary>
    public SystemState CurrentState { get; init; }

    /// <summary>创建成功结果</summary>
    public static StateChangeResult CreateSuccess(SystemState previousState, SystemState currentState)
    {
        return new StateChangeResult
        {
            Success = true,
            PreviousState = previousState,
            CurrentState = currentState
        };
    }

    /// <summary>创建失败结果</summary>
    public static StateChangeResult CreateFailure(string errorMessage, SystemState currentState)
    {
        return new StateChangeResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            CurrentState = currentState
        };
    }
}

/// <summary>
/// 状态转移记录
/// </summary>
public class StateTransitionRecord
{
    /// <summary>转移前的状态</summary>
    public SystemState FromState { get; init; }

    /// <summary>转移后的状态</summary>
    public SystemState ToState { get; init; }

    /// <summary>转移时间</summary>
    public DateTimeOffset TransitionTime { get; init; }

    /// <summary>转移是否成功</summary>
    public bool Success { get; init; }

    /// <summary>失败原因（如果失败）</summary>
    public string? FailureReason { get; init; }
}

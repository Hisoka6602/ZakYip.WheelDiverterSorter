using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 摆轮命令模型 - 统一硬件能力层（HAL）
/// Wheel Command Model - Unified Hardware Abstraction Layer (HAL)
/// </summary>
/// <remarks>
/// <para>定义发送给摆轮设备的命令参数。</para>
/// <para>此命令只包含"让摆轮做什么"，不包含拓扑信息（如包裹ID、格口号等）。</para>
/// </remarks>
public readonly record struct WheelCommand
{
    /// <summary>
    /// 目标摆轮方向
    /// </summary>
    public required Enums.Hardware.DiverterDirection Direction { get; init; }

    /// <summary>
    /// 命令超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// 摆轮设备接口 - 统一硬件能力层（HAL）
/// Wheel Diverter Device Interface - Unified Hardware Abstraction Layer (HAL)
/// </summary>
/// <remarks>
/// <para>表示单个摆轮设备的控制能力。</para>
/// <para>此接口只描述"设备能做什么"（能力），不包含拓扑信息（如格口号、小车号等）。</para>
/// <para>厂商驱动需实现此接口以提供摆轮控制能力。</para>
/// </remarks>
public interface IWheelDiverterDevice
{
    /// <summary>
    /// 设备唯一标识符
    /// </summary>
    /// <remarks>
    /// 设备级别的标识，如 "D001", "WHEEL_1" 等。
    /// </remarks>
    string DeviceId { get; }

    /// <summary>
    /// 执行摆轮命令
    /// </summary>
    /// <param name="command">摆轮命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>
    /// 操作结果：
    /// <list type="bullet">
    /// <item>成功时 IsSuccess = true</item>
    /// <item>失败时包含错误码和错误消息</item>
    /// </list>
    /// </returns>
    Task<OperationResult> ExecuteAsync(
        WheelCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止摆轮当前动作
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<OperationResult> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取摆轮当前状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>摆轮状态</returns>
    Task<WheelDiverterState> GetStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 摆轮设备状态
/// </summary>
public enum WheelDiverterState
{
    /// <summary>未知状态</summary>
    Unknown = 0,
    
    /// <summary>空闲/就绪</summary>
    Idle = 1,
    
    /// <summary>正在执行动作</summary>
    Executing = 2,
    
    /// <summary>处于左转位置</summary>
    AtLeft = 3,
    
    /// <summary>处于右转位置</summary>
    AtRight = 4,
    
    /// <summary>处于直通位置</summary>
    AtStraight = 5,
    
    /// <summary>故障状态</summary>
    Fault = 99
}

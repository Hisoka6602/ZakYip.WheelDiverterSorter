using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 摆轮命令模型 - 统一硬件能力层（HAL）
/// Wheel Command Model - Unified Hardware Abstraction Layer (HAL)
/// </summary>
/// <remarks>
/// <para>定义发送给摆轮设备的命令参数。</para>
/// <para>此命令只包含"让摆轮做什么"（方向和超时），不包含拓扑信息（如包裹ID、格口号等）。</para>
/// <para>使用示例：</para>
/// <code>
/// var command = new WheelCommand
/// {
///     Direction = DiverterDirection.Left,
///     Timeout = TimeSpan.FromSeconds(5)
/// };
/// var result = await device.ExecuteAsync(command, cancellationToken);
/// </code>
/// </remarks>
public readonly record struct WheelCommand
{
    /// <summary>
    /// 目标摆轮方向
    /// </summary>
    /// <remarks>
    /// 指定摆轮执行动作后的目标位置：
    /// <list type="bullet">
    /// <item><see cref="Enums.Hardware.DiverterDirection.Left"/> - 左转</item>
    /// <item><see cref="Enums.Hardware.DiverterDirection.Right"/> - 右转</item>
    /// <item><see cref="Enums.Hardware.DiverterDirection.Straight"/> - 直通</item>
    /// </list>
    /// </remarks>
    public required Enums.Hardware.DiverterDirection Direction { get; init; }

    /// <summary>
    /// 命令超时时间（可选）
    /// </summary>
    /// <remarks>
    /// <para>如果设置，当命令执行时间超过此值时，操作将返回超时错误。</para>
    /// <para>如果未设置（null），将使用设备默认超时时间。</para>
    /// </remarks>
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

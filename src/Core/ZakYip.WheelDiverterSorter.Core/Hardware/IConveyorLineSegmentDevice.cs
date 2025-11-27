using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 线体段设备接口 - 统一硬件能力层（HAL）
/// Conveyor Line Segment Device Interface - Unified Hardware Abstraction Layer (HAL)
/// </summary>
/// <remarks>
/// <para>表示单个线体段（皮带、滚筒等）的控制能力。</para>
/// <para>此接口只描述"设备能做什么"（能力），不包含拓扑信息（如哪段线体连接哪个摆轮等）。</para>
/// <para>厂商驱动需实现此接口以提供线体段控制能力。</para>
/// </remarks>
public interface IConveyorLineSegmentDevice
{
    /// <summary>
    /// 线体段唯一标识符
    /// </summary>
    /// <remarks>
    /// 设备级别的标识，如 "SEG001", "BELT_1" 等。
    /// </remarks>
    string SegmentId { get; }

    /// <summary>
    /// 设置线体段运行速度
    /// </summary>
    /// <param name="speedMmPerSec">速度值（毫米/秒），0表示停止</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<OperationResult> SetSpeedAsync(
        decimal speedMmPerSec,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止线体段运行
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<OperationResult> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动线体段运行（使用默认或上次设置的速度）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<OperationResult> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取线体段当前状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前运行状态</returns>
    Task<ConveyorSegmentState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前运行速度
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前速度值（毫米/秒）</returns>
    Task<decimal> GetCurrentSpeedAsync(CancellationToken cancellationToken = default);
}

using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Execution.Abstractions;

/// <summary>
/// 传感器事件提供者接口
/// </summary>
/// <remarks>
/// 抽象了传感器层的事件订阅，隐藏底层传感器实现细节（模拟/真实硬件）。
/// 
/// <para><b>职责</b>：</para>
/// <list type="bullet">
///   <item>提供包裹检测事件订阅</item>
///   <item>提供重复触发异常事件订阅</item>
///   <item>启动/停止传感器监听</item>
/// </list>
/// 
/// <para><b>实现层</b>：</para>
/// Ingress 项目实现此接口，内部使用 IParcelDetectionService 等具体实现。
/// </remarks>
public interface ISensorEventProvider
{
    /// <summary>
    /// 包裹检测事件
    /// </summary>
    /// <remarks>
    /// 当传感器检测到包裹到达时触发此事件。
    /// </remarks>
    event EventHandler<ParcelDetectedArgs>? ParcelDetected;

    /// <summary>
    /// 重复触发异常事件
    /// </summary>
    /// <remarks>
    /// 当检测到同一传感器在短时间内重复触发时触发此事件。
    /// </remarks>
    event EventHandler<DuplicateTriggerArgs>? DuplicateTriggerDetected;

    /// <summary>
    /// 启动传感器事件监听
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止传感器事件监听
    /// </summary>
    /// <returns>异步任务</returns>
    Task StopAsync();
}

/// <summary>
/// 包裹检测事件参数
/// </summary>
/// <remarks>
/// 纯数据对象，不依赖具体的 Ingress 层类型。
/// </remarks>
public record ParcelDetectedArgs
{
    /// <summary>
    /// 生成的唯一包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 触发检测的传感器ID
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType SensorType { get; init; }
}

/// <summary>
/// 重复触发异常事件参数
/// </summary>
/// <remarks>
/// 纯数据对象，不依赖具体的 Ingress 层类型。
/// </remarks>
public record DuplicateTriggerArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 触发检测的传感器ID
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType SensorType { get; init; }

    /// <summary>
    /// 距离上次触发的时间间隔（毫秒）
    /// </summary>
    public required double TimeSinceLastTriggerMs { get; init; }

    /// <summary>
    /// 异常原因
    /// </summary>
    public required string Reason { get; init; }
}

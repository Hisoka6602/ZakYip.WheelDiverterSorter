using ZakYip.WheelDiverterSorter.Core.Events.Sensor;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;

/// <summary>
/// 传感器事件提供者接口
/// </summary>
/// <remarks>
/// 本接口属于 Core 层，定义传感器事件订阅的抽象契约。
/// 抽象了传感器层的事件订阅，隐藏底层传感器实现细节（模拟/真实硬件）。
/// 
/// <para><b>职责</b>：</para>
/// <list type="bullet">
///   <item>提供包裹检测事件订阅</item>
///   <item>提供重复触发异常事件订阅</item>
///   <item>提供落格传感器事件订阅</item>
///   <item>启动/停止传感器监听</item>
/// </list>
/// 
/// <para><b>实现层</b>：</para>
/// Ingress 项目实现此接口，内部使用 IParcelDetectionService 等具体实现。
/// 
/// <para><b>事件类型说明</b>：</para>
/// 使用 Core.Events.Sensor 中定义的统一事件参数类型，避免重复定义。
/// </remarks>
public interface ISensorEventProvider
{
    /// <summary>
    /// 包裹检测事件
    /// </summary>
    /// <remarks>
    /// 当传感器检测到包裹到达时触发此事件。
    /// 使用 <see cref="ParcelDetectedEventArgs"/> 作为事件参数。
    /// </remarks>
    event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

    /// <summary>
    /// 重复触发异常事件
    /// </summary>
    /// <remarks>
    /// 当检测到同一传感器在短时间内重复触发时触发此事件。
    /// 使用 <see cref="DuplicateTriggerEventArgs"/> 作为事件参数。
    /// </remarks>
    event EventHandler<DuplicateTriggerEventArgs>? DuplicateTriggerDetected;

    /// <summary>
    /// 落格传感器检测事件
    /// </summary>
    /// <remarks>
    /// 当落格传感器检测到包裹落入格口时触发此事件。
    /// 用于 OnSensorTrigger 模式下的落格完成通知。
    /// 使用 <see cref="ChuteDropoffDetectedEventArgs"/> 作为事件参数。
    /// </remarks>
    event EventHandler<ChuteDropoffDetectedEventArgs>? ChuteDropoffDetected;

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

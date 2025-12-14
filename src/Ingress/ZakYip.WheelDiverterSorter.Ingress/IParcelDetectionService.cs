using ZakYip.WheelDiverterSorter.Core.Events.Sensor;

namespace ZakYip.WheelDiverterSorter.Ingress;

/// <summary>
/// 包裹检测服务接口
/// </summary>
/// <remarks>
/// 负责监听传感器事件并检测包裹到达和落格
/// </remarks>
public interface IParcelDetectionService
{
    /// <summary>
    /// 包裹检测事件
    /// </summary>
    event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

    /// <summary>
    /// 重复触发异常事件
    /// </summary>
    event EventHandler<DuplicateTriggerEventArgs>? DuplicateTriggerDetected;

    /// <summary>
    /// 落格传感器检测事件
    /// </summary>
    /// <remarks>
    /// 当落格传感器检测到包裹落入格口时触发此事件
    /// </remarks>
    event EventHandler<ChuteDropoffDetectedEventArgs>? ChuteDropoffDetected;

    /// <summary>
    /// 启动包裹检测服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止包裹检测服务
    /// </summary>
    Task StopAsync();
}

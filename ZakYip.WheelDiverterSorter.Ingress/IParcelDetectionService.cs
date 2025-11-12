using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.Ingress;

/// <summary>
/// 包裹检测服务接口
/// </summary>
/// <remarks>
/// 负责监听传感器事件并检测包裹到达
/// </remarks>
public interface IParcelDetectionService
{
    /// <summary>
    /// 包裹检测事件
    /// </summary>
    event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

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

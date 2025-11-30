using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.Ingress.Services;

/// <summary>
/// 传感器健康监控服务接口
/// </summary>
/// <remarks>
/// 负责监控传感器的健康状态，检测故障并触发告警
/// </remarks>
public interface ISensorHealthMonitor
{
    /// <summary>
    /// 传感器故障事件
    /// </summary>
    event EventHandler<SensorFaultEventArgs>? SensorFault;

    /// <summary>
    /// 传感器恢复事件
    /// </summary>
    event EventHandler<SensorRecoveryEventArgs>? SensorRecovery;

    /// <summary>
    /// 启动健康监控
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止健康监控
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 获取传感器健康状态
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <returns>健康状态</returns>
    SensorHealthStatus GetHealthStatus(string sensorId);

    /// <summary>
    /// 获取所有传感器的健康状态
    /// </summary>
    /// <returns>健康状态字典</returns>
    IDictionary<string, SensorHealthStatus> GetAllHealthStatus();

    /// <summary>
    /// 手动报告传感器错误
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="error">错误信息</param>
    void ReportError(string sensorId, string error);
}

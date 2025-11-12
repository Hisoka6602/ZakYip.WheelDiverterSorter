using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.Ingress;

/// <summary>
/// 传感器接口
/// </summary>
/// <remarks>
/// 定义传感器的基本行为，包括启动、停止和事件触发
/// </remarks>
public interface ISensor : IDisposable
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    string SensorId { get; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    SensorType Type { get; }

    /// <summary>
    /// 传感器是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 传感器事件触发时发生
    /// </summary>
    event EventHandler<SensorEvent>? SensorTriggered;

    /// <summary>
    /// 启动传感器监听
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止传感器监听
    /// </summary>
    Task StopAsync();
}

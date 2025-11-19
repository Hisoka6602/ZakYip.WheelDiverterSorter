namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 主线传送带驱动控制器接口
/// Main Conveyor Drive Controller Interface - Controls conveyor start/stop and speed
/// </summary>
/// <remarks>
/// 此接口定义传送带的启停与变速控制。
/// 厂商驱动需实现此接口以提供传送带控制能力。
/// </remarks>
public interface IConveyorDriveController
{
    /// <summary>
    /// 传送带段唯一标识符
    /// </summary>
    string SegmentId { get; }

    /// <summary>
    /// 启动传送带
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止传送带
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置传送带速度（单位：毫米/秒）
    /// </summary>
    /// <param name="speedMmPerSec">速度值（毫米/秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> SetSpeedAsync(int speedMmPerSec, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前传送带速度（单位：毫米/秒）
    /// </summary>
    /// <returns>当前速度值</returns>
    Task<int> GetCurrentSpeedAsync();

    /// <summary>
    /// 获取传送带运行状态
    /// </summary>
    /// <returns>是否正在运行</returns>
    Task<bool> IsRunningAsync();
}

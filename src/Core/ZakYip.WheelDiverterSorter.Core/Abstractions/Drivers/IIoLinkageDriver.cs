using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

/// <summary>
/// IO 联动驱动接口
/// </summary>
/// <remarks>
/// 本接口属于 Core 层，定义 IO 联动控制的抽象契约。
/// 用于控制中段皮带等设备的 IO 联动，由 Drivers 层实现。
/// </remarks>
public interface IIoLinkageDriver
{
    /// <summary>
    /// 设置指定 IO 点的电平状态。
    /// </summary>
    /// <param name="ioPoint">IO 联动点配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量设置多个 IO 点的电平状态。
    /// </summary>
    /// <param name="ioPoints">IO 联动点配置列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task SetIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取指定 IO 点的当前电平状态。
    /// </summary>
    /// <param name="bitNumber">IO 端口编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前电平状态（true=高电平，false=低电平）</returns>
    Task<bool> ReadIoPointAsync(int bitNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// 复位所有 IO 联动点到默认状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task ResetAllIoPointsAsync(CancellationToken cancellationToken = default);
}

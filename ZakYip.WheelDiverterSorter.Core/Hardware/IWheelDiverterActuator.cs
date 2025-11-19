namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 摆轮执行器接口，提供摆轮控制的核心能力
/// Wheel Diverter Actuator Interface - Core capabilities for diverter control
/// </summary>
/// <remarks>
/// 此接口定义摆轮的基本控制操作（左转/右转/直行）。
/// 是厂商驱动实现的标准契约，上层执行层只依赖此抽象。
/// </remarks>
public interface IWheelDiverterActuator
{
    /// <summary>
    /// 摆轮唯一标识符
    /// </summary>
    string DiverterId { get; }

    /// <summary>
    /// 控制摆轮向左转向
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 控制摆轮向右转向
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> TurnRightAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 控制摆轮保持直通状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> PassThroughAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止摆轮当前动作
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取摆轮当前状态
    /// </summary>
    /// <returns>摆轮状态描述</returns>
    Task<string> GetStatusAsync();
}

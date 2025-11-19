namespace ZakYip.WheelDiverterSorter.Drivers.Abstractions;

/// <summary>
/// 摆轮驱动器接口，提供高层语义化操作，不暴露硬件细节
/// </summary>
/// <remarks>
/// 此接口封装了摆轮的所有操作，隐藏底层Y点编号、继电器通道等硬件实现细节。
/// 执行层（Execution）和核心层（Core）应仅依赖此接口，不直接操作具体硬件。
/// </remarks>
public interface IWheelDiverterDriver
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
    /// <remarks>
    /// 将摆轮转向左侧，具体的角度和动作由驱动实现内部决定。
    /// </remarks>
    Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 控制摆轮向右转向
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    /// <remarks>
    /// 将摆轮转向右侧，具体的角度和动作由驱动实现内部决定。
    /// </remarks>
    Task<bool> TurnRightAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 控制摆轮保持直通状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    /// <remarks>
    /// 将摆轮设置为直通（不转向）状态，让包裹直行通过。
    /// </remarks>
    Task<bool> PassThroughAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止摆轮当前动作
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    /// <remarks>
    /// 立即停止摆轮的任何运动，用于紧急情况或系统停止时。
    /// </remarks>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取摆轮当前状态
    /// </summary>
    /// <returns>摆轮状态描述</returns>
    /// <remarks>
    /// 返回摆轮当前状态的文本描述，用于监控和诊断。
    /// </remarks>
    Task<string> GetStatusAsync();
}

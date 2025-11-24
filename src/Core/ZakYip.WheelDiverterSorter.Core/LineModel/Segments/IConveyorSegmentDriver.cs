using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Segments;

/// <summary>
/// 中段皮带段驱动接口。
/// 定义对单个中段皮带段的底层 IO 控制能力。
/// </summary>
public interface IConveyorSegmentDriver
{
    /// <summary>
    /// IO 映射配置
    /// </summary>
    ConveyorIoMapping Mapping { get; }

    /// <summary>
    /// 写入启动信号
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task WriteStartSignalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 写入停止信号
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task WriteStopSignalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取故障输入状态（如果配置了故障输入点位）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果检测到故障返回 true，否则返回 false</returns>
    Task<bool> ReadFaultStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取运行反馈状态（如果配置了运行反馈点位）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果皮带正在运行返回 true，否则返回 false</returns>
    Task<bool> ReadRunningStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 复位驱动状态（清除所有输出信号）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task ResetAsync(CancellationToken cancellationToken = default);
}

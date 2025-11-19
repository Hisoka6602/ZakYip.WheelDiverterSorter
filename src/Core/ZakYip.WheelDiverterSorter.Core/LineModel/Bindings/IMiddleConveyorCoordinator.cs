using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 中段皮带多段联动协调器接口。
/// 负责协调多个中段皮带段的启停，支持顺序控制和策略配置。
/// </summary>
public interface IMiddleConveyorCoordinator
{
    /// <summary>
    /// 获取所有管理的皮带段
    /// </summary>
    IReadOnlyList<IConveyorSegment> Segments { get; }

    /// <summary>
    /// 启动所有皮带段
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    ValueTask<ConveyorOperationResult> StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止所有皮带段
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    ValueTask<ConveyorOperationResult> StopAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 先停下游、再停上游的有序停机
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    ValueTask<ConveyorOperationResult> StopDownstreamFirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有任何皮带段处于故障状态
    /// </summary>
    /// <returns>如果有故障返回 true，否则返回 false</returns>
    bool HasAnyFault();
}

using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Execution.Events;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 路径执行失败处理器接口
/// </summary>
/// <remarks>
/// 负责处理路径执行失败的情况，包括：
/// 1. 记录失败原因和位置
/// 2. 计算备用路径（通常是到异常格口的路径）
/// 3. 触发相关事件通知
/// </remarks>
public interface IPathFailureHandler
{
    /// <summary>
    /// 路径段执行失败事件
    /// </summary>
    event EventHandler<PathSegmentExecutionFailedEventArgs>? SegmentExecutionFailed;

    /// <summary>
    /// 路径执行失败事件
    /// </summary>
    event EventHandler<PathExecutionFailedEventArgs>? PathExecutionFailed;

    /// <summary>
    /// 路径切换事件
    /// </summary>
    event EventHandler<PathSwitchedEventArgs>? PathSwitched;

    /// <summary>
    /// 处理路径段执行失败
    /// </summary>
    /// <param name="parcelId">包裹标识</param>
    /// <param name="originalPath">原始路径</param>
    /// <param name="failedSegment">失败的路径段</param>
    /// <param name="failureReason">失败原因</param>
    void HandleSegmentFailure(
        long parcelId,
        SwitchingPath originalPath,
        SwitchingPathSegment failedSegment,
        string failureReason);

    /// <summary>
    /// 处理路径执行失败
    /// </summary>
    /// <param name="parcelId">包裹标识</param>
    /// <param name="originalPath">原始路径</param>
    /// <param name="failureReason">失败原因</param>
    /// <param name="failedSegment">失败的路径段（如果适用）</param>
    void HandlePathFailure(
        long parcelId,
        SwitchingPath originalPath,
        string failureReason,
        SwitchingPathSegment? failedSegment = null);

    /// <summary>
    /// 计算备用路径（到异常格口）
    /// </summary>
    /// <param name="originalPath">原始路径</param>
    /// <returns>备用路径，如果无法生成则返回null</returns>
    SwitchingPath? CalculateBackupPath(SwitchingPath originalPath);
}

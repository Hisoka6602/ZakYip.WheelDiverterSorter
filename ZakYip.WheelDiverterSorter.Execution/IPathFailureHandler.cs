using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Execution.Events;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 路径执行失败处理器接口
/// </summary>
/// <remarks>
/// 负责监控和通知路径执行失败的情况，包括：
/// 1. 记录失败原因和位置
/// 2. 计算备用路径（通常是到异常格口的路径）用于信息通知
/// 3. 触发相关事件通知
/// 
/// 重要说明：此接口仅提供监控和事件通知功能，不会自动执行备用路径。
/// 如需自动故障转移，外部系统需要订阅事件并实现相应的恢复逻辑。
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
    /// 备用路径已计算事件（注意：此事件仅表示备用路径已被计算，不代表路径已切换或执行）
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
    /// <remarks>
    /// 此方法仅计算备用路径用于信息通知，不会自动执行该路径。
    /// 如需执行备用路径，调用方需要显式调用路径执行器。
    /// </remarks>
    SwitchingPath? CalculateBackupPath(SwitchingPath originalPath);
}

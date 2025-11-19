namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 异常趋势检测器接口 / Anomaly Trend Detector Interface
/// 监控业务指标，检测异常趋势并触发告警
/// Monitors business metrics, detects anomaly trends and triggers alerts
/// </summary>
public interface IAnomalyDetector
{
    /// <summary>
    /// 记录分拣结果（用于统计异常格口比例）
    /// Record sorting result (for exception chute ratio statistics)
    /// </summary>
    /// <param name="targetChuteId">目标格口ID / Target chute ID</param>
    /// <param name="isExceptionChute">是否为异常格口 / Whether it's an exception chute</param>
    void RecordSortingResult(string targetChuteId, bool isExceptionChute);

    /// <summary>
    /// 记录超载事件（用于统计超载原因分布）
    /// Record overload event (for overload reason distribution statistics)
    /// </summary>
    /// <param name="reason">超载原因 / Overload reason</param>
    void RecordOverload(string reason);

    /// <summary>
    /// 记录上游超时事件
    /// Record upstream timeout event
    /// </summary>
    void RecordUpstreamTimeout();

    /// <summary>
    /// 检测异常趋势并触发告警（周期性调用）
    /// Detect anomaly trends and trigger alerts (periodic call)
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>异步任务 / Async task</returns>
    Task CheckAnomalyTrendsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置统计数据（可选，用于测试或周期性重置）
    /// Reset statistics (optional, for testing or periodic reset)
    /// </summary>
    void ResetStatistics();
}

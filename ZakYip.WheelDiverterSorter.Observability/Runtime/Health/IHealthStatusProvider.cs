namespace ZakYip.WheelDiverterSorter.Observability.Runtime.Health;

/// <summary>
/// 健康状态提供器接口 / Health Status Provider Interface
/// 负责收集和聚合 Execution、Drivers、Ingress 等模块的健康状态
/// Responsible for collecting and aggregating health status from Execution, Drivers, Ingress and other modules
/// </summary>
public interface IHealthStatusProvider
{
    /// <summary>
    /// 获取线体健康快照 / Get line health snapshot
    /// 聚合各模块状态，返回完整的健康数据
    /// Aggregates status from all modules and returns complete health data
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>线体健康快照 / Line health snapshot</returns>
    Task<LineHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default);
}

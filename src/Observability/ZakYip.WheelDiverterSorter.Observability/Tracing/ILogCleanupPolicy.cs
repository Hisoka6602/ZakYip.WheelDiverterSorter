namespace ZakYip.WheelDiverterSorter.Observability.Tracing;

/// <summary>
/// 日志清理策略接口
/// </summary>
/// <remarks>
/// 定义日志文件的清理策略，防止日志占满磁盘。
/// </remarks>
public interface ILogCleanupPolicy
{
    /// <summary>
    /// 执行日志清理
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

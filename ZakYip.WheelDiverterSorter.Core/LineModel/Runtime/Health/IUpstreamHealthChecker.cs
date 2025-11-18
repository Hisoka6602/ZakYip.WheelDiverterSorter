using ZakYip.WheelDiverterSorter.Core.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Core.Runtime.Health;

/// <summary>
/// 上游系统健康检查接口
/// </summary>
/// <remarks>
/// 为上游系统（RuleEngine、API通道等）提供连通性检查能力。
/// </remarks>
public interface IUpstreamHealthChecker
{
    /// <summary>
    /// 上游端点名称
    /// </summary>
    string EndpointName { get; }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上游健康状态</returns>
    /// <remarks>
    /// 执行轻量级连通性测试，如ping、握手等。
    /// 超时或连接失败时提供清晰的错误消息。
    /// </remarks>
    Task<UpstreamHealthStatus> CheckAsync(CancellationToken cancellationToken = default);
}

using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Health;

/// <summary>
/// 上游路由健康检查实现
/// </summary>
/// <remarks>
/// PR-U1: 使用 IUpstreamRoutingClient 替代 IRuleEngineClient
/// </remarks>
public class RuleEngineUpstreamHealthChecker : IUpstreamHealthChecker
{
    private readonly IUpstreamRoutingClient? _client;
    private readonly ILogger<RuleEngineUpstreamHealthChecker> _logger;
    private readonly ISystemClock _systemClock;
    private readonly string _connectionType;

    public RuleEngineUpstreamHealthChecker(
        IUpstreamRoutingClient? client,
        string connectionType,
        ILogger<RuleEngineUpstreamHealthChecker> logger,
        ISystemClock systemClock)
    {
        _client = client;
        _connectionType = connectionType;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <inheritdoc/>
    public string EndpointName => $"RuleEngine-{_connectionType}";

    /// <inheritdoc/>
    public async Task<UpstreamHealthStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        try
        {
            _logger.LogInformation("开始检查上游连接: {EndpointName}", EndpointName);

            if (_client == null)
            {
                _logger.LogWarning("上游连接未配置: {EndpointName}", EndpointName);
                return new UpstreamHealthStatus
                {
                    EndpointName = EndpointName,
                    IsHealthy = true, // 未配置视为健康（可选功能）
                    ErrorMessage = "未配置上游连接（可选功能）",
                    CheckedAt = _systemClock.LocalNowOffset
                };
            }

            // 检查连接状态
            var isConnected = _client.IsConnected;

            if (!isConnected)
            {
                _logger.LogWarning("上游连接未建立: {EndpointName}", EndpointName);
                return new UpstreamHealthStatus
                {
                    EndpointName = EndpointName,
                    IsHealthy = false,
                    ErrorCode = "NOT_CONNECTED",
                    ErrorMessage = "上游连接未建立，请检查网络配置",
                    CheckedAt = _systemClock.LocalNowOffset
                };
            }

            _logger.LogInformation("上游连接检查成功: {EndpointName}", EndpointName);
            return new UpstreamHealthStatus
            {
                EndpointName = EndpointName,
                IsHealthy = true,
                CheckedAt = _systemClock.LocalNowOffset
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("上游连接检查被取消: {EndpointName}", EndpointName);
            return new UpstreamHealthStatus
            {
                EndpointName = EndpointName,
                IsHealthy = false,
                ErrorCode = "CANCELLED",
                ErrorMessage = "健康检查操作被取消",
                CheckedAt = _systemClock.LocalNowOffset
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上游连接检查失败: {EndpointName}", EndpointName);
            return new UpstreamHealthStatus
            {
                EndpointName = EndpointName,
                IsHealthy = false,
                ErrorCode = "CHECK_ERROR",
                ErrorMessage = $"健康检查失败: {ex.Message}",
                CheckedAt = _systemClock.LocalNowOffset
            };
        }
    }
}

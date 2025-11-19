using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Communication.Health;

/// <summary>
/// RuleEngine上游健康检查实现
/// </summary>
public class RuleEngineUpstreamHealthChecker : IUpstreamHealthChecker
{
    private readonly IRuleEngineClient? _client;
    private readonly ILogger<RuleEngineUpstreamHealthChecker> _logger;
    private readonly string _connectionType;

    public RuleEngineUpstreamHealthChecker(
        IRuleEngineClient? client,
        string connectionType,
        ILogger<RuleEngineUpstreamHealthChecker> logger)
    {
        _client = client;
        _connectionType = connectionType;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string EndpointName => $"RuleEngine-{_connectionType}";

    /// <inheritdoc/>
    public async Task<UpstreamHealthStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
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
                    CheckedAt = DateTimeOffset.UtcNow
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
                    CheckedAt = DateTimeOffset.UtcNow
                };
            }

            _logger.LogInformation("上游连接检查成功: {EndpointName}", EndpointName);
            return new UpstreamHealthStatus
            {
                EndpointName = EndpointName,
                IsHealthy = true,
                CheckedAt = DateTimeOffset.UtcNow
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
                CheckedAt = DateTimeOffset.UtcNow
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
                CheckedAt = DateTimeOffset.UtcNow
            };
        }
    }
}

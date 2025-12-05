using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;

/// <summary>
/// 配置审计日志实现
/// </summary>
/// <remarks>
/// 将所有配置修改记录到专用的审计日志文件（config-audit-{date}.log）
/// </remarks>
public sealed class ConfigurationAuditLogger : IConfigurationAuditLogger
{
    private readonly ILogger<ConfigurationAuditLogger> _logger;
    private readonly ISystemClock _systemClock;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigurationAuditLogger(
        ILogger<ConfigurationAuditLogger> logger,
        ISystemClock systemClock)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <inheritdoc/>
    public void LogConfigurationChange<T>(
        string configName,
        string operationType,
        T? beforeConfig,
        T? afterConfig,
        string? operatorInfo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);

        try
        {
            var timestamp = _systemClock.LocalNow;
            var beforeJson = beforeConfig != null
                ? JsonSerializer.Serialize(beforeConfig, JsonOptions)
                : "null";
            var afterJson = afterConfig != null
                ? JsonSerializer.Serialize(afterConfig, JsonOptions)
                : "null";

            var operatorPart = string.IsNullOrEmpty(operatorInfo)
                ? string.Empty
                : $" | Operator={operatorInfo}";

            var logMessage = $"[配置审计] ConfigName={configName} | Operation={operationType} | Timestamp={timestamp:yyyy-MM-dd HH:mm:ss.fff}{operatorPart}"
                + Environment.NewLine
                + $"[修改前] {beforeJson}"
                + Environment.NewLine
                + $"[修改后] {afterJson}";

            _logger.LogInformation("{LogMessage}", logMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录配置审计日志失败: ConfigName={ConfigName}, OperationType={OperationType}",
                configName, operationType);
        }
    }
}

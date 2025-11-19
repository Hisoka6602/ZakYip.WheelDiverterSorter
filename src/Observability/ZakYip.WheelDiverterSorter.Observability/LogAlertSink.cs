using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Events;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 基于日志的告警接收器实现 / Log-based Alert Sink Implementation
/// 将告警事件写入结构化日志文件（alert.log），支持未来扩展到其他通道
/// Writes alert events to structured log file (alert.log), supports future extension to other channels
/// </summary>
public class LogAlertSink : IAlertSink
{
    private readonly ILogger<LogAlertSink> _logger;
    private readonly PrometheusMetrics? _metrics;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LogAlertSink(ILogger<LogAlertSink> logger, PrometheusMetrics? metrics = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics; // Optional - if not provided, metrics won't be recorded
    }

    public Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // 序列化为 JSON
            var jsonAlert = new
            {
                alertCode = alertEvent.AlertCode,
                severity = alertEvent.Severity.ToString(),
                message = alertEvent.Message,
                raisedAt = alertEvent.RaisedAt.ToString("o"), // ISO 8601 format
                lineId = alertEvent.LineId,
                chuteId = alertEvent.ChuteId,
                nodeId = alertEvent.NodeId,
                details = alertEvent.Details
            };

            var jsonString = JsonSerializer.Serialize(jsonAlert, JsonOptions);

            // 使用结构化日志输出（配置 NLog 或 Serilog 时可路由到 alert.log）
            // Use structured logging (can be routed to alert.log when configuring NLog or Serilog)
            _logger.LogInformation(
                "[ALERT] {AlertCode} | Severity={Severity} | Message={Message} | JSON={JsonAlert}",
                alertEvent.AlertCode,
                alertEvent.Severity,
                alertEvent.Message,
                jsonString);

            // 根据严重程度使用不同的日志级别
            // Use different log levels based on severity
            switch (alertEvent.Severity)
            {
                case Core.LineModel.Enums.AlertSeverity.Critical:
                    _logger.LogCritical(
                        "[ALERT-CRITICAL] {AlertCode}: {Message}", 
                        alertEvent.AlertCode, 
                        alertEvent.Message);
                    break;
                case Core.LineModel.Enums.AlertSeverity.Warning:
                    _logger.LogWarning(
                        "[ALERT-WARNING] {AlertCode}: {Message}", 
                        alertEvent.AlertCode, 
                        alertEvent.Message);
                    break;
                case Core.LineModel.Enums.AlertSeverity.Info:
                    _logger.LogInformation(
                        "[ALERT-INFO] {AlertCode}: {Message}", 
                        alertEvent.AlertCode, 
                        alertEvent.Message);
                    break;
            }

            // 记录 Prometheus 指标
            // Record Prometheus metrics
            _metrics?.RecordAlert(alertEvent.Severity.ToString(), alertEvent.AlertCode);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write alert to log: {AlertCode}", alertEvent.AlertCode);
            // Don't throw - alert sink failures should not break the main business flow
            return Task.CompletedTask;
        }
    }
}

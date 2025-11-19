using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Events;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 基于文件的告警记录器
/// </summary>
/// <remarks>
/// 将告警事件写入日志文件（JSONL格式）。
/// 文件名格式：alerts-yyyyMMdd.log
/// 写入失败不会影响主业务流程，仅记录 Warning 日志。
/// </remarks>
public class FileAlertSink : IAlertSink
{
    private readonly ILogger<FileAlertSink> _logger;
    private readonly string _logDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="logDirectory">日志目录，默认为 "logs"</param>
    public FileAlertSink(ILogger<FileAlertSink> logger, string logDirectory = "logs")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logDirectory = logDirectory;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // 确保日志目录存在
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
            _logger.LogInformation("创建告警日志目录: {Directory}", _logDirectory);
        }
    }

    /// <summary>
    /// 写入告警事件
    /// </summary>
    public async Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = $"alerts-{DateTime.UtcNow:yyyyMMdd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);

            // 序列化为 JSON
            var json = JsonSerializer.Serialize(new
            {
                alertEvent.AlertCode,
                Severity = alertEvent.Severity.ToString(),
                alertEvent.Message,
                RaisedAt = alertEvent.RaisedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
                alertEvent.LineId,
                alertEvent.ChuteId,
                alertEvent.NodeId,
                alertEvent.Details
            }, _jsonOptions);

            // 追加写入文件（每行一条 JSON）
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
        }
        catch (Exception ex)
        {
            // 捕获所有异常，不向调用方抛出
            // 仅记录 Warning 级别日志
            _logger.LogWarning(ex,
                "写入告警日志失败。AlertCode={AlertCode}, Severity={Severity}, Message={Message}",
                alertEvent.AlertCode,
                alertEvent.Severity,
                alertEvent.Message);
        }
    }
}

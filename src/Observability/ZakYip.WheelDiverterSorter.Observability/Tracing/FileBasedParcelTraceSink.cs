using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tracing;

/// <summary>
/// 基于日志文件的包裹追踪记录器
/// </summary>
/// <remarks>
/// 将追踪事件写入日志文件（JSONL格式）。
/// 文件名格式：parcel-trace-yyyyMMdd.log
/// 写入失败不会影响主业务流程，仅记录 Warning 日志。
/// </remarks>
public class FileBasedParcelTraceSink : IParcelTraceSink
{
    private readonly ILogger<FileBasedParcelTraceSink> _logger;
    private readonly ISystemClock _clock;
    private readonly string _logDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="logDirectory">日志目录，默认为 "logs"</param>
    public FileBasedParcelTraceSink(ILogger<FileBasedParcelTraceSink> logger, ISystemClock clock, string logDirectory = "logs")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
            _logger.LogInformation("创建包裹追踪日志目录: {Directory}", _logDirectory);
        }
    }

    /// <summary>
    /// 写入一条分拣轨迹事件
    /// </summary>
    /// <param name="eventArgs">事件参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async ValueTask WriteAsync(ParcelTraceEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = $"parcel-trace-{_clock.LocalNow:yyyyMMdd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);

            // 序列化为 JSON
            var json = JsonSerializer.Serialize(new
            {
                eventArgs.ItemId,
                eventArgs.BarCode,
                eventArgs.TargetChuteId,
                eventArgs.ActualChuteId,
                eventArgs.Stage,
                OccurredAt = eventArgs.OccurredAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
                eventArgs.Source,
                eventArgs.Details
            }, _jsonOptions);

            // 追加写入文件（每行一条 JSON）
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
        }
        catch (Exception ex)
        {
            // 捕获所有异常，不向调用方抛出
            // 仅记录 Warning 级别日志
            _logger.LogWarning(ex,
                "写入包裹追踪日志失败。ItemId={ItemId}, Stage={Stage}, Source={Source}",
                eventArgs.ItemId,
                eventArgs.Stage,
                eventArgs.Source);
        }
    }
}

using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Tracing;

namespace ZakYip.WheelDiverterSorter.Observability.Tracing;

/// <summary>
/// 基于日志文件的包裹追踪记录器
/// </summary>
/// <remarks>
/// 使用 ILogger 将追踪事件写入独立的日志文件。
/// 写入失败不会影响主业务流程，仅记录 Warning 日志。
/// </remarks>
public class FileBasedParcelTraceSink : IParcelTraceSink
{
    private readonly ILogger<FileBasedParcelTraceSink> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public FileBasedParcelTraceSink(ILogger<FileBasedParcelTraceSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 写入一条分拣轨迹事件
    /// </summary>
    /// <param name="eventArgs">事件参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public ValueTask WriteAsync(ParcelTraceEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            // 使用结构化日志记录，便于后续分析
            _logger.LogInformation(
                "ParcelTrace {@Trace}",
                new
                {
                    eventArgs.ItemId,
                    eventArgs.BarCode,
                    OccurredAt = eventArgs.OccurredAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
                    eventArgs.Stage,
                    eventArgs.Source,
                    eventArgs.Details
                });
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

        return ValueTask.CompletedTask;
    }
}

using System.Text;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// Markdown 格式的仿真报告写入器
/// </summary>
public class MarkdownReportWriter : ISimulationReportWriter
{
    private readonly ILogger<MarkdownReportWriter> _logger;
    private readonly ISystemClock _clock;
    private readonly string _outputDirectory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="outputDirectory">输出目录，默认为 logs/simulation</param>
    public MarkdownReportWriter(ILogger<MarkdownReportWriter> logger, ISystemClock clock, string? outputDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _outputDirectory = outputDirectory ?? Path.Combine("logs", "simulation");
    }

    /// <summary>
    /// 将仿真结果写入 Markdown 格式报告
    /// </summary>
    public async Task<string> WriteMarkdownAsync(
        string scenarioName,
        IReadOnlyCollection<ParcelTimelineSnapshot> parcels,
        CancellationToken cancellationToken = default)
    {
        // 确保输出目录存在
        Directory.CreateDirectory(_outputDirectory);

        // 生成文件名（使用本地时间）
        var timestamp = _clock.LocalNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{scenarioName}_{timestamp}.md";
        var filePath = Path.Combine(_outputDirectory, fileName);

        _logger.LogInformation("生成仿真报告: {FilePath}", filePath);

        // 构建报告内容
        var sb = new StringBuilder();

        // 标题
        sb.AppendLine($"# 仿真场景报告：{scenarioName}");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {_clock.LocalNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 场景摘要
        sb.AppendLine("## 场景摘要");
        sb.AppendLine();

        var totalParcels = parcels.Count;
        var successCount = parcels.Count(p => p.FinalStatus == ParcelFinalStatus.Success);
        var exceptionCount = parcels.Count(p => p.FinalStatus == ParcelFinalStatus.ExceptionRouted);
        var sensorFaultCount = parcels.Count(p => p.FinalStatus == ParcelFinalStatus.SensorFault);
        var timeoutCount = parcels.Count(p => p.FinalStatus == ParcelFinalStatus.Timeout);
        var droppedCount = parcels.Count(p => p.FinalStatus == ParcelFinalStatus.Dropped);
        var errorCount = parcels.Count(p => p.FinalStatus == ParcelFinalStatus.ExecutionError);
        var unknownSourceCount = parcels.Count(p => p.FinalStatus == ParcelFinalStatus.UnknownSource);

        sb.AppendLine($"- **总包裹数**: {totalParcels}");
        sb.AppendLine($"- **成功分拣**: {successCount} ({successCount * 100.0 / totalParcels:F1}%)");
        sb.AppendLine($"- **异常路由**: {exceptionCount} ({exceptionCount * 100.0 / totalParcels:F1}%)");
        sb.AppendLine($"- **传感器故障**: {sensorFaultCount}");
        sb.AppendLine($"- **超时**: {timeoutCount}");
        sb.AppendLine($"- **掉包**: {droppedCount}");
        sb.AppendLine($"- **执行错误**: {errorCount}");
        sb.AppendLine($"- **未知来源**: {unknownSourceCount}");
        sb.AppendLine();

        // 格口分布统计
        sb.AppendLine("## 格口分布");
        sb.AppendLine();
        var chuteGroups = parcels
            .Where(p => p.ActualChuteId.HasValue)
            .GroupBy(p => p.ActualChuteId!.Value)
            .OrderBy(g => g.Key);

        sb.AppendLine("| 格口ID | 包裹数 | 百分比 |");
        sb.AppendLine("|--------|--------|--------|");
        foreach (var group in chuteGroups)
        {
            var count = group.Count();
            var percentage = count * 100.0 / totalParcels;
            sb.AppendLine($"| {group.Key} | {count} | {percentage:F1}% |");
        }
        sb.AppendLine();

        // 包裹详情（仅显示前50个和异常包裹）
        sb.AppendLine("## 包裹详情");
        sb.AppendLine();
        sb.AppendLine("### 正常包裹（前50个）");
        sb.AppendLine();

        var normalParcels = parcels
            .Where(p => p.FinalStatus == ParcelFinalStatus.Success)
            .Take(50);

        foreach (var parcel in normalParcels)
        {
            AppendParcelDetails(sb, parcel);
        }

        // 异常包裹
        var exceptionParcels = parcels
            .Where(p => p.FinalStatus != ParcelFinalStatus.Success);

        if (exceptionParcels.Any())
        {
            sb.AppendLine("### 异常包裹（全部）");
            sb.AppendLine();

            foreach (var parcel in exceptionParcels)
            {
                AppendParcelDetails(sb, parcel);
            }
        }

        // 写入文件
        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);

        _logger.LogInformation("仿真报告已生成: {FilePath}, 总包裹数: {TotalParcels}", filePath, totalParcels);

        return filePath;
    }

    /// <summary>
    /// 添加包裹详情到报告
    /// </summary>
    private void AppendParcelDetails(StringBuilder sb, ParcelTimelineSnapshot parcel)
    {
        sb.AppendLine($"#### 包裹 {parcel.ParcelId}");
        sb.AppendLine();
        sb.AppendLine($"- **目标格口**: {parcel.TargetChuteId?.ToString() ?? "N/A"}");
        sb.AppendLine($"- **实际格口**: {parcel.ActualChuteId?.ToString() ?? "N/A"}");
        sb.AppendLine($"- **最终状态**: {parcel.FinalStatus}");
        
        if (!string.IsNullOrEmpty(parcel.FailureReason))
        {
            sb.AppendLine($"- **失败原因**: {parcel.FailureReason}");
        }

        if (parcel.IsDenseParcel)
        {
            sb.AppendLine($"- **高密度包裹**: 是");
            if (parcel.HeadwayTime.HasValue)
            {
                sb.AppendLine($"  - 时间间隔: {parcel.HeadwayTime.Value.TotalMilliseconds:F0}ms");
            }
            if (parcel.HeadwayMm.HasValue)
            {
                sb.AppendLine($"  - 空间间隔: {parcel.HeadwayMm.Value:F0}mm");
            }
        }

        if (parcel.CreatedTime.HasValue && parcel.CompletedTime.HasValue)
        {
            var duration = parcel.CompletedTime.Value - parcel.CreatedTime.Value;
            sb.AppendLine($"- **处理时长**: {duration.TotalSeconds:F2}s");
        }

        // 关键事件时间轴
        if (parcel.Events.Count > 0)
        {
            sb.AppendLine("- **事件时间轴**:");
            foreach (var evt in parcel.Events.Take(10)) // 仅显示前10个事件
            {
                sb.AppendLine($"  - `{evt.EventTime:HH:mm:ss.fff}` {evt.EventType}: {evt.Description}");
            }
            if (parcel.Events.Count > 10)
            {
                sb.AppendLine($"  - *(还有 {parcel.Events.Count - 10} 个事件未显示)*");
            }
        }

        sb.AppendLine();
    }
}

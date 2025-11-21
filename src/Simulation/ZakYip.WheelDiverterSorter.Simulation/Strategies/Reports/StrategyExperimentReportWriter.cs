using System.Globalization;
using System.Text;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Simulation.Strategies.Reports;

/// <summary>
/// 策略实验报表写入器
/// Strategy experiment report writer
/// </summary>
public class StrategyExperimentReportWriter
{
    private readonly ISystemClock _clock;

    public StrategyExperimentReportWriter(ISystemClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    /// <summary>
    /// 写入 CSV 格式报表
    /// Write CSV format report
    /// </summary>
    public void WriteCsvReport(IEnumerable<StrategyExperimentResult> results, string outputPath)
    {
        var csv = new StringBuilder();
        
        // CSV 标题行
        csv.AppendLine("ProfileName,Description,TotalParcels,SuccessParcels,ExceptionParcels,SuccessRatio,ExceptionRatio,OverloadEvents,AvgLatencyMs,MaxLatencyMs");
        
        // 数据行
        foreach (var result in results)
        {
            csv.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5:F4},{6:F4},{7},{8:F2},{9:F2}",
                EscapeCsvField(result.Profile.ProfileName),
                EscapeCsvField(result.Profile.Description),
                result.TotalParcels,
                result.SuccessParcels,
                result.ExceptionParcels,
                result.SuccessRatio,
                result.ExceptionRatio,
                result.OverloadEvents,
                result.AverageLatency.TotalMilliseconds,
                result.MaxLatency.TotalMilliseconds));
        }
        
        // 确保目录存在
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(outputPath, csv.ToString());
    }

    /// <summary>
    /// 写入 Markdown 格式报表
    /// Write Markdown format report
    /// </summary>
    public void WriteMarkdownReport(IEnumerable<StrategyExperimentResult> results, string outputPath)
    {
        var md = new StringBuilder();
        
        md.AppendLine("# 策略对比实验报告");
        md.AppendLine("# Strategy Comparison Experiment Report");
        md.AppendLine();
        md.AppendLine($"生成时间 / Generated at: {_clock.LocalNow:yyyy-MM-dd HH:mm:ss}");
        md.AppendLine();
        
        md.AppendLine("## 整体对比 / Overall Comparison");
        md.AppendLine();
        md.AppendLine("| Profile | 描述 / Description | 成功率 / Success | 异常率 / Exception | Overload 次数 / Events | 平均延迟(ms) / Avg Latency | 最大延迟(ms) / Max Latency |");
        md.AppendLine("|---------|-------------------|-----------------|-------------------|----------------------|---------------------------|---------------------------|");
        
        foreach (var result in results)
        {
            md.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "| {0} | {1} | {2:P2} | {3:P2} | {4} | {5:F2} | {6:F2} |",
                EscapeMarkdownField(result.Profile.ProfileName),
                EscapeMarkdownField(result.Profile.Description),
                result.SuccessRatio,
                result.ExceptionRatio,
                result.OverloadEvents,
                result.AverageLatency.TotalMilliseconds,
                result.MaxLatency.TotalMilliseconds));
        }
        
        md.AppendLine();
        md.AppendLine("## 详细配置 / Detailed Configuration");
        md.AppendLine();
        
        foreach (var result in results)
        {
            md.AppendLine($"### {result.Profile.ProfileName}");
            md.AppendLine();
            md.AppendLine($"**描述 / Description**: {result.Profile.Description}");
            md.AppendLine();
            md.AppendLine("**Overload 策略配置 / Overload Policy Configuration**:");
            md.AppendLine();
            var config = result.Profile.OverloadPolicy;
            md.AppendLine($"- 启用 / Enabled: {config.Enabled}");
            md.AppendLine($"- 严重拥堵强制异常 / Force Exception on Severe: {config.ForceExceptionOnSevere}");
            md.AppendLine($"- 超容量强制异常 / Force Exception on Over Capacity: {config.ForceExceptionOnOverCapacity}");
            md.AppendLine($"- 超时强制异常 / Force Exception on Timeout: {config.ForceExceptionOnTimeout}");
            md.AppendLine($"- 窗口不足强制异常 / Force Exception on Window Miss: {config.ForceExceptionOnWindowMiss}");
            md.AppendLine($"- 最大在途包裹数 / Max In Flight Parcels: {config.MaxInFlightParcels?.ToString() ?? "无限制 / Unlimited"}");
            md.AppendLine($"- 最小所需 TTL / Min Required TTL: {config.MinRequiredTtlMs}ms");
            md.AppendLine($"- 最小到达窗口 / Min Arrival Window: {config.MinArrivalWindowMs}ms");
            md.AppendLine();
            
            md.AppendLine("**统计结果 / Statistics**:");
            md.AppendLine();
            md.AppendLine($"- 总包裹数 / Total Parcels: {result.TotalParcels}");
            md.AppendLine($"- 成功落格 / Success Parcels: {result.SuccessParcels} ({result.SuccessRatio:P2})");
            md.AppendLine($"- 异常口 / Exception Parcels: {result.ExceptionParcels} ({result.ExceptionRatio:P2})");
            md.AppendLine($"- Overload 事件 / Overload Events: {result.OverloadEvents}");
            md.AppendLine($"- 平均延迟 / Average Latency: {result.AverageLatency.TotalMilliseconds:F2}ms");
            md.AppendLine($"- 最大延迟 / Max Latency: {result.MaxLatency.TotalMilliseconds:F2}ms");
            
            if (result.OverloadReasonDistribution != null && result.OverloadReasonDistribution.Any())
            {
                md.AppendLine();
                md.AppendLine("**Overload 原因分布 / Overload Reason Distribution**:");
                md.AppendLine();
                foreach (var kvp in result.OverloadReasonDistribution.OrderByDescending(x => x.Value))
                {
                    md.AppendLine($"- {kvp.Key}: {kvp.Value} 次");
                }
            }
            
            md.AppendLine();
        }
        
        // 确保目录存在
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(outputPath, md.ToString());
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;
            
        // 如果字段包含逗号、引号或换行符，则用引号包裹
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        
        return field;
    }

    private static string EscapeMarkdownField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;
            
        // 转义 Markdown 特殊字符
        return field
            .Replace("|", "\\|")
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }
}

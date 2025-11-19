using System.Text;
using ZakYip.WheelDiverterSorter.Tools.Reporting.Analyzers;
using ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Writers;

/// <summary>
/// 报表写入器
/// Report writer for generating CSV and Markdown files
/// </summary>
public class ReportWriter
{
    private readonly string _outputDirectory;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="outputDirectory">输出目录 / Output directory</param>
    public ReportWriter(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
        
        // 确保输出目录存在
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    /// <summary>
    /// 写入所有报表
    /// Write all reports
    /// </summary>
    public void WriteReports(AnalysisResult result, DateTimeOffset? fromTime, DateTimeOffset? toTime)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var timeRangeStr = BuildTimeRangeString(fromTime, toTime);

        // 写入 CSV 文件
        WriteTimeBucketCsv(result.TimeBuckets, timestamp);
        WriteOverloadReasonCsv(result.OverloadReasons, timestamp);
        WriteChuteErrorCsv(result.ChuteErrors, timestamp);
        WriteNodeErrorCsv(result.NodeErrors, timestamp);

        // 写入 Markdown 文件
        WriteMarkdownSummary(result, timestamp, timeRangeStr);

        Console.WriteLine($"\n📊 报表已生成至目录：{_outputDirectory}");
        Console.WriteLine($"   - summary-{timestamp}.csv");
        Console.WriteLine($"   - overload-{timestamp}.csv");
        Console.WriteLine($"   - chute-hotspot-{timestamp}.csv");
        Console.WriteLine($"   - node-hotspot-{timestamp}.csv");
        Console.WriteLine($"   - report-{timestamp}.md");
    }

    /// <summary>
    /// 写入时间片统计 CSV
    /// Write time bucket statistics CSV
    /// </summary>
    private void WriteTimeBucketCsv(List<TimeBucketStatistics> stats, string timestamp)
    {
        var fileName = Path.Combine(_outputDirectory, $"summary-{timestamp}.csv");
        var sb = new StringBuilder();

        // CSV 标题
        sb.AppendLine("BucketStart,BucketEnd,TotalParcels,ExceptionParcels,OverloadEvents,ExceptionRatio,OverloadRatio");

        // CSV 数据
        foreach (var stat in stats)
        {
            sb.AppendLine($"{stat.BucketStart:O},{stat.BucketEnd:O},{stat.TotalParcels},{stat.ExceptionParcels},{stat.OverloadEvents},{stat.ExceptionRatio:F4},{stat.OverloadRatio:F4}");
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 写入 OverloadReason 统计 CSV
    /// Write OverloadReason statistics CSV
    /// </summary>
    private void WriteOverloadReasonCsv(List<OverloadReasonStatistics> stats, string timestamp)
    {
        var fileName = Path.Combine(_outputDirectory, $"overload-{timestamp}.csv");
        var sb = new StringBuilder();

        // CSV 标题
        sb.AppendLine("Reason,Count,Percent");

        // CSV 数据
        foreach (var stat in stats)
        {
            sb.AppendLine($"{stat.Reason},{stat.Count},{stat.Percent:F2}");
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 写入格口热点统计 CSV
    /// Write chute hotspot statistics CSV
    /// </summary>
    private void WriteChuteErrorCsv(List<ChuteErrorStatistics> stats, string timestamp)
    {
        var fileName = Path.Combine(_outputDirectory, $"chute-hotspot-{timestamp}.csv");
        var sb = new StringBuilder();

        // CSV 标题
        sb.AppendLine("ChuteId,ExceptionCount,Percent");

        // CSV 数据
        foreach (var stat in stats)
        {
            sb.AppendLine($"{stat.ChuteId},{stat.ExceptionCount},{stat.Percent:F2}");
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 写入节点热点统计 CSV
    /// Write node hotspot statistics CSV
    /// </summary>
    private void WriteNodeErrorCsv(List<NodeErrorStatistics> stats, string timestamp)
    {
        var fileName = Path.Combine(_outputDirectory, $"node-hotspot-{timestamp}.csv");
        var sb = new StringBuilder();

        // CSV 标题
        sb.AppendLine("NodeId,EventCount,Percent");

        // CSV 数据
        foreach (var stat in stats)
        {
            sb.AppendLine($"{stat.NodeId},{stat.EventCount},{stat.Percent:F2}");
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 写入 Markdown 汇总报告
    /// Write Markdown summary report
    /// </summary>
    private void WriteMarkdownSummary(AnalysisResult result, string timestamp, string timeRangeStr)
    {
        var fileName = Path.Combine(_outputDirectory, $"report-{timestamp}.md");
        var sb = new StringBuilder();

        sb.AppendLine("# 包裹分拣异常统计报告");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"**统计时间范围**: {timeRangeStr}");
        sb.AppendLine();

        // 时间片统计
        sb.AppendLine("## 时间片维度统计");
        sb.AppendLine();
        if (result.TimeBuckets.Count > 0)
        {
            sb.AppendLine("| 起始时间 | 结束时间 | 总包裹数 | 异常包裹数 | 超载事件数 | 异常比例 | 超载比例 |");
            sb.AppendLine("|---------|---------|---------|-----------|-----------|---------|---------|");
            
            foreach (var stat in result.TimeBuckets)
            {
                sb.AppendLine($"| {stat.BucketStart:yyyy-MM-dd HH:mm:ss} | {stat.BucketEnd:yyyy-MM-dd HH:mm:ss} | {stat.TotalParcels} | {stat.ExceptionParcels} | {stat.OverloadEvents} | {stat.ExceptionRatio:P2} | {stat.OverloadRatio:P2} |");
            }

            // 汇总统计
            var totalParcels = result.TimeBuckets.Sum(s => s.TotalParcels);
            var totalExceptions = result.TimeBuckets.Sum(s => s.ExceptionParcels);
            var totalOverloads = result.TimeBuckets.Sum(s => s.OverloadEvents);
            var overallExceptionRatio = totalParcels > 0 ? (double)totalExceptions / totalParcels : 0;
            var overallOverloadRatio = totalParcels > 0 ? (double)totalOverloads / totalParcels : 0;

            sb.AppendLine();
            sb.AppendLine("### 汇总");
            sb.AppendLine($"- **总包裹数**: {totalParcels}");
            sb.AppendLine($"- **异常包裹数**: {totalExceptions}");
            sb.AppendLine($"- **超载事件数**: {totalOverloads}");
            sb.AppendLine($"- **整体异常率**: {overallExceptionRatio:P2}");
            sb.AppendLine($"- **整体超载率**: {overallOverloadRatio:P2}");
        }
        else
        {
            sb.AppendLine("*无数据*");
        }
        sb.AppendLine();

        // OverloadReason 统计
        sb.AppendLine("## OverloadReason 分布统计");
        sb.AppendLine();
        if (result.OverloadReasons.Count > 0)
        {
            sb.AppendLine("| 原因 | 次数 | 占比 |");
            sb.AppendLine("|-----|------|------|");
            
            foreach (var stat in result.OverloadReasons)
            {
                sb.AppendLine($"| {stat.Reason} | {stat.Count} | {stat.Percent:F2}% |");
            }
        }
        else
        {
            sb.AppendLine("*无数据*");
        }
        sb.AppendLine();

        // 格口热点统计
        sb.AppendLine("## 格口异常热点统计（Top 20）");
        sb.AppendLine();
        if (result.ChuteErrors.Count > 0)
        {
            sb.AppendLine("| 格口ID | 异常次数 | 占比 |");
            sb.AppendLine("|-------|---------|------|");
            
            foreach (var stat in result.ChuteErrors.Take(20))
            {
                sb.AppendLine($"| {stat.ChuteId} | {stat.ExceptionCount} | {stat.Percent:F2}% |");
            }
        }
        else
        {
            sb.AppendLine("*无数据*");
        }
        sb.AppendLine();

        // 节点热点统计
        sb.AppendLine("## 节点异常热点统计（Top 20）");
        sb.AppendLine();
        if (result.NodeErrors.Count > 0)
        {
            sb.AppendLine("| 节点ID | 事件次数 | 占比 |");
            sb.AppendLine("|-------|---------|------|");
            
            foreach (var stat in result.NodeErrors.Take(20))
            {
                sb.AppendLine($"| {stat.NodeId} | {stat.EventCount} | {stat.Percent:F2}% |");
            }
        }
        else
        {
            sb.AppendLine("*无数据*");
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 构建时间范围字符串
    /// Build time range string
    /// </summary>
    private string BuildTimeRangeString(DateTimeOffset? fromTime, DateTimeOffset? toTime)
    {
        if (fromTime.HasValue && toTime.HasValue)
        {
            return $"{fromTime.Value:yyyy-MM-dd HH:mm:ss} ~ {toTime.Value:yyyy-MM-dd HH:mm:ss}";
        }
        else if (fromTime.HasValue)
        {
            return $"{fromTime.Value:yyyy-MM-dd HH:mm:ss} ~ 最新";
        }
        else if (toTime.HasValue)
        {
            return $"开始 ~ {toTime.Value:yyyy-MM-dd HH:mm:ss}";
        }
        else
        {
            return "全部";
        }
    }

    /// <summary>
    /// 写入告警报表
    /// Write alert report
    /// </summary>
    public void WriteAlertReport(List<AlertLogRecord> alerts, DateTimeOffset? fromTime, DateTimeOffset? toTime)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        
        // 按严重程度分组统计
        var bySeverity = alerts.GroupBy(a => a.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        // 按告警代码分组统计
        var byAlertCode = alerts.GroupBy(a => a.AlertCode)
            .Select(g => new { AlertCode = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        // 写入告警统计 CSV
        WriteAlertStatisticsCsv(bySeverity, byAlertCode, timestamp);

        // 写入告警详情 CSV
        WriteAlertDetailCsv(alerts, timestamp);

        // 写入告警 Markdown 报表
        WriteAlertMarkdown(alerts, bySeverity, byAlertCode, timestamp, fromTime, toTime);

        Console.WriteLine($"\n📢 告警报表已生成：");
        Console.WriteLine($"   - alerts-statistics-{timestamp}.csv");
        Console.WriteLine($"   - alerts-detail-{timestamp}.csv");
        Console.WriteLine($"   - alerts-report-{timestamp}.md");
    }

    /// <summary>
    /// 写入告警统计 CSV
    /// </summary>
    private void WriteAlertStatisticsCsv(
        IEnumerable<object> bySeverity, 
        IEnumerable<object> byAlertCode, 
        string timestamp)
    {
        var fileName = Path.Combine(_outputDirectory, $"alerts-statistics-{timestamp}.csv");
        var sb = new StringBuilder();

        sb.AppendLine("## 按严重程度统计");
        sb.AppendLine("Severity,Count");
        foreach (dynamic item in bySeverity)
        {
            sb.AppendLine($"{item.Severity},{item.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("## 按告警代码统计");
        sb.AppendLine("AlertCode,Count");
        foreach (dynamic item in byAlertCode)
        {
            sb.AppendLine($"{item.AlertCode},{item.Count}");
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 写入告警详情 CSV
    /// </summary>
    private void WriteAlertDetailCsv(List<AlertLogRecord> alerts, string timestamp)
    {
        var fileName = Path.Combine(_outputDirectory, $"alerts-detail-{timestamp}.csv");
        var sb = new StringBuilder();

        sb.AppendLine("RaisedAt,Severity,AlertCode,Message");
        foreach (var alert in alerts.OrderBy(a => a.RaisedAt))
        {
            var message = alert.Message.Replace("\"", "\"\"").Replace(",", ";");
            sb.AppendLine($"{alert.RaisedAt:O},{alert.Severity},{alert.AlertCode},\"{message}\"");
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 写入告警 Markdown 报表
    /// </summary>
    private void WriteAlertMarkdown(
        List<AlertLogRecord> alerts,
        IEnumerable<object> bySeverity,
        IEnumerable<object> byAlertCode,
        string timestamp,
        DateTimeOffset? fromTime,
        DateTimeOffset? toTime)
    {
        var fileName = Path.Combine(_outputDirectory, $"alerts-report-{timestamp}.md");
        var sb = new StringBuilder();

        sb.AppendLine("# 告警分析报表");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"**统计范围**：{BuildTimeRangeString(fromTime, toTime)}");
        sb.AppendLine();
        sb.AppendLine($"**告警总数**：{alerts.Count}");
        sb.AppendLine();

        // 按严重程度统计
        sb.AppendLine("## 按严重程度统计");
        sb.AppendLine();
        sb.AppendLine("| 严重程度 | 数量 |");
        sb.AppendLine("|---------|------|");
        foreach (dynamic item in bySeverity)
        {
            sb.AppendLine($"| {item.Severity} | {item.Count} |");
        }
        sb.AppendLine();

        // 按告警代码统计（Top 20）
        sb.AppendLine("## 按告警代码统计（Top 20）");
        sb.AppendLine();
        sb.AppendLine("| 告警代码 | 数量 |");
        sb.AppendLine("|---------|------|");
        var codeList = byAlertCode.Cast<dynamic>().Take(20);
        foreach (dynamic item in codeList)
        {
            sb.AppendLine($"| {item.AlertCode} | {item.Count} |");
        }
        sb.AppendLine();

        // 最近的 Critical 告警（Top 10）
        var recentCritical = alerts
            .Where(a => a.Severity == "Critical")
            .OrderByDescending(a => a.RaisedAt)
            .Take(10)
            .ToList();

        if (recentCritical.Count > 0)
        {
            sb.AppendLine("## 最近的 Critical 告警（Top 10）");
            sb.AppendLine();
            sb.AppendLine("| 时间 | 告警代码 | 消息 |");
            sb.AppendLine("|------|---------|------|");
            foreach (var alert in recentCritical)
            {
                var message = alert.Message.Replace("|", "\\|");
                sb.AppendLine($"| {alert.RaisedAt:yyyy-MM-dd HH:mm:ss} | {alert.AlertCode} | {message} |");
            }
        }

        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
    }
}

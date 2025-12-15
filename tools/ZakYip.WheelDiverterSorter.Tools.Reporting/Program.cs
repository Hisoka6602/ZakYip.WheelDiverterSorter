using ZakYip.WheelDiverterSorter.Tools.Reporting.Analyzers;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Tools.Reporting.Models;
using ZakYip.WheelDiverterSorter.Tools.Reporting.Writers;

namespace ZakYip.WheelDiverterSorter.Tools.Reporting;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  包裹分拣异常统计工具");
        Console.WriteLine("  Parcel Sorting Exception Analysis Tool");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // 解析命令行参数
        var config = ParseArguments(args);

        if (config == null)
        {
            PrintUsage();
            return;
        }

        try
        {
            // 执行分析
            RunAnalysis(config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 错误：{ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 解析命令行参数
    /// Parse command line arguments
    /// </summary>
    private static AnalysisConfig? ParseArguments(string[] args)
    {
        var config = new AnalysisConfig
        {
            LogDirectory = "./logs",
            OutputDirectory = "./reports",
            BucketSize = TimeSpan.FromMinutes(5)
        };

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--log-dir":
                    if (i + 1 < args.Length)
                    {
                        config.LogDirectory = args[++i];
                    }
                    break;

                case "--from":
                    if (i + 1 < args.Length)
                    {
                        if (DateTimeOffset.TryParse(args[++i], out var fromTime))
                        {
                            config.FromTime = fromTime;
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ 警告：无法解析起始时间：{args[i]}");
                        }
                    }
                    break;

                case "--to":
                    if (i + 1 < args.Length)
                    {
                        if (DateTimeOffset.TryParse(args[++i], out var toTime))
                        {
                            config.ToTime = toTime;
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ 警告：无法解析结束时间：{args[i]}");
                        }
                    }
                    break;

                case "--bucket":
                    if (i + 1 < args.Length)
                    {
                        var bucketSpec = args[++i];
                        config.BucketSize = ParseBucketSize(bucketSpec);
                    }
                    break;

                case "--output":
                    if (i + 1 < args.Length)
                    {
                        config.OutputDirectory = args[++i];
                    }
                    break;

                case "--help":
                case "-h":
                case "-?":
                    return null;

                default:
                    Console.WriteLine($"⚠️ 警告：未知参数：{args[i]}");
                    break;
            }
        }

        return config;
    }

    /// <summary>
    /// 解析时间片大小
    /// Parse bucket size specification
    /// </summary>
    private static TimeSpan ParseBucketSize(string spec)
    {
        spec = spec.ToLowerInvariant().Trim();

        if (spec.EndsWith("m"))
        {
            if (int.TryParse(spec.TrimEnd('m'), out var minutes))
            {
                return TimeSpan.FromMinutes(minutes);
            }
        }
        else if (spec.EndsWith("h"))
        {
            if (int.TryParse(spec.TrimEnd('h'), out var hours))
            {
                return TimeSpan.FromHours(hours);
            }
        }
        else if (spec.EndsWith("s"))
        {
            if (int.TryParse(spec.TrimEnd('s'), out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        Console.WriteLine($"⚠️ 警告：无法解析时间片大小：{spec}，使用默认值 5 分钟");
        return TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// 执行分析
    /// Run analysis
    /// </summary>
    private static void RunAnalysis(AnalysisConfig config)
    {
        Console.WriteLine("⚙️  配置信息：");
        Console.WriteLine($"   日志目录：{config.LogDirectory}");
        Console.WriteLine($"   输出目录：{config.OutputDirectory}");
        Console.WriteLine($"   时间片大小：{config.BucketSize}");
        Console.WriteLine($"   起始时间：{(config.FromTime.HasValue ? config.FromTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "不限")}");
        Console.WriteLine($"   结束时间：{(config.ToTime.HasValue ? config.ToTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "不限")}");
        Console.WriteLine();

        // 扫描日志文件
        Console.WriteLine("🔍 正在扫描日志文件...");
        DateOnly? fromDate = config.FromTime.HasValue ? DateOnly.FromDateTime(config.FromTime.Value.DateTime) : null;
        DateOnly? toDate = config.ToTime.HasValue ? DateOnly.FromDateTime(config.ToTime.Value.DateTime) : null;
        
        var logFiles = LogParser.ScanTraceLogFiles(config.LogDirectory, fromDate, toDate);
        var alertFiles = LogParser.ScanAlertLogFiles(config.LogDirectory, fromDate, toDate);
        
        if (logFiles.Count == 0 && alertFiles.Count == 0)
        {
            Console.WriteLine("⚠️ 未找到任何日志文件");
            return;
        }

        Console.WriteLine($"✅ 找到 {logFiles.Count} 个包裹追踪日志文件");
        Console.WriteLine($"✅ 找到 {alertFiles.Count} 个告警日志文件");
        Console.WriteLine();

        // 解析日志
        Console.WriteLine("📖 正在解析日志...");
        var records = LogParser.ParseTraceLogFiles(logFiles, config.FromTime, config.ToTime);
        
        if (records.Count == 0)
        {
            Console.WriteLine("⚠️ 未找到任何有效记录");
            return;
        }

        Console.WriteLine();

        // 解析告警日志
        var alertRecords = new List<AlertLogRecord>();
        if (alertFiles.Count > 0)
        {
            Console.WriteLine("📖 正在解析告警日志...");
            alertRecords = LogParser.ParseAlertLogFiles(alertFiles, config.FromTime, config.ToTime);
            Console.WriteLine();
        }

        // 分析统计
        Console.WriteLine("📊 正在分析统计...");
        var analyzer = new TraceStatisticsAnalyzer(config.BucketSize);
        var result = analyzer.Analyze(records);

        Console.WriteLine($"✅ 分析完成：");
        Console.WriteLine($"   - 时间片数量：{result.TimeBuckets.Count}");
        Console.WriteLine($"   - OverloadReason 类型：{result.OverloadReasons.Count}");
        Console.WriteLine($"   - 异常格口数量：{result.ChuteErrors.Count}");
        Console.WriteLine($"   - 异常节点数量：{result.NodeErrors.Count}");
        Console.WriteLine();

        // 生成报表
        Console.WriteLine("📝 正在生成报表...");
        var writer = new ReportWriter(config.OutputDirectory, new LocalSystemClock());
        writer.WriteReports(result, config.FromTime, config.ToTime);
        
        // 如果有告警记录，生成告警报表
        if (alertRecords.Count > 0)
        {
            writer.WriteAlertReport(alertRecords, config.FromTime, config.ToTime);
        }

        // 汇总信息
        var totalParcels = result.TimeBuckets.Sum(s => s.TotalParcels);
        var totalExceptions = result.TimeBuckets.Sum(s => s.ExceptionParcels);
        var totalOverloads = result.TimeBuckets.Sum(s => s.OverloadEvents);
        var exceptionRatio = totalParcels > 0 ? (double)totalExceptions / totalParcels : 0;
        var overloadRatio = totalParcels > 0 ? (double)totalOverloads / totalParcels : 0;

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("  汇总信息");
        Console.WriteLine("========================================");
        Console.WriteLine($"统计时间区间：{BuildTimeRangeString(config.FromTime, config.ToTime)}");
        Console.WriteLine($"总包裹数：{totalParcels:N0}");
        Console.WriteLine($"异常包裹数：{totalExceptions:N0}");
        Console.WriteLine($"超载事件数：{totalOverloads:N0}");
        Console.WriteLine($"告警事件数：{alertRecords.Count:N0}");
        Console.WriteLine($"异常比例：{exceptionRatio:P2}");
        Console.WriteLine($"超载比例：{overloadRatio:P2}");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine("✅ 完成！");
    }

    /// <summary>
    /// 打印使用说明
    /// Print usage information
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("用法 / Usage:");
        Console.WriteLine();
        Console.WriteLine("  dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \\");
        Console.WriteLine("    [--log-dir <目录>] \\");
        Console.WriteLine("    [--from <起始时间>] \\");
        Console.WriteLine("    [--to <结束时间>] \\");
        Console.WriteLine("    [--bucket <时间片大小>] \\");
        Console.WriteLine("    [--output <输出目录>]");
        Console.WriteLine();
        Console.WriteLine("参数说明 / Parameters:");
        Console.WriteLine("  --log-dir    日志根目录（默认：./logs）");
        Console.WriteLine("               Log root directory (default: ./logs)");
        Console.WriteLine();
        Console.WriteLine("  --from       起始时间，格式：YYYY-MM-DD 或 YYYY-MM-DDTHH:mm:ss");
        Console.WriteLine("               Start time, format: YYYY-MM-DD or YYYY-MM-DDTHH:mm:ss");
        Console.WriteLine();
        Console.WriteLine("  --to         结束时间，格式同上（可选，不填则到最新）");
        Console.WriteLine("               End time, same format (optional, defaults to latest)");
        Console.WriteLine();
        Console.WriteLine("  --bucket     时间片大小，如 5m（分钟）、1h（小时）、30s（秒）");
        Console.WriteLine("               Time bucket size, e.g., 5m (minutes), 1h (hours), 30s (seconds)");
        Console.WriteLine("               默认：5m / Default: 5m");
        Console.WriteLine();
        Console.WriteLine("  --output     输出目录（默认：./reports）");
        Console.WriteLine("               Output directory (default: ./reports)");
        Console.WriteLine();
        Console.WriteLine("示例 / Examples:");
        Console.WriteLine();
        Console.WriteLine("  # 分析所有日志，按 1 小时分片");
        Console.WriteLine("  dotnet run --bucket 1h");
        Console.WriteLine();
        Console.WriteLine("  # 分析指定日期范围的日志");
        Console.WriteLine("  dotnet run --from 2025-11-01 --to 2025-11-18");
        Console.WriteLine();
        Console.WriteLine("  # 自定义所有参数");
        Console.WriteLine("  dotnet run --log-dir /var/logs/wheel-diverter --from 2025-11-01 --bucket 1h --output ./my-reports");
    }

    /// <summary>
    /// 构建时间范围字符串
    /// Build time range string
    /// </summary>
    private static string BuildTimeRangeString(DateTimeOffset? fromTime, DateTimeOffset? toTime)
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
            return "全部日志";
        }
    }
}

/// <summary>
/// 分析配置
/// Analysis configuration
/// </summary>
class AnalysisConfig
{
    public required string LogDirectory { get; set; }
    public required string OutputDirectory { get; set; }
    public required TimeSpan BucketSize { get; set; }
    public DateTimeOffset? FromTime { get; set; }
    public DateTimeOffset? ToTime { get; set; }
}

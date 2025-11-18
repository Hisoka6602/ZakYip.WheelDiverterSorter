using System.Text.Json;
using ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Analyzers;

/// <summary>
/// 日志解析器
/// Log parser for trace log files
/// </summary>
public static class LogParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 解析日志文件并返回指定时间范围内的记录
    /// Parse log files and return records within the specified time range
    /// </summary>
    /// <param name="logFiles">日志文件路径列表 / Log file path list</param>
    /// <param name="fromTime">起始时间（可选）/ Start time (optional)</param>
    /// <param name="toTime">结束时间（可选）/ End time (optional)</param>
    /// <returns>解析的日志记录列表 / List of parsed log records</returns>
    public static List<ParcelTraceLogRecord> ParseTraceLogFiles(
        IEnumerable<string> logFiles,
        DateTimeOffset? fromTime = null,
        DateTimeOffset? toTime = null)
    {
        var records = new List<ParcelTraceLogRecord>();
        int totalLines = 0;
        int skippedLines = 0;

        foreach (var logFile in logFiles)
        {
            if (!File.Exists(logFile))
            {
                Console.WriteLine($"⚠️ 警告：日志文件不存在：{logFile}");
                continue;
            }

            Console.WriteLine($"📖 正在读取：{logFile}");

            try
            {
                foreach (var line in File.ReadLines(logFile))
                {
                    totalLines++;
                    
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var record = JsonSerializer.Deserialize<ParcelTraceLogRecord>(line, JsonOptions);
                        
                        // 过滤时间范围
                        if (fromTime.HasValue && record.OccurredAt < fromTime.Value)
                        {
                            continue;
                        }
                        if (toTime.HasValue && record.OccurredAt > toTime.Value)
                        {
                            continue;
                        }

                        records.Add(record);
                    }
                    catch (JsonException ex)
                    {
                        skippedLines++;
                        if (skippedLines <= 10) // 只显示前 10 个解析错误
                        {
                            Console.WriteLine($"⚠️ 警告：第 {totalLines} 行解析失败：{ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 警告：读取文件 {logFile} 时出错：{ex.Message}");
            }
        }

        Console.WriteLine($"✅ 解析完成：总行数 {totalLines}，跳过 {skippedLines} 行，有效记录 {records.Count} 条");
        if (skippedLines > 10)
        {
            Console.WriteLine($"   （省略了 {skippedLines - 10} 个额外的解析错误）");
        }

        return records;
    }

    /// <summary>
    /// 扫描日志目录并查找指定日期范围内的 trace 日志文件
    /// Scan log directory and find trace log files within the specified date range
    /// </summary>
    /// <param name="logDirectory">日志根目录 / Log root directory</param>
    /// <param name="fromDate">起始日期 / Start date</param>
    /// <param name="toDate">结束日期（可选）/ End date (optional)</param>
    /// <returns>日志文件路径列表 / List of log file paths</returns>
    public static List<string> ScanTraceLogFiles(
        string logDirectory,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        var traceDir = Path.Combine(logDirectory, "trace");
        if (!Directory.Exists(traceDir))
        {
            Console.WriteLine($"⚠️ 警告：trace 日志目录不存在：{traceDir}");
            return new List<string>();
        }

        var pattern = "parcel-trace-*.log";
        var allFiles = Directory.GetFiles(traceDir, pattern, SearchOption.TopDirectoryOnly);

        // 如果没有指定日期范围，返回所有文件
        if (!fromDate.HasValue && !toDate.HasValue)
        {
            return allFiles.OrderBy(f => f).ToList();
        }

        var filteredFiles = new List<string>();

        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            // 尝试从文件名解析日期：parcel-trace-YYYY-MM-DD
            if (TryParseDateFromFileName(fileName, out var fileDate))
            {
                if (fromDate.HasValue && fileDate < fromDate.Value)
                {
                    continue;
                }
                if (toDate.HasValue && fileDate > toDate.Value)
                {
                    continue;
                }
                filteredFiles.Add(file);
            }
            else
            {
                // 无法解析日期的文件也包含进来
                filteredFiles.Add(file);
            }
        }

        return filteredFiles.OrderBy(f => f).ToList();
    }

    /// <summary>
    /// 从文件名中解析日期
    /// Parse date from file name
    /// </summary>
    private static bool TryParseDateFromFileName(string fileName, out DateOnly date)
    {
        date = default;

        // 文件名格式：parcel-trace-YYYY-MM-DD
        var parts = fileName.Split('-');
        if (parts.Length >= 5)
        {
            var datePart = $"{parts[2]}-{parts[3]}-{parts[4]}";
            return DateOnly.TryParse(datePart, out date);
        }

        return false;
    }
}

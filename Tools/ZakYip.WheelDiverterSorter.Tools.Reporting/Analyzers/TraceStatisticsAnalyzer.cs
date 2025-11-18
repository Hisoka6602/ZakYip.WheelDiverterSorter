using System.Text.Json;
using ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Analyzers;

/// <summary>
/// 追踪日志统计分析器
/// Trace statistics analyzer
/// </summary>
public class TraceStatisticsAnalyzer
{
    private readonly TimeSpan _bucketSize;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="bucketSize">时间片大小 / Time bucket size</param>
    public TraceStatisticsAnalyzer(TimeSpan bucketSize)
    {
        _bucketSize = bucketSize;
    }

    /// <summary>
    /// 分析追踪日志记录
    /// Analyze trace log records
    /// </summary>
    public AnalysisResult Analyze(List<ParcelTraceLogRecord> records)
    {
        if (records.Count == 0)
        {
            return new AnalysisResult
            {
                TimeBuckets = new List<TimeBucketStatistics>(),
                OverloadReasons = new List<OverloadReasonStatistics>(),
                ChuteErrors = new List<ChuteErrorStatistics>(),
                NodeErrors = new List<NodeErrorStatistics>()
            };
        }

        var timeBuckets = AnalyzeTimeBuckets(records);
        var overloadReasons = AnalyzeOverloadReasons(records);
        var chuteErrors = AnalyzeChuteErrors(records);
        var nodeErrors = AnalyzeNodeErrors(records);

        return new AnalysisResult
        {
            TimeBuckets = timeBuckets,
            OverloadReasons = overloadReasons,
            ChuteErrors = chuteErrors,
            NodeErrors = nodeErrors
        };
    }

    /// <summary>
    /// 时间片维度统计
    /// Time bucket dimension statistics
    /// </summary>
    private List<TimeBucketStatistics> AnalyzeTimeBuckets(List<ParcelTraceLogRecord> records)
    {
        if (records.Count == 0)
        {
            return new List<TimeBucketStatistics>();
        }

        var minTime = records.Min(r => r.OccurredAt);
        var maxTime = records.Max(r => r.OccurredAt);

        // 按时间片分组
        var buckets = new Dictionary<DateTimeOffset, BucketData>();

        foreach (var record in records)
        {
            var bucketStart = GetBucketStart(record.OccurredAt, minTime);
            
            if (!buckets.ContainsKey(bucketStart))
            {
                buckets[bucketStart] = new BucketData();
            }

            var bucket = buckets[bucketStart];

            // 统计包裹创建事件
            if (record.Stage == "Created")
            {
                bucket.TotalParcels++;
            }

            // 统计异常落格
            if (record.Stage == "ExceptionDiverted")
            {
                bucket.ExceptionParcels++;
            }

            // 统计超载决策
            if (record.Stage == "OverloadDecision")
            {
                bucket.OverloadEvents++;
            }
        }

        var result = buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new TimeBucketStatistics
            {
                BucketStart = kv.Key,
                BucketEnd = kv.Key + _bucketSize,
                TotalParcels = kv.Value.TotalParcels,
                ExceptionParcels = kv.Value.ExceptionParcels,
                OverloadEvents = kv.Value.OverloadEvents
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// OverloadReason 分布统计
    /// OverloadReason distribution statistics
    /// </summary>
    private List<OverloadReasonStatistics> AnalyzeOverloadReasons(List<ParcelTraceLogRecord> records)
    {
        var overloadRecords = records
            .Where(r => r.Stage == "OverloadDecision" || r.Stage == "ExceptionDiverted")
            .ToList();

        if (overloadRecords.Count == 0)
        {
            return new List<OverloadReasonStatistics>();
        }

        var reasonCounts = new Dictionary<string, int>();

        foreach (var record in overloadRecords)
        {
            var reason = ExtractReason(record.Details);
            reasonCounts[reason] = reasonCounts.GetValueOrDefault(reason, 0) + 1;
        }

        var total = reasonCounts.Values.Sum();

        var result = reasonCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new OverloadReasonStatistics
            {
                Reason = kv.Key,
                Count = kv.Value,
                Percent = total > 0 ? (double)kv.Value / total * 100 : 0
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// 按格口的异常热点统计
    /// Exception hotspot statistics by chute
    /// </summary>
    private List<ChuteErrorStatistics> AnalyzeChuteErrors(List<ParcelTraceLogRecord> records)
    {
        var exceptionRecords = records
            .Where(r => r.Stage == "ExceptionDiverted")
            .ToList();

        if (exceptionRecords.Count == 0)
        {
            return new List<ChuteErrorStatistics>();
        }

        var chuteCounts = new Dictionary<string, int>();

        foreach (var record in exceptionRecords)
        {
            var chuteId = ExtractChuteId(record.Details);
            chuteCounts[chuteId] = chuteCounts.GetValueOrDefault(chuteId, 0) + 1;
        }

        var total = chuteCounts.Values.Sum();

        var result = chuteCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new ChuteErrorStatistics
            {
                ChuteId = kv.Key,
                ExceptionCount = kv.Value,
                Percent = total > 0 ? (double)kv.Value / total * 100 : 0
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// 按节点的异常热点统计
    /// Exception hotspot statistics by node
    /// </summary>
    private List<NodeErrorStatistics> AnalyzeNodeErrors(List<ParcelTraceLogRecord> records)
    {
        var relevantRecords = records
            .Where(r => r.Stage == "OverloadDecision" || r.Stage == "NodeArrived" || r.Stage == "ExceptionDiverted")
            .ToList();

        if (relevantRecords.Count == 0)
        {
            return new List<NodeErrorStatistics>();
        }

        var nodeCounts = new Dictionary<string, int>();

        foreach (var record in relevantRecords)
        {
            var nodeId = ExtractNodeId(record.Details);
            if (!string.IsNullOrEmpty(nodeId))
            {
                nodeCounts[nodeId] = nodeCounts.GetValueOrDefault(nodeId, 0) + 1;
            }
        }

        var total = nodeCounts.Values.Sum();

        var result = nodeCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new NodeErrorStatistics
            {
                NodeId = kv.Key,
                EventCount = kv.Value,
                Percent = total > 0 ? (double)kv.Value / total * 100 : 0
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// 获取时间片起始时间
    /// Get bucket start time
    /// </summary>
    private DateTimeOffset GetBucketStart(DateTimeOffset time, DateTimeOffset baseTime)
    {
        var elapsed = time - baseTime;
        var bucketIndex = (long)(elapsed.TotalSeconds / _bucketSize.TotalSeconds);
        return baseTime + TimeSpan.FromSeconds(bucketIndex * _bucketSize.TotalSeconds);
    }

    /// <summary>
    /// 从 Details 字段中提取 Reason
    /// Extract Reason from Details field
    /// </summary>
    private string ExtractReason(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return "Unknown";
        }

        try
        {
            // 尝试解析为 JSON
            using var doc = JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("Reason", out var reasonElement))
            {
                return reasonElement.GetString() ?? "Unknown";
            }
            if (doc.RootElement.TryGetProperty("reason", out var reasonElementLower))
            {
                return reasonElementLower.GetString() ?? "Unknown";
            }
        }
        catch
        {
            // JSON 解析失败，尝试简单文本匹配
            if (details.Contains("Reason=", StringComparison.OrdinalIgnoreCase) ||
                details.Contains("reason=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = details.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (part.StartsWith("Reason=", StringComparison.OrdinalIgnoreCase) ||
                        part.StartsWith("reason=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = part.Substring(part.IndexOf('=') + 1).Trim();
                        return value;
                    }
                }
            }
        }

        return "Unknown";
    }

    /// <summary>
    /// 从 Details 字段中提取 ChuteId
    /// Extract ChuteId from Details field
    /// </summary>
    private string ExtractChuteId(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return "Unknown";
        }

        try
        {
            // 尝试解析为 JSON
            using var doc = JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("ChuteId", out var chuteElement))
            {
                return chuteElement.GetString() ?? "Unknown";
            }
            if (doc.RootElement.TryGetProperty("chuteId", out var chuteElementLower))
            {
                return chuteElementLower.GetString() ?? "Unknown";
            }
            if (doc.RootElement.TryGetProperty("ActualChute", out var actualChuteElement))
            {
                return actualChuteElement.GetString() ?? "Unknown";
            }
        }
        catch
        {
            // JSON 解析失败，尝试简单文本匹配
        }

        return "Unknown";
    }

    /// <summary>
    /// 从 Details 字段中提取 NodeId
    /// Extract NodeId from Details field
    /// </summary>
    private string ExtractNodeId(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return string.Empty;
        }

        try
        {
            // 尝试解析为 JSON
            using var doc = JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("NodeId", out var nodeElement))
            {
                return nodeElement.GetString() ?? string.Empty;
            }
            if (doc.RootElement.TryGetProperty("nodeId", out var nodeElementLower))
            {
                return nodeElementLower.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // JSON 解析失败
        }

        return string.Empty;
    }

    /// <summary>
    /// 临时数据结构，用于累积统计
    /// Temporary data structure for accumulating statistics
    /// </summary>
    private class BucketData
    {
        public int TotalParcels { get; set; }
        public int ExceptionParcels { get; set; }
        public int OverloadEvents { get; set; }
    }
}

/// <summary>
/// 分析结果
/// Analysis result
/// </summary>
public class AnalysisResult
{
    public required List<TimeBucketStatistics> TimeBuckets { get; init; }
    public required List<OverloadReasonStatistics> OverloadReasons { get; init; }
    public required List<ChuteErrorStatistics> ChuteErrors { get; init; }
    public required List<NodeErrorStatistics> NodeErrors { get; init; }
}

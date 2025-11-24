using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 路由配置导入导出服务
/// </summary>
public interface IRouteImportExportService
{
    /// <summary>
    /// 导出路由配置为 JSON 格式
    /// </summary>
    byte[] ExportAsJson(IEnumerable<ChuteRouteConfiguration> configs);

    /// <summary>
    /// 导出路由配置为 CSV 格式
    /// </summary>
    byte[] ExportAsCsv(IEnumerable<ChuteRouteConfiguration> configs);

    /// <summary>
    /// 从 JSON 格式导入路由配置
    /// </summary>
    List<RouteConfigRequest> ImportFromJson(Stream stream);

    /// <summary>
    /// 从 CSV 格式导入路由配置
    /// </summary>
    List<RouteConfigRequest> ImportFromCsv(Stream stream);
}

/// <summary>
/// 路由配置导入导出服务实现
/// </summary>
public class RouteImportExportService : IRouteImportExportService
{
    private readonly ILogger<RouteImportExportService> _logger;

    public RouteImportExportService(ILogger<RouteImportExportService> logger)
    {
        _logger = logger;
    }

    public byte[] ExportAsJson(IEnumerable<ChuteRouteConfiguration> configs)
    {
        var exportData = configs.Select(config => new
        {
            chuteId = config.ChuteId,
            chuteName = config.ChuteName,
            diverterConfigurations = config.DiverterConfigurations.Select(d => new
            {
                diverterId = d.DiverterId,
                targetDirection = (int)d.TargetDirection,
                sequenceNumber = d.SequenceNumber,
                segmentLengthMm = d.SegmentLengthMm,
                segmentSpeedMmPerSecond = d.SegmentSpeedMmPerSecond,
                segmentToleranceTimeMs = d.SegmentToleranceTimeMs
            }).ToList(),
            beltSpeedMmPerSecond = config.BeltSpeedMmPerSecond,
            beltLengthMm = config.BeltLengthMm,
            toleranceTimeMs = config.ToleranceTimeMs,
            isEnabled = config.IsEnabled
        }).ToList();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping  // 解决中文字符被转义的问题
        };

        var json = JsonSerializer.Serialize(exportData, options);
        return Encoding.UTF8.GetBytes(json);
    }

    public byte[] ExportAsCsv(IEnumerable<ChuteRouteConfiguration> configs)
    {
        var sb = new StringBuilder();
        
        // CSV 头部
        sb.AppendLine("ChuteId,ChuteName,DiverterId,TargetDirection,SequenceNumber,SegmentLengthMm,SegmentSpeedMmPerSecond,SegmentToleranceTimeMs,BeltSpeedMmPerSecond,BeltLengthMm,ToleranceTimeMs,IsEnabled");

        // CSV 数据行
        foreach (var config in configs)
        {
            foreach (var diverter in config.DiverterConfigurations.OrderBy(d => d.SequenceNumber))
            {
                sb.AppendLine(string.Join(",",
                    config.ChuteId,
                    EscapeCsvValue(config.ChuteName ?? ""),
                    diverter.DiverterId,
                    (int)diverter.TargetDirection,
                    diverter.SequenceNumber,
                    diverter.SegmentLengthMm.ToString(CultureInfo.InvariantCulture),
                    diverter.SegmentSpeedMmPerSecond.ToString(CultureInfo.InvariantCulture),
                    diverter.SegmentToleranceTimeMs,
                    config.BeltSpeedMmPerSecond.ToString(CultureInfo.InvariantCulture),
                    config.BeltLengthMm.ToString(CultureInfo.InvariantCulture),
                    config.ToleranceTimeMs,
                    config.IsEnabled));
            }
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public List<RouteConfigRequest> ImportFromJson(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var importData = JsonSerializer.Deserialize<List<RouteConfigRequest>>(json, options);
        
        if (importData == null || importData.Count == 0)
        {
            throw new InvalidOperationException("JSON 文件为空或格式不正确");
        }

        return importData;
    }

    public List<RouteConfigRequest> ImportFromCsv(Stream stream)
    {
        var routes = new Dictionary<int, RouteConfigRequest>();

        using var reader = new StreamReader(stream);
        
        // 跳过头部
        var header = reader.ReadLine();
        if (header == null)
        {
            throw new InvalidOperationException("CSV 文件为空");
        }

        int lineNumber = 1;
        while (!reader.EndOfStream)
        {
            lineNumber++;
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var values = ParseCsvLine(line);
                if (values.Length < 12)
                {
                    throw new InvalidOperationException($"第 {lineNumber} 行字段数不足，至少需要12个字段");
                }

                var chuteId = int.Parse(values[0]);
                var chuteName = values[1];
                var diverterId = int.Parse(values[2]);
                var targetDirection = (DiverterDirection)int.Parse(values[3]);
                var sequenceNumber = int.Parse(values[4]);
                var segmentLengthMm = double.Parse(values[5], CultureInfo.InvariantCulture);
                var segmentSpeedMmPerSecond = double.Parse(values[6], CultureInfo.InvariantCulture);
                var segmentToleranceTimeMs = int.Parse(values[7]);
                var beltSpeedMmPerSecond = double.Parse(values[8], CultureInfo.InvariantCulture);
                var beltLengthMm = double.Parse(values[9], CultureInfo.InvariantCulture);
                var toleranceTimeMs = int.Parse(values[10]);
                var isEnabled = bool.Parse(values[11]);

                if (!routes.ContainsKey(chuteId))
                {
                    routes[chuteId] = new RouteConfigRequest
                    {
                        ChuteId = chuteId,
                        ChuteName = chuteName,
                        DiverterConfigurations = new List<DiverterConfigRequest>(),
                        BeltSpeedMmPerSecond = beltSpeedMmPerSecond,
                        BeltLengthMm = beltLengthMm,
                        ToleranceTimeMs = toleranceTimeMs,
                        IsEnabled = isEnabled
                    };
                }

                routes[chuteId].DiverterConfigurations.Add(new DiverterConfigRequest
                {
                    DiverterId = diverterId,
                    TargetDirection = targetDirection,
                    SequenceNumber = sequenceNumber,
                    SegmentLengthMm = segmentLengthMm,
                    SegmentSpeedMmPerSecond = segmentSpeedMmPerSecond,
                    SegmentToleranceTimeMs = segmentToleranceTimeMs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析 CSV 第 {LineNumber} 行失败", lineNumber);
                throw new InvalidOperationException($"解析 CSV 第 {lineNumber} 行失败: {ex.Message}", ex);
            }
        }

        return routes.Values.ToList();
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var currentValue = new StringBuilder();
        var insideQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // 双引号转义
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (c == ',' && !insideQuotes)
            {
                values.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        values.Add(currentValue.ToString());
        return values.ToArray();
    }
}

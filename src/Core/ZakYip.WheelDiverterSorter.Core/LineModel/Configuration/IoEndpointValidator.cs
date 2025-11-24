using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// IO 端点验证器
/// </summary>
/// <remarks>
/// 提供 IO 端点有效性验证逻辑：
/// - 小于 0 的端点视为无效（包括默认值 -1）
/// - 大于 1000 的端点视为无效
/// - null 视为无效
/// 无效的端点不应被监控或读写。
/// </remarks>
public static class IoEndpointValidator
{
    /// <summary>
    /// 默认无效端点值（用于表示未配置的IO端点）
    /// </summary>
    public const int InvalidEndpoint = -1;

    /// <summary>
    /// 最小有效端点编号
    /// </summary>
    public const int MinValidEndpoint = 0;

    /// <summary>
    /// 最大有效端点编号
    /// </summary>
    public const int MaxValidEndpoint = 1000;

    /// <summary>
    /// 验证 IO 端点编号是否有效
    /// </summary>
    /// <param name="endpoint">IO 端点编号</param>
    /// <returns>true 表示有效，false 表示无效</returns>
    public static bool IsValidEndpoint(int endpoint)
    {
        return endpoint >= MinValidEndpoint && endpoint <= MaxValidEndpoint;
    }

    /// <summary>
    /// 验证 IO 端点编号是否有效（可空类型）
    /// </summary>
    /// <param name="endpoint">IO 端点编号（可为 null）</param>
    /// <returns>true 表示有效，false 表示无效或为 null</returns>
    public static bool IsValidEndpoint(int? endpoint)
    {
        return endpoint.HasValue && IsValidEndpoint(endpoint.Value);
    }

    /// <summary>
    /// 过滤无效的 IO 端点
    /// </summary>
    /// <param name="ioPoints">IO 联动点列表</param>
    /// <returns>仅包含有效端点的 IO 联动点列表</returns>
    public static IEnumerable<IoLinkagePoint> FilterValidEndpoints(IEnumerable<IoLinkagePoint> ioPoints)
    {
        return ioPoints.Where(p => IsValidEndpoint(p.BitNumber));
    }

    /// <summary>
    /// 验证并记录无效的 IO 端点
    /// </summary>
    /// <typeparam name="T">日志记录器类型</typeparam>
    /// <param name="ioPoints">IO 联动点列表</param>
    /// <param name="logger">日志记录器</param>
    /// <returns>仅包含有效端点的 IO 联动点列表</returns>
    public static IEnumerable<IoLinkagePoint> FilterAndLogInvalidEndpoints<T>(
        IEnumerable<IoLinkagePoint> ioPoints,
        Microsoft.Extensions.Logging.ILogger<T> logger)
    {
        var validPoints = new List<IoLinkagePoint>();
        var invalidPoints = new List<int>();

        foreach (var point in ioPoints)
        {
            if (IsValidEndpoint(point.BitNumber))
            {
                validPoints.Add(point);
            }
            else
            {
                invalidPoints.Add(point.BitNumber);
            }
        }

        if (invalidPoints.Count > 0)
        {
            logger.LogWarning(
                "检测到 {Count} 个无效 IO 端点，已跳过: [{Endpoints}]。有效范围: {MinValid}-{MaxValid}",
                invalidPoints.Count,
                string.Join(", ", invalidPoints),
                MinValidEndpoint,
                MaxValidEndpoint);
        }

        return validPoints;
    }

    /// <summary>
    /// 获取验证错误消息
    /// </summary>
    /// <param name="endpoint">IO 端点编号</param>
    /// <returns>错误消息，如果端点有效则返回 null</returns>
    public static string? GetValidationError(int endpoint)
    {
        if (endpoint < MinValidEndpoint)
        {
            return $"IO 端点 {endpoint} 无效：小于最小值 {MinValidEndpoint}";
        }

        if (endpoint > MaxValidEndpoint)
        {
            return $"IO 端点 {endpoint} 无效：大于最大值 {MaxValidEndpoint}";
        }

        return null;
    }

    /// <summary>
    /// 获取验证错误消息（可空类型）
    /// </summary>
    /// <param name="endpoint">IO 端点编号（可为 null）</param>
    /// <returns>错误消息，如果端点有效则返回 null</returns>
    public static string? GetValidationError(int? endpoint)
    {
        if (!endpoint.HasValue)
        {
            return "IO 端点不能为 null";
        }

        return GetValidationError(endpoint.Value);
    }
}

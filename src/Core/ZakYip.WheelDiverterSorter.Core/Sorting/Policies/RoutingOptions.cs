using Microsoft.Extensions.Options;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 路径生成配置选项（强类型）
/// </summary>
/// <remarks>
/// 统一管理路径生成、TTL、缓存等相关配置。
/// 通过 IValidateOptions 实现启动时校验。
/// </remarks>
public record RoutingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Routing";

    /// <summary>
    /// 是否启用路径缓存
    /// </summary>
    /// <remarks>
    /// 启用后，相同目标格口的路径将被缓存以提高性能。
    /// 默认启用。
    /// </remarks>
    public bool EnablePathCaching { get; init; } = true;

    /// <summary>
    /// 路径缓存过期时间（秒）
    /// </summary>
    /// <remarks>
    /// 缓存的路径在此时间后失效，重新生成。
    /// 范围：1 ~ 3600 秒，默认 300 秒（5分钟）。
    /// </remarks>
    public int PathCacheExpirationSeconds { get; init; } = 300;

    /// <summary>
    /// 最大路径段数
    /// </summary>
    /// <remarks>
    /// 单条路径允许的最大摆轮段数，超出则视为无效路径。
    /// 范围：1 ~ 100，默认 50。
    /// </remarks>
    public int MaxPathSegments { get; init; } = 50;

    /// <summary>
    /// 默认 TTL（毫秒）
    /// </summary>
    /// <remarks>
    /// 包裹在系统中的默认生存时间。
    /// 范围：1000 ~ 120000 毫秒，默认 30000 毫秒（30秒）。
    /// </remarks>
    public int DefaultTtlMs { get; init; } = 30000;

    /// <summary>
    /// 是否启用路径重规划
    /// </summary>
    /// <remarks>
    /// 启用后，当路径执行失败时尝试从当前位置重新规划路径。
    /// 默认启用。
    /// </remarks>
    public bool EnablePathRerouting { get; init; } = true;

    /// <summary>
    /// 是否启用节点健康检查
    /// </summary>
    /// <remarks>
    /// 启用后，在生成路径时检查节点健康状态，避开不健康的节点。
    /// 默认启用。
    /// </remarks>
    public bool EnableNodeHealthCheck { get; init; } = true;
}

/// <summary>
/// RoutingOptions 校验器
/// </summary>
/// <remarks>
/// 实现 IValidateOptions，在应用启动时校验配置合法性。
/// </remarks>
public class RoutingOptionsValidator : IValidateOptions<RoutingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RoutingOptions options)
    {
        var errors = new List<string>();

        // 校验缓存过期时间
        if (options.PathCacheExpirationSeconds < 1 || options.PathCacheExpirationSeconds > 3600)
        {
            errors.Add("路径缓存过期时间（PathCacheExpirationSeconds）必须在1到3600秒之间");
        }

        // 校验最大路径段数
        if (options.MaxPathSegments < 1 || options.MaxPathSegments > 100)
        {
            errors.Add("最大路径段数（MaxPathSegments）必须在1到100之间");
        }

        // 校验默认TTL
        if (options.DefaultTtlMs < 1000 || options.DefaultTtlMs > 120000)
        {
            errors.Add("默认TTL（DefaultTtlMs）必须在1000到120000毫秒之间");
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}

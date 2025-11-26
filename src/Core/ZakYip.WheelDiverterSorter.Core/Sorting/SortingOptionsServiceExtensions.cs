using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Core.Sorting;

/// <summary>
/// 分拣配置服务扩展方法
/// </summary>
/// <remarks>
/// 提供统一的配置注册入口，确保所有配置通过 IValidateOptions 进行启动时校验
/// </remarks>
public static class SortingOptionsServiceExtensions
{
    /// <summary>
    /// 注册分拣系统强类型配置选项
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托（可选）</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下配置选项及其校验器：
    /// <list type="bullet">
    ///   <item>SortingSystemOptions - 分拣模式、格口配置</item>
    ///   <item>UpstreamConnectionOptions - 上游连接配置</item>
    ///   <item>RoutingOptions - 路径生成配置</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddSortingSystemOptions(
        this IServiceCollection services,
        Action<SortingSystemOptions>? configureOptions = null)
    {
        var builder = services.AddOptions<SortingSystemOptions>();
        
        if (configureOptions != null)
        {
            builder.Configure(configureOptions);
        }
        
        // 注册校验器
        services.AddSingleton<IValidateOptions<SortingSystemOptions>, SortingSystemOptionsValidator>();
        
        return services;
    }

    /// <summary>
    /// 注册上游连接强类型配置选项
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托（可选）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddUpstreamConnectionOptions(
        this IServiceCollection services,
        Action<UpstreamConnectionOptions>? configureOptions = null)
    {
        var builder = services.AddOptions<UpstreamConnectionOptions>();
        
        if (configureOptions != null)
        {
            builder.Configure(configureOptions);
        }
        
        // 注册校验器
        services.AddSingleton<IValidateOptions<UpstreamConnectionOptions>, UpstreamConnectionOptionsValidator>();
        
        return services;
    }

    /// <summary>
    /// 注册路径生成强类型配置选项
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托（可选）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRoutingOptions(
        this IServiceCollection services,
        Action<RoutingOptions>? configureOptions = null)
    {
        var builder = services.AddOptions<RoutingOptions>();
        
        if (configureOptions != null)
        {
            builder.Configure(configureOptions);
        }
        
        // 注册校验器
        services.AddSingleton<IValidateOptions<RoutingOptions>, RoutingOptionsValidator>();
        
        return services;
    }

    /// <summary>
    /// 注册所有分拣相关强类型配置选项
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 便捷方法，一次性注册所有配置选项及其校验器
    /// </remarks>
    public static IServiceCollection AddAllSortingOptions(this IServiceCollection services)
    {
        services.AddSortingSystemOptions();
        services.AddUpstreamConnectionOptions();
        services.AddRoutingOptions();
        
        return services;
    }
}

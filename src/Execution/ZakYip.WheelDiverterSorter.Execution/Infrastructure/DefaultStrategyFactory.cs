using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Execution.Infrastructure;

/// <summary>
/// 默认策略工厂实现，根据 StrategyProfile 创建策略实例
/// Default strategy factory implementation that creates strategy instances from StrategyProfile
/// </summary>
public class DefaultStrategyFactory : IStrategyFactory
{
    private readonly ILogger<DefaultStrategyFactory>? _logger;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="logger">日志记录器（可选）/ Logger (optional)</param>
    public DefaultStrategyFactory(ILogger<DefaultStrategyFactory>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IOverloadHandlingPolicy CreateOverloadPolicy(StrategyProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (profile.OverloadPolicy == null)
        {
            throw new ArgumentException("OverloadPolicy cannot be null in StrategyProfile", nameof(profile));
        }

        _logger?.LogInformation(
            "创建 Overload 策略实例: Profile={ProfileName}, Description={Description}",
            profile.ProfileName, profile.Description);

        // 创建新的 DefaultOverloadHandlingPolicy 实例
        // Create new DefaultOverloadHandlingPolicy instance
        return new DefaultOverloadHandlingPolicy(profile.OverloadPolicy);
    }
}

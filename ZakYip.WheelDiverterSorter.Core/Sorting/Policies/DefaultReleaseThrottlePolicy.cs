using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 默认放包节流策略
/// Default release throttle policy
/// </summary>
public class DefaultReleaseThrottlePolicy : IReleaseThrottlePolicy
{
    private readonly ReleaseThrottleConfiguration _config;

    public DefaultReleaseThrottlePolicy(ReleaseThrottleConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 获取当前放包间隔（毫秒）
    /// </summary>
    public int GetReleaseIntervalMs(CongestionLevel congestionLevel)
    {
        if (!_config.EnableThrottling)
        {
            return _config.NormalReleaseIntervalMs;
        }

        return congestionLevel switch
        {
            CongestionLevel.Normal => _config.NormalReleaseIntervalMs,
            CongestionLevel.Warning => _config.WarningReleaseIntervalMs,
            CongestionLevel.Severe => _config.SevereReleaseIntervalMs,
            _ => _config.NormalReleaseIntervalMs
        };
    }

    /// <summary>
    /// 判断是否允许创建新包裹
    /// </summary>
    public bool AllowNewParcel(CongestionLevel congestionLevel)
    {
        if (!_config.EnableThrottling)
        {
            return true;
        }

        // 严重拥堵且配置为暂停时，不允许新包裹
        if (congestionLevel == CongestionLevel.Severe && _config.ShouldPauseOnSevere)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断当前是否处于暂停状态
    /// </summary>
    public bool IsPaused(CongestionLevel congestionLevel)
    {
        if (!_config.EnableThrottling)
        {
            return false;
        }

        return congestionLevel == CongestionLevel.Severe && _config.ShouldPauseOnSevere;
    }
}

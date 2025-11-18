namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 放包节流配置请求模型
/// </summary>
public class ReleaseThrottleConfigRequest
{
    /// <summary>警告级别延迟阈值（毫秒）</summary>
    public int WarningThresholdLatencyMs { get; set; } = 5000;

    /// <summary>严重级别延迟阈值（毫秒）</summary>
    public int SevereThresholdLatencyMs { get; set; } = 10000;

    /// <summary>警告级别成功率阈值（0.0-1.0）</summary>
    public double WarningThresholdSuccessRate { get; set; } = 0.9;

    /// <summary>严重级别成功率阈值（0.0-1.0）</summary>
    public double SevereThresholdSuccessRate { get; set; } = 0.7;

    /// <summary>警告级别在途包裹数阈值</summary>
    public int WarningThresholdInFlightParcels { get; set; } = 50;

    /// <summary>严重级别在途包裹数阈值</summary>
    public int SevereThresholdInFlightParcels { get; set; } = 100;

    /// <summary>正常状态下的放包间隔（毫秒）</summary>
    public int NormalReleaseIntervalMs { get; set; } = 300;

    /// <summary>警告状态下的放包间隔（毫秒）</summary>
    public int WarningReleaseIntervalMs { get; set; } = 500;

    /// <summary>严重状态下的放包间隔（毫秒）</summary>
    public int SevereReleaseIntervalMs { get; set; } = 1000;

    /// <summary>是否在严重拥堵时暂停放包</summary>
    public bool ShouldPauseOnSevere { get; set; } = false;

    /// <summary>启用节流功能</summary>
    public bool EnableThrottling { get; set; } = true;

    /// <summary>指标采样时间窗口（秒）</summary>
    public int MetricsTimeWindowSeconds { get; set; } = 60;
}

/// <summary>
/// 放包节流配置响应模型
/// </summary>
public class ReleaseThrottleConfigResponse
{
    /// <summary>警告级别延迟阈值（毫秒）</summary>
    public int WarningThresholdLatencyMs { get; set; }

    /// <summary>严重级别延迟阈值（毫秒）</summary>
    public int SevereThresholdLatencyMs { get; set; }

    /// <summary>警告级别成功率阈值（0.0-1.0）</summary>
    public double WarningThresholdSuccessRate { get; set; }

    /// <summary>严重级别成功率阈值（0.0-1.0）</summary>
    public double SevereThresholdSuccessRate { get; set; }

    /// <summary>警告级别在途包裹数阈值</summary>
    public int WarningThresholdInFlightParcels { get; set; }

    /// <summary>严重级别在途包裹数阈值</summary>
    public int SevereThresholdInFlightParcels { get; set; }

    /// <summary>正常状态下的放包间隔（毫秒）</summary>
    public int NormalReleaseIntervalMs { get; set; }

    /// <summary>警告状态下的放包间隔（毫秒）</summary>
    public int WarningReleaseIntervalMs { get; set; }

    /// <summary>严重状态下的放包间隔（毫秒）</summary>
    public int SevereReleaseIntervalMs { get; set; }

    /// <summary>是否在严重拥堵时暂停放包</summary>
    public bool ShouldPauseOnSevere { get; set; }

    /// <summary>启用节流功能</summary>
    public bool EnableThrottling { get; set; }

    /// <summary>指标采样时间窗口（秒）</summary>
    public int MetricsTimeWindowSeconds { get; set; }
}

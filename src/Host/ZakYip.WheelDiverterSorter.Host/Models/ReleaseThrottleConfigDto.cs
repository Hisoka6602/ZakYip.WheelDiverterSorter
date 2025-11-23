namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 放包节流配置请求模型
/// </summary>
public record ReleaseThrottleConfigRequest
{
    /// <summary>警告级别延迟阈值（毫秒）</summary>
    public int WarningThresholdLatencyMs { get; init; } = 5000;

    /// <summary>严重级别延迟阈值（毫秒）</summary>
    public int SevereThresholdLatencyMs { get; init; } = 10000;

    /// <summary>警告级别成功率阈值（0.0-1.0）</summary>
    public double WarningThresholdSuccessRate { get; init; } = 0.9;

    /// <summary>严重级别成功率阈值（0.0-1.0）</summary>
    public double SevereThresholdSuccessRate { get; init; } = 0.7;

    /// <summary>警告级别在途包裹数阈值</summary>
    public int WarningThresholdInFlightParcels { get; init; } = 50;

    /// <summary>严重级别在途包裹数阈值</summary>
    public int SevereThresholdInFlightParcels { get; init; } = 100;

    /// <summary>正常状态下的放包间隔（毫秒）</summary>
    public int NormalReleaseIntervalMs { get; init; } = 300;

    /// <summary>警告状态下的放包间隔（毫秒）</summary>
    public int WarningReleaseIntervalMs { get; init; } = 500;

    /// <summary>严重状态下的放包间隔（毫秒）</summary>
    public int SevereReleaseIntervalMs { get; init; } = 1000;

    /// <summary>是否在严重拥堵时暂停放包</summary>
    public bool ShouldPauseOnSevere { get; init; } = false;

    /// <summary>启用节流功能</summary>
    public bool EnableThrottling { get; init; } = true;

    /// <summary>指标采样时间窗口（秒）</summary>
    public int MetricsTimeWindowSeconds { get; init; } = 60;
}

/// <summary>
/// 放包节流配置响应模型
/// </summary>
public record ReleaseThrottleConfigResponse
{
    /// <summary>警告级别延迟阈值（毫秒）</summary>
    public required int WarningThresholdLatencyMs { get; init; }

    /// <summary>严重级别延迟阈值（毫秒）</summary>
    public required int SevereThresholdLatencyMs { get; init; }

    /// <summary>警告级别成功率阈值（0.0-1.0）</summary>
    public required double WarningThresholdSuccessRate { get; init; }

    /// <summary>严重级别成功率阈值（0.0-1.0）</summary>
    public required double SevereThresholdSuccessRate { get; init; }

    /// <summary>警告级别在途包裹数阈值</summary>
    public required int WarningThresholdInFlightParcels { get; init; }

    /// <summary>严重级别在途包裹数阈值</summary>
    public required int SevereThresholdInFlightParcels { get; init; }

    /// <summary>正常状态下的放包间隔（毫秒）</summary>
    public required int NormalReleaseIntervalMs { get; init; }

    /// <summary>警告状态下的放包间隔（毫秒）</summary>
    public required int WarningReleaseIntervalMs { get; init; }

    /// <summary>严重状态下的放包间隔（毫秒）</summary>
    public required int SevereReleaseIntervalMs { get; init; }

    /// <summary>是否在严重拥堵时暂停放包</summary>
    public required bool ShouldPauseOnSevere { get; init; }

    /// <summary>启用节流功能</summary>
    public required bool EnableThrottling { get; init; }

    /// <summary>指标采样时间窗口（秒）</summary>
    public required int MetricsTimeWindowSeconds { get; init; }
}

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 超载策略配置 DTO
/// </summary>
public class OverloadPolicyDto
{
    /// <summary>是否启用超载检测和处置（默认：true）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>严重拥堵时是否直接路由到异常口（默认：true）</summary>
    public bool ForceExceptionOnSevere { get; set; } = true;

    /// <summary>超过在途包裹容量时是否强制异常（默认：false，仅打标记）</summary>
    public bool ForceExceptionOnOverCapacity { get; set; } = false;

    /// <summary>TTL不足时是否强制异常（默认：true）</summary>
    public bool ForceExceptionOnTimeout { get; set; } = true;

    /// <summary>到达窗口不足时是否强制异常（默认：false）</summary>
    public bool ForceExceptionOnWindowMiss { get; set; } = false;

    /// <summary>最大允许在途包裹数（null表示不限制，默认：null）</summary>
    public int? MaxInFlightParcels { get; set; }

    /// <summary>最小所需剩余TTL（毫秒，默认：500）</summary>
    public double MinRequiredTtlMs { get; set; } = 500;

    /// <summary>最小到达窗口（毫秒，默认：200）</summary>
    public double MinArrivalWindowMs { get; set; } = 200;
}

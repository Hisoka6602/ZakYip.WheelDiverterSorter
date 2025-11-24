namespace ZakYip.WheelDiverterSorter.Core.Utilities;

/// <summary>
/// 本地系统时钟实现 - 提供本地时间访问
/// Local system clock implementation - provides local time access
/// </summary>
/// <remarks>
/// 此实现返回系统本地时间，用于所有业务场景（包裹创建时间、落格时间、指标记录等）。
/// This implementation returns system local time for all business scenarios (parcel creation time, slot time, metrics recording, etc.).
/// </remarks>
public sealed class LocalSystemClock : ISystemClock
{
    /// <inheritdoc/>
    public DateTime LocalNow => DateTime.Now;

    /// <inheritdoc/>
    public DateTimeOffset LocalNowOffset => DateTimeOffset.Now;
}

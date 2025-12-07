namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao.Events;

/// <summary>
/// 设备连接事件参数
/// </summary>
public sealed record DeviceConnectionEventArgs
{
    /// <summary>
    /// 设备地址
    /// </summary>
    public required byte DeviceAddress { get; init; }

    /// <summary>
    /// 客户端地址
    /// </summary>
    public required string ClientAddress { get; init; }

    /// <summary>
    /// 事件时间戳
    /// </summary>
    public required DateTime Timestamp { get; init; }
}

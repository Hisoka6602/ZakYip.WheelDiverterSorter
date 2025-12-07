using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

namespace ZakYip.WheelDiverterSorter.Core.Events.Device;

/// <summary>
/// 设备状态事件参数
/// </summary>
public sealed record DeviceStatusEventArgs
{
    /// <summary>
    /// 设备地址
    /// </summary>
    public required byte DeviceAddress { get; init; }

    /// <summary>
    /// 设备状态
    /// </summary>
    public required ShuDiNiaoDeviceState DeviceState { get; init; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public required DateTime ReceivedAt { get; init; }
}

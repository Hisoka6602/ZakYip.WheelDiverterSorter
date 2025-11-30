using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Events.Hardware;

/// <summary>
/// 摆轮方向变化事件载荷，当分拣设备的摆轮转向方向发生改变时触发
/// </summary>
public readonly record struct DiverterDirectionChangedEventArgs
{
    /// <summary>
    /// 设备标识
    /// </summary>
    public required int DeviceId { get; init; }

    /// <summary>
    /// 摆轮转向方向
    /// </summary>
    public required DiverterDirection Direction { get; init; }

    /// <summary>
    /// 方向变化时间
    /// </summary>
    public required DateTimeOffset ChangedAt { get; init; }
}

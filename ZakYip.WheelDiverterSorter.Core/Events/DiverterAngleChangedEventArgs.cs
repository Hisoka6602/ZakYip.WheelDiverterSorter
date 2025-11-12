namespace ZakYip.WheelDiverterSorter.Core.Events;

/// <summary>
/// 摆轮角度变化事件载荷，当分拣设备的摆轮角度发生改变时触发
/// </summary>
public record DiverterAngleChangedEventArgs
{
    /// <summary>
    /// 设备标识
    /// </summary>
    public required int DeviceId { get; init; }

    /// <summary>
    /// 摆轮角度
    /// </summary>
    public required DiverterAngle Angle { get; init; }

    /// <summary>
    /// 角度变化时间
    /// </summary>
    public required DateTimeOffset ChangedAt { get; init; }
}

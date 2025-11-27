namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// IO端口状态变化事件参数 - 统一硬件事件模型
/// IO Port Changed Event Args - Unified Hardware Event Model
/// </summary>
/// <remarks>
/// <para>当IO端口电平状态发生变化时触发此事件。</para>
/// <para>此事件只描述"设备状态变化"，不包含拓扑信息（如传感器逻辑名称等）。</para>
/// </remarks>
public readonly record struct IoPortChangedEventArgs
{
    /// <summary>
    /// IO组名称
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// 端口编号
    /// </summary>
    public required int PortNumber { get; init; }

    /// <summary>
    /// 新的电平状态（true=高电平/ON，false=低电平/OFF）
    /// </summary>
    public required bool IsOn { get; init; }

    /// <summary>
    /// 状态变化的时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// 摆轮设备状态变化事件参数 - 统一硬件事件模型
/// Wheel Diverter State Changed Event Args - Unified Hardware Event Model
/// </summary>
/// <remarks>
/// <para>当摆轮设备状态发生变化时触发此事件。</para>
/// <para>此事件只描述"设备状态变化"，不包含拓扑信息（如格口号等）。</para>
/// </remarks>
public readonly record struct WheelDiverterStateChangedEventArgs
{
    /// <summary>
    /// 设备标识符
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// 新的设备状态
    /// </summary>
    public required WheelDiverterState NewState { get; init; }

    /// <summary>
    /// 之前的设备状态（可选）
    /// </summary>
    public WheelDiverterState? PreviousState { get; init; }

    /// <summary>
    /// 状态变化的时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// 线体段状态变化事件参数 - 统一硬件事件模型
/// Conveyor Segment State Changed Event Args - Unified Hardware Event Model
/// </summary>
/// <remarks>
/// <para>当线体段运行状态发生变化时触发此事件。</para>
/// <para>此事件只描述"设备状态变化"，不包含拓扑信息。</para>
/// </remarks>
public readonly record struct ConveyorSegmentStateChangedEventArgs
{
    /// <summary>
    /// 线体段标识符
    /// </summary>
    public required string SegmentId { get; init; }

    /// <summary>
    /// 新的运行状态
    /// </summary>
    public required Enums.Hardware.ConveyorSegmentState NewState { get; init; }

    /// <summary>
    /// 之前的运行状态（可选）
    /// </summary>
    public Enums.Hardware.ConveyorSegmentState? PreviousState { get; init; }

    /// <summary>
    /// 当前速度（毫米/秒），可选
    /// </summary>
    public decimal? CurrentSpeedMmPerSec { get; init; }

    /// <summary>
    /// 状态变化的时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

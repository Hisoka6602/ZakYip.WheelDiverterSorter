namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// IO 联动配置选项。
/// 定义在不同系统状态下要联动的 IO 端口组。
/// </summary>
public sealed record class IoLinkageOptions
{
    /// <summary>
    /// 是否启用 IO 联动功能。
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 运行中状态时联动的 IO 点列表。
    /// 例如：运行中时将 IO 3、5、6 设置为低电平。
    /// </summary>
    public List<IoLinkagePoint> RunningStateIos { get; init; } = new();

    /// <summary>
    /// 停止/复位状态时联动的 IO 点列表。
    /// 例如：停止/复位时将 IO 3、5、6 设置为高电平。
    /// </summary>
    public List<IoLinkagePoint> StoppedStateIos { get; init; } = new();

    /// <summary>
    /// 急停状态时联动的 IO 点列表。
    /// 例如：急停时将某些 IO 设置为特定电平以紧急停止设备。
    /// </summary>
    public List<IoLinkagePoint> EmergencyStopStateIos { get; init; } = new();

    /// <summary>
    /// 上游连接异常状态时联动的 IO 点列表。
    /// 例如：上游连接异常时将某些 IO 设置为特定电平以告警。
    /// </summary>
    public List<IoLinkagePoint> UpstreamConnectionExceptionStateIos { get; init; } = new();

    /// <summary>
    /// 摆轮异常状态时联动的 IO 点列表。
    /// 例如：摆轮异常时将某些 IO 设置为特定电平以告警。
    /// </summary>
    public List<IoLinkagePoint> DiverterExceptionStateIos { get; init; } = new();

    /// <summary>
    /// 运行前预警结束后联动的 IO 点列表。
    /// 当系统按下启动按钮后，先进行运行前预警（preStartWarning），
    /// 等待 durationSeconds 秒后，预警结束，此时触发这些 IO 点。
    /// </summary>
    public List<IoLinkagePoint> PostPreStartWarningStateIos { get; init; } = new();

    /// <summary>
    /// 摆轮断联/异常状态时联动的 IO 点列表。
    /// 当摆轮首次连接成功后，如果摆轮断联或发生异常，将触发这些 IO 点。
    /// </summary>
    public List<IoLinkagePoint> WheelDiverterDisconnectedStateIos { get; init; } = new();
}

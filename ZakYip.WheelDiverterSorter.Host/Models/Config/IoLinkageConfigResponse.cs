namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// IO 联动配置响应模型
/// </summary>
public sealed record class IoLinkageConfigResponse
{
    /// <summary>
    /// 是否启用 IO 联动功能
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// 运行中状态时联动的 IO 点列表
    /// </summary>
    public required List<IoLinkagePointResponse> RunningStateIos { get; init; }

    /// <summary>
    /// 停止/复位状态时联动的 IO 点列表
    /// </summary>
    public required List<IoLinkagePointResponse> StoppedStateIos { get; init; }
}

/// <summary>
/// IO 联动点配置响应模型
/// </summary>
public sealed record class IoLinkagePointResponse
{
    /// <summary>
    /// IO 端口编号
    /// </summary>
    public required int BitNumber { get; init; }

    /// <summary>
    /// 目标电平状态（ActiveHigh=高电平，ActiveLow=低电平）
    /// </summary>
    public required string Level { get; init; }
}

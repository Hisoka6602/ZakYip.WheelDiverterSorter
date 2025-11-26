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

    /// <summary>
    /// 急停状态时联动的 IO 点列表
    /// </summary>
    public required List<IoLinkagePointResponse> EmergencyStopStateIos { get; init; }

    /// <summary>
    /// 上游连接异常状态时联动的 IO 点列表
    /// </summary>
    public required List<IoLinkagePointResponse> UpstreamConnectionExceptionStateIos { get; init; }

    /// <summary>
    /// 摆轮异常状态时联动的 IO 点列表
    /// </summary>
    public required List<IoLinkagePointResponse> DiverterExceptionStateIos { get; init; }

    /// <summary>
    /// 运行前预警结束后联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 当系统按下启动按钮后，先进行运行前预警（preStartWarning），
    /// 等待 durationSeconds 秒后，预警结束，此时触发这些 IO 点。
    /// 用于在预警结束后通知外部设备可以开始工作。
    /// </remarks>
    public required List<IoLinkagePointResponse> PostPreStartWarningStateIos { get; init; }
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

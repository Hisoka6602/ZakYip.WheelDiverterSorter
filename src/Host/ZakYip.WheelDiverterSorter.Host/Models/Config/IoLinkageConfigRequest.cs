using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// IO 联动配置请求模型
/// </summary>
public sealed record class IoLinkageConfigRequest
{
    /// <summary>
    /// 是否启用 IO 联动功能
    /// </summary>
    [Required(ErrorMessage = "Enabled 不能为空")]
    public required bool Enabled { get; init; }

    /// <summary>
    /// 就绪状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "ReadyStateIos 不能为空")]
    public required List<IoLinkagePointRequest> ReadyStateIos { get; init; }

    /// <summary>
    /// 运行中状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "RunningStateIos 不能为空")]
    public required List<IoLinkagePointRequest> RunningStateIos { get; init; }

    /// <summary>
    /// 停止/复位状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "StoppedStateIos 不能为空")]
    public required List<IoLinkagePointRequest> StoppedStateIos { get; init; }

    /// <summary>
    /// 急停状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "EmergencyStopStateIos 不能为空")]
    public required List<IoLinkagePointRequest> EmergencyStopStateIos { get; init; }

    /// <summary>
    /// 上游连接异常状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "UpstreamConnectionExceptionStateIos 不能为空")]
    public required List<IoLinkagePointRequest> UpstreamConnectionExceptionStateIos { get; init; }

    /// <summary>
    /// 摆轮异常状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "DiverterExceptionStateIos 不能为空")]
    public required List<IoLinkagePointRequest> DiverterExceptionStateIos { get; init; }

    /// <summary>
    /// 运行前预警结束后联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 当系统按下启动按钮后，先进行运行前预警（preStartWarning），
    /// 等待 durationSeconds 秒后，预警结束，此时触发这些 IO 点。
    /// 用于在预警结束后通知外部设备可以开始工作。
    /// </remarks>
    [Required(ErrorMessage = "PostPreStartWarningStateIos 不能为空")]
    public required List<IoLinkagePointRequest> PostPreStartWarningStateIos { get; init; }

    /// <summary>
    /// 摆轮断联/异常状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 当摆轮首次连接成功后，如果摆轮断联或发生异常，将触发这些 IO 点。
    /// 用于通知外部设备摆轮出现连接问题或异常情况。
    /// 注意：只有在摆轮首次连接成功后才会触发此联动。
    /// </remarks>
    [Required(ErrorMessage = "WheelDiverterDisconnectedStateIos 不能为空")]
    public required List<IoLinkagePointRequest> WheelDiverterDisconnectedStateIos { get; init; }
}

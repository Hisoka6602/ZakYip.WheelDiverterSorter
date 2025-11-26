namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 雷赛控制器配置选项
/// </summary>
/// <remarks>
/// 基于 ZakYip.Singulation 项目的 LeadshineLtdmcBusAdapter 实现。
/// 支持以太网模式（需要 ControllerIp）和本地 PCI 模式（ControllerIp 为空）。
/// </remarks>
public class LeadshineOptions
{
    /// <summary>
    /// 控制器IP地址（以太网模式）
    /// </summary>
    /// <remarks>
    /// 以太网模式需要配置控制器的IP地址。
    /// 如果为空或null，则使用本地PCI模式（dmc_board_init）。
    /// 如果配置了IP，则使用以太网模式（dmc_board_init_eth）。
    /// 默认值为 "192.168.5.11"。
    /// </remarks>
    /// <example>192.168.5.11</example>
    public string? ControllerIp { get; set; } = "192.168.5.11";

    /// <summary>
    /// 控制器卡号
    /// </summary>
    /// <remarks>
    /// 默认值为 8。
    /// </remarks>
    /// <example>8</example>
    public ushort CardNo { get; set; } = 8;

    /// <summary>
    /// 端口号（CAN/EtherCAT端口编号）
    /// </summary>
    /// <remarks>
    /// 默认值为 2。
    /// </remarks>
    /// <example>2</example>
    public ushort PortNo { get; set; } = 2;

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public List<LeadshineDiverterConfigDto> Diverters { get; set; } = new();
}

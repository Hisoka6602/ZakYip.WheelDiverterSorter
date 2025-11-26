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
    /// </remarks>
    /// <example>192.168.1.100</example>
    public string? ControllerIp { get; set; }

    /// <summary>
    /// 控制器卡号
    /// </summary>
    /// <example>0</example>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 端口号（CAN/EtherCAT端口编号）
    /// </summary>
    /// <example>0</example>
    public ushort PortNo { get; set; } = 0;

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public List<LeadshineDiverterConfigDto> Diverters { get; set; } = new();
}

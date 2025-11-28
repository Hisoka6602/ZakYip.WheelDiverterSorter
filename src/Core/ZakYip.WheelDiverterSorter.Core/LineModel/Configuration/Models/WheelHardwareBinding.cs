namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 摆轮硬件绑定配置
/// </summary>
/// <remarks>
/// 将逻辑摆轮节点与物理硬件绑定，包含两部分配置：
/// 
/// 1. **IO驱动配置**（用于传感器信号和继电器控制）：
///    - IoDriverType: IO驱动厂商类型（Leadshine/Siemens/Mitsubishi/Omron）
///    - IoAddress: IO板地址
///    - IoChannel: IO通道号
///    - OutputStartBit: 输出起始位
///    - FeedbackInputBit: 反馈输入位
/// 
/// 2. **摆轮驱动配置**（用于摆轮方向控制）：
///    - WheelDriverType: 摆轮驱动厂商类型（ShuDiNiao/Modi）
///    - WheelDriverHost: 摆轮驱动TCP主机地址
///    - WheelDriverPort: 摆轮驱动TCP端口
///    - WheelDeviceAddress: 摆轮设备地址
/// 
/// IO驱动和摆轮驱动共同组成完整的摆轮控制拓扑。
/// </remarks>
public record class WheelHardwareBinding
{
    /// <summary>
    /// 摆轮节点ID（逻辑标识）
    /// </summary>
    public required string WheelNodeId { get; init; }

    /// <summary>
    /// 摆轮显示名称
    /// </summary>
    public required string WheelName { get; init; }

    /// <summary>
    /// 驱动器ID（数字ID，用于内部标识）
    /// </summary>
    public required long DriverId { get; init; }

    /// <summary>
    /// 驱动器名称（可选，用于显示）
    /// </summary>
    public string? DriverName { get; init; }

    // ========== IO驱动配置（用于传感器信号和继电器控制） ==========

    /// <summary>
    /// IO驱动厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值: Leadshine（雷赛）、Siemens（西门子）、Mitsubishi（三菱）、Omron（欧姆龙）
    /// </remarks>
    public string? IoDriverType { get; init; }

    /// <summary>
    /// IO板地址
    /// </summary>
    /// <remarks>
    /// 例如：192.168.1.100 或雷赛卡号 0
    /// </remarks>
    public string? IoAddress { get; init; }

    /// <summary>
    /// IO通道号
    /// </summary>
    public int? IoChannel { get; init; }

    /// <summary>
    /// 输出起始位（用于继电器控制）
    /// </summary>
    public int? OutputStartBit { get; init; }

    /// <summary>
    /// 反馈输入位（用于传感器信号读取）
    /// </summary>
    public int? FeedbackInputBit { get; init; }

    // ========== 摆轮驱动配置（用于摆轮方向控制） ==========

    /// <summary>
    /// 摆轮驱动厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值: ShuDiNiao（数递鸟）、Modi（莫迪）
    /// </remarks>
    public string? WheelDriverType { get; init; }

    /// <summary>
    /// 摆轮驱动TCP主机地址
    /// </summary>
    /// <remarks>
    /// 例如：192.168.0.100
    /// </remarks>
    public string? WheelDriverHost { get; init; }

    /// <summary>
    /// 摆轮驱动TCP端口
    /// </summary>
    /// <remarks>
    /// 例如：2000（数递鸟）或 8000（莫迪）
    /// </remarks>
    public int? WheelDriverPort { get; init; }

    /// <summary>
    /// 摆轮设备地址
    /// </summary>
    /// <remarks>
    /// 数递鸟: 0x51=1号设备, 0x52=2号设备, ...
    /// 莫迪: 设备编号
    /// </remarks>
    public int? WheelDeviceAddress { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }
}

/// <summary>
/// 摆轮硬件绑定配置集合
/// </summary>
/// <remarks>
/// 存储所有摆轮节点的硬件绑定配置，形成完整的摆轮控制拓扑。
/// 每个绑定包含IO驱动和摆轮驱动两部分配置。
/// </remarks>
public record class WheelBindingsConfig
{
    /// <summary>
    /// 配置ID（LiteDB自动生成）
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 配置名称
    /// </summary>
    public string ConfigName { get; set; } = "wheel-bindings";

    /// <summary>
    /// 摆轮绑定列表
    /// </summary>
    public required List<WheelHardwareBinding> Bindings { get; init; }

    /// <summary>
    /// 创建时间（本地时间）
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间（本地时间）
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}



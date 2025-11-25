using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 摆轮硬件绑定请求
/// </summary>
/// <remarks>
/// 将逻辑摆轮节点与物理硬件绑定，包含IO驱动和摆轮驱动两部分配置。
/// </remarks>
public record WheelHardwareBindingRequest
{
    /// <summary>
    /// 摆轮节点ID（逻辑标识）
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string WheelNodeId { get; init; }

    /// <summary>
    /// 摆轮显示名称
    /// </summary>
    [Required]
    [StringLength(200)]
    public required string WheelName { get; init; }

    /// <summary>
    /// 驱动器ID（数字ID，用于内部标识）
    /// </summary>
    [Required]
    [Range(1, long.MaxValue)]
    public required long DriverId { get; init; }

    /// <summary>
    /// 驱动器名称（可选，用于显示）
    /// </summary>
    [StringLength(200)]
    public string? DriverName { get; init; }

    // ========== IO驱动配置（用于传感器信号和继电器控制） ==========

    /// <summary>
    /// IO驱动厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值: Leadshine（雷赛）、Siemens（西门子）、Mitsubishi（三菱）、Omron（欧姆龙）
    /// </remarks>
    /// <example>Leadshine</example>
    [StringLength(50)]
    public string? IoDriverType { get; init; }

    /// <summary>
    /// IO板地址
    /// </summary>
    /// <remarks>
    /// 例如：192.168.1.100 或雷赛卡号 0
    /// </remarks>
    [StringLength(100)]
    public string? IoAddress { get; init; }

    /// <summary>
    /// IO通道号
    /// </summary>
    [Range(0, 1000)]
    public int? IoChannel { get; init; }

    /// <summary>
    /// 输出起始位（用于继电器控制）
    /// </summary>
    /// <example>0</example>
    [Range(0, 1000)]
    public int? OutputStartBit { get; init; }

    /// <summary>
    /// 反馈输入位（用于传感器信号读取）
    /// </summary>
    /// <example>10</example>
    [Range(0, 1000)]
    public int? FeedbackInputBit { get; init; }

    // ========== 摆轮驱动配置（用于摆轮方向控制） ==========

    /// <summary>
    /// 摆轮驱动厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值: ShuDiNiao（数递鸟）、Modi（莫迪）
    /// </remarks>
    /// <example>ShuDiNiao</example>
    [StringLength(50)]
    public string? WheelDriverType { get; init; }

    /// <summary>
    /// 摆轮驱动TCP主机地址
    /// </summary>
    /// <remarks>
    /// 例如：192.168.0.100
    /// </remarks>
    /// <example>192.168.0.100</example>
    [StringLength(255)]
    public string? WheelDriverHost { get; init; }

    /// <summary>
    /// 摆轮驱动TCP端口
    /// </summary>
    /// <remarks>
    /// 例如：2000（数递鸟）或 8000（莫迪）
    /// </remarks>
    /// <example>2000</example>
    [Range(1, 65535)]
    public int? WheelDriverPort { get; init; }

    /// <summary>
    /// 摆轮设备地址
    /// </summary>
    /// <remarks>
    /// 数递鸟: 0x51=1号设备(81), 0x52=2号设备(82), ...
    /// 莫迪: 设备编号
    /// </remarks>
    /// <example>81</example>
    [Range(0, 255)]
    public int? WheelDeviceAddress { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 备注信息
    /// </summary>
    [StringLength(500)]
    public string? Remarks { get; init; }
}

/// <summary>
/// 摆轮硬件绑定配置请求
/// </summary>
/// <remarks>
/// 包含所有摆轮节点的硬件绑定配置，形成完整的摆轮控制拓扑。
/// 每个绑定同时包含IO驱动和摆轮驱动两部分配置。
/// </remarks>
public record WheelBindingsRequest
{
    /// <summary>
    /// 摆轮绑定列表
    /// </summary>
    [Required]
    public required List<WheelHardwareBindingRequest> Bindings { get; init; }
}

/// <summary>
/// 摆轮硬件绑定配置响应
/// </summary>
public record WheelBindingsResponse
{
    public required List<WheelHardwareBindingRequest> Bindings { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

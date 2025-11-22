using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 摆轮硬件绑定请求
/// </summary>
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
    /// 驱动器ID（数字ID）
    /// </summary>
    [Required]
    [Range(1, 10000)]
    public required int DriverId { get; init; }

    /// <summary>
    /// 驱动器名称（可选）
    /// </summary>
    [StringLength(200)]
    public string? DriverName { get; init; }

    /// <summary>
    /// IO板地址（可选）
    /// </summary>
    [StringLength(100)]
    public string? IoAddress { get; init; }

    /// <summary>
    /// IO通道号（可选）
    /// </summary>
    [Range(0, 1000)]
    public int? IoChannel { get; init; }

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

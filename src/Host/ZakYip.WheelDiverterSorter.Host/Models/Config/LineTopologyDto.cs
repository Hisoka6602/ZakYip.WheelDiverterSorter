using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 线体段配置请求
/// </summary>
public record LineSegmentRequest
{
    /// <summary>
    /// 线体段唯一标识符
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string SegmentId { get; init; }

    /// <summary>
    /// 起始节点ID
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string FromNodeId { get; init; }

    /// <summary>
    /// 目标节点ID
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string ToNodeId { get; init; }

    /// <summary>
    /// 线体段物理长度（单位：毫米）
    /// </summary>
    [Required]
    [Range(0.1, 100000.0)]
    public required double LengthMm { get; init; }

    /// <summary>
    /// 标称运行速度（单位：毫米/秒）
    /// </summary>
    [Required]
    [Range(1.0, 10000.0)]
    public required double NominalSpeedMmPerSec { get; init; }

    /// <summary>
    /// 线体段描述（可选）
    /// </summary>
    [StringLength(500)]
    public string? Description { get; init; }
}

/// <summary>
/// 摆轮节点配置请求
/// </summary>
public record WheelNodeRequest
{
    /// <summary>
    /// 节点唯一标识符
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点显示名称
    /// </summary>
    [Required]
    [StringLength(200)]
    public required string NodeName { get; init; }

    /// <summary>
    /// 物理位置索引（从入口开始的顺序）
    /// </summary>
    [Required]
    [Range(0, 1000)]
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 左侧出口是否连接格口
    /// </summary>
    public bool HasLeftChute { get; init; }

    /// <summary>
    /// 右侧出口是否连接格口
    /// </summary>
    public bool HasRightChute { get; init; }

    /// <summary>
    /// 备注信息
    /// </summary>
    [StringLength(500)]
    public string? Remarks { get; init; }
}

/// <summary>
/// 格口配置请求
/// </summary>
public record ChuteConfigRequest
{
    /// <summary>
    /// 格口唯一标识符
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string ChuteId { get; init; }

    /// <summary>
    /// 格口显示名称
    /// </summary>
    [Required]
    [StringLength(200)]
    public required string ChuteName { get; init; }

    /// <summary>
    /// 是否为异常格口
    /// </summary>
    public bool IsExceptionChute { get; init; }

    /// <summary>
    /// 绑定的节点ID
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string BoundNodeId { get; init; }

    /// <summary>
    /// 绑定的节点方向（Left/Right/Straight）
    /// </summary>
    [Required]
    [StringLength(20)]
    public required string BoundDirection { get; init; }

    /// <summary>
    /// 落格偏移距离（毫米）
    /// </summary>
    [Range(0.0, 10000.0)]
    public double DropOffsetMm { get; init; } = 0.0;

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
/// 线体拓扑配置请求
/// </summary>
public record LineTopologyRequest
{
    /// <summary>
    /// 拓扑配置名称
    /// </summary>
    [Required]
    [StringLength(200)]
    public required string TopologyName { get; init; }

    /// <summary>
    /// 拓扑描述
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; init; }

    /// <summary>
    /// 摆轮节点配置列表
    /// </summary>
    [Required]
    public required List<WheelNodeRequest> WheelNodes { get; init; }

    /// <summary>
    /// 格口配置列表
    /// </summary>
    [Required]
    public required List<ChuteConfigRequest> Chutes { get; init; }

    /// <summary>
    /// 线体段配置列表
    /// </summary>
    public List<LineSegmentRequest>? LineSegments { get; init; }

    /// <summary>
    /// 入口传感器ID
    /// </summary>
    [StringLength(100)]
    public string? EntrySensorId { get; init; }

    /// <summary>
    /// 出口传感器ID
    /// </summary>
    [StringLength(100)]
    public string? ExitSensorId { get; init; }

    /// <summary>
    /// 默认线速（毫米/秒）
    /// </summary>
    [Range(1.0, 10000.0)]
    public decimal DefaultLineSpeedMmps { get; init; } = 500m;
}

/// <summary>
/// 线体拓扑配置响应
/// </summary>
public record LineTopologyResponse
{
    public required string TopologyId { get; init; }
    public required string TopologyName { get; init; }
    public string? Description { get; init; }
    public required List<WheelNodeRequest> WheelNodes { get; init; }
    public required List<ChuteConfigRequest> Chutes { get; init; }
    public required List<LineSegmentRequest> LineSegments { get; init; }
    public string? EntrySensorId { get; init; }
    public string? ExitSensorId { get; init; }
    public decimal DefaultLineSpeedMmps { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

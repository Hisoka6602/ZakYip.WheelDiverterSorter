using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 线体段配置请求
/// </summary>
/// <remarks>
/// 定义两个感应IO之间的线体段配置。
/// 
/// **拓扑规则**：
/// - 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）
/// - 最后一段线体的终点IO Id设为0，表示已到达末端
/// - 中间段的起点/终点IO通常是摆轮前感应IO（WheelFront类型）
/// </remarks>
public record LineSegmentRequest
{
    /// <summary>
    /// 线体段唯一标识符
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string SegmentId { get; init; }

    /// <summary>
    /// 线体段显示名称
    /// </summary>
    [StringLength(200)]
    public string? SegmentName { get; init; }

    /// <summary>
    /// 起点感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）
    /// </remarks>
    [Required]
    [Range(0, long.MaxValue)]
    public required long StartIoId { get; init; }

    /// <summary>
    /// 终点感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// - 中间线体段：通常是摆轮前感应IO（WheelFront类型）
    /// - 最后一段线体：设为0，表示已到达末端
    /// </remarks>
    [Required]
    [Range(0, long.MaxValue)]
    public required long EndIoId { get; init; }

    /// <summary>
    /// 线体段物理长度（单位：毫米）
    /// </summary>
    [Required]
    [Range(0.1, 100000.0)]
    public required double LengthMm { get; init; }

    /// <summary>
    /// 线体运行速度（单位：毫米/秒）
    /// </summary>
    [Required]
    [Range(1.0, 10000.0)]
    public required double SpeedMmPerSec { get; init; }

    /// <summary>
    /// 线体段描述（可选）
    /// </summary>
    [StringLength(500)]
    public string? Description { get; init; }

    #region 向后兼容 - 废弃字段

    /// <summary>
    /// [废弃] 起始节点ID - 已改用 StartIoId
    /// </summary>
    [Obsolete("请使用 StartIoId")]
    [StringLength(100)]
    public string? FromNodeId { get; init; }

    /// <summary>
    /// [废弃] 目标节点ID - 已改用 EndIoId
    /// </summary>
    [Obsolete("请使用 EndIoId")]
    [StringLength(100)]
    public string? ToNodeId { get; init; }

    /// <summary>
    /// [废弃] 标称运行速度 - 已改用 SpeedMmPerSec
    /// </summary>
    [Obsolete("请使用 SpeedMmPerSec")]
    [Range(1.0, 10000.0)]
    public double? NominalSpeedMmPerSec { get; init; }

    #endregion
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
    /// 摆轮前感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// 关联的摆轮前感应IO（WheelFront类型），用于检测包裹即将到达摆轮
    /// </remarks>
    [Range(0, long.MaxValue)]
    public long? FrontIoId { get; init; }

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
    /// 锁格感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// 关联的锁格感应IO（ChuteLock类型），用于检测包裹落入格口
    /// </remarks>
    [Range(0, long.MaxValue)]
    public long? LockIoId { get; init; }

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
/// <remarks>
/// 定义完整的线体拓扑结构。
/// 
/// **拓扑组成规则**：
/// 一个最简的摆轮分拣拓扑由以下元素组成：
/// - 创建包裹感应IO -> 线体段 -> 摆轮 -> 格口（摆轮方向=格口）
/// 
/// **多段线体规则**：
/// - 第一段线体的起点IO必须是创建包裹的IO（ParcelCreation类型）
/// - 最后一段线体的终点IO Id应该是0（表示已到达末端）
/// </remarks>
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
    /// <remarks>
    /// 定义所有线体段，每段由起点IO和终点IO定义
    /// </remarks>
    public List<LineSegmentRequest>? LineSegments { get; init; }

    /// <summary>
    /// 默认线速（毫米/秒）
    /// </summary>
    [Range(1.0, 10000.0)]
    public decimal DefaultLineSpeedMmps { get; init; } = 500m;

    #region 向后兼容 - 废弃字段

    /// <summary>
    /// [废弃] 入口传感器ID - 使用 LineSegments 中第一段的 StartIoId
    /// </summary>
    [Obsolete("请使用 LineSegments 中第一段的 StartIoId")]
    [StringLength(100)]
    public string? EntrySensorId { get; init; }

    /// <summary>
    /// [废弃] 出口传感器ID - 使用 LineSegments 中最后一段的 EndIoId
    /// </summary>
    [Obsolete("请使用 LineSegments 中最后一段的 EndIoId")]
    [StringLength(100)]
    public string? ExitSensorId { get; init; }

    #endregion
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
    public decimal DefaultLineSpeedMmps { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    #region 向后兼容 - 废弃字段

    /// <summary>
    /// [废弃] 入口传感器ID
    /// </summary>
    [Obsolete("请使用 LineSegments 中第一段的 StartIoId")]
    public string? EntrySensorId { get; init; }

    /// <summary>
    /// [废弃] 出口传感器ID
    /// </summary>
    [Obsolete("请使用 LineSegments 中最后一段的 EndIoId")]
    public string? ExitSensorId { get; init; }

    #endregion
}

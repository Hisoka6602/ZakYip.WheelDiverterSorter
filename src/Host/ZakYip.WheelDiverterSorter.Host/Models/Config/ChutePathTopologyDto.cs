using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 摆轮路径节点请求
/// </summary>
/// <remarks>
/// <para>描述单个摆轮在路径中的配置，通过引用已配置的ID来组织：</para>
/// <list type="bullet">
/// <item>DiverterId - 引用摆轮设备配置中的摆轮ID</item>
/// <item>SegmentId - 引用线体段配置中的线体段ID</item>
/// <item>FrontSensorId - 引用感应IO配置中的传感器ID（可选）</item>
/// <item>LeftChuteIds/RightChuteIds - 格口ID列表</item>
/// </list>
/// </remarks>
public record DiverterPathNodeRequest
{
    /// <summary>
    /// 摆轮ID（引用摆轮设备配置中的摆轮ID）
    /// </summary>
    /// <example>1</example>
    [Required]
    [Range(1, long.MaxValue)]
    public required long DiverterId { get; init; }

    /// <summary>
    /// 摆轮显示名称（可选）
    /// </summary>
    /// <example>摆轮D1</example>
    [StringLength(200)]
    public string? DiverterName { get; init; }

    /// <summary>
    /// 物理位置索引（从入口开始的顺序，从1开始）
    /// </summary>
    /// <example>1</example>
    [Required]
    [Range(1, 1000)]
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 前置线体段ID（引用线体段配置中的SegmentId）
    /// </summary>
    /// <remarks>
    /// 从上一个节点（入口或上一个摆轮）到本摆轮的线体段
    /// </remarks>
    /// <example>1</example>
    [Required]
    [Range(1, long.MaxValue)]
    public required long SegmentId { get; init; }

    /// <summary>
    /// 摆轮前感应IO的ID（引用感应IO配置中的SensorId，可选）
    /// </summary>
    /// <remarks>
    /// 类型应为 WheelFront，用于检测包裹即将到达摆轮
    /// </remarks>
    /// <example>2</example>
    [Range(0, long.MaxValue)]
    public long? FrontSensorId { get; init; }

    /// <summary>
    /// 左侧格口ID列表
    /// </summary>
    /// <remarks>
    /// 摆轮左转时可分拣到的格口ID列表
    /// </remarks>
    /// <example>[2, 3]</example>
    public List<long>? LeftChuteIds { get; init; }

    /// <summary>
    /// 右侧格口ID列表
    /// </summary>
    /// <remarks>
    /// 摆轮右转时可分拣到的格口ID列表
    /// </remarks>
    /// <example>[1, 4]</example>
    public List<long>? RightChuteIds { get; init; }

    /// <summary>
    /// 备注信息
    /// </summary>
    [StringLength(500)]
    public string? Remarks { get; init; }
}

/// <summary>
/// 格口路径拓扑配置请求
/// </summary>
/// <remarks>
/// <para>定义从入口到各个格口的完整路径拓扑结构。</para>
/// <para>本配置通过引用其他配置中已定义的ID来组织路径关系：</para>
/// <list type="bullet">
/// <item>EntrySensorId - 引用感应IO配置中的传感器ID（ParcelCreation类型）</item>
/// <item>DiverterNodes - 摆轮路径节点列表，每个节点引用摆轮ID、线体段ID等</item>
/// <item>ExceptionChuteId - 异常格口ID</item>
/// </list>
/// 
/// <para><b>拓扑结构示例：</b></para>
/// <code>
///       格口B     格口D     格口F
///         ↑         ↑         ↑
/// 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(默认异常口)
///   ↓     ↓         ↓         ↓
/// 传感器  格口A      格口C     格口E
/// </code>
/// </remarks>
public record ChutePathTopologyRequest
{
    /// <summary>
    /// 拓扑配置名称
    /// </summary>
    /// <example>标准格口路径拓扑</example>
    [Required]
    [StringLength(200)]
    public required string TopologyName { get; init; }

    /// <summary>
    /// 拓扑描述
    /// </summary>
    /// <example>3摆轮6格口的标准配置</example>
    [StringLength(1000)]
    public string? Description { get; init; }

    /// <summary>
    /// 入口传感器ID（引用感应IO配置中类型为ParcelCreation的传感器）
    /// </summary>
    /// <example>1</example>
    [Required]
    [Range(1, long.MaxValue)]
    public required long EntrySensorId { get; init; }

    /// <summary>
    /// 摆轮路径节点列表（按物理位置顺序排列）
    /// </summary>
    [Required]
    public required List<DiverterPathNodeRequest> DiverterNodes { get; init; }

    /// <summary>
    /// 末端异常格口ID
    /// </summary>
    /// <remarks>
    /// 当包裹无法分拣到任何目标格口时，将被导向此异常格口
    /// </remarks>
    /// <example>999</example>
    [Required]
    [Range(1, long.MaxValue)]
    public required long ExceptionChuteId { get; init; }
}

/// <summary>
/// 格口路径拓扑配置响应
/// </summary>
/// <remarks>
/// 包含完整的格口路径拓扑配置信息
/// </remarks>
public record ChutePathTopologyResponse
{
    /// <summary>
    /// 拓扑配置唯一标识符
    /// </summary>
    /// <example>default</example>
    public required string TopologyId { get; init; }

    /// <summary>
    /// 拓扑配置名称
    /// </summary>
    /// <example>标准格口路径拓扑</example>
    public required string TopologyName { get; init; }

    /// <summary>
    /// 拓扑描述
    /// </summary>
    /// <example>3摆轮6格口的标准配置</example>
    public string? Description { get; init; }

    /// <summary>
    /// 入口传感器ID
    /// </summary>
    /// <example>1</example>
    public required long EntrySensorId { get; init; }

    /// <summary>
    /// 摆轮路径节点列表
    /// </summary>
    public required List<DiverterPathNodeRequest> DiverterNodes { get; init; }

    /// <summary>
    /// 末端异常格口ID
    /// </summary>
    /// <example>999</example>
    public required long ExceptionChuteId { get; init; }

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

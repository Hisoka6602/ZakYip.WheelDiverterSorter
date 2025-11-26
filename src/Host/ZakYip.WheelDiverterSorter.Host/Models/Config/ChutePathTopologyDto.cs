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
    /// 摆轮前感应IO的ID（引用感应IO配置中的SensorId，必须配置）
    /// </summary>
    /// <remarks>
    /// 类型必须为 WheelFront，用于检测包裹是否已经到达摆轮前。
    /// 此字段为必填项，因为需要依靠感应器来判断包裹是否已经到达摆轮前。
    /// </remarks>
    /// <example>2</example>
    [Required]
    [Range(1, long.MaxValue)]
    public required long FrontSensorId { get; init; }

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

/// <summary>
/// 拓扑模拟测试请求
/// </summary>
/// <remarks>
/// <para>用于测试格口路径拓扑配置，模拟包裹从入口到指定格口的完整分拣过程。</para>
/// <para><b>重要</b>：线体速度和线体段长度参数已从此请求中移除。</para>
/// <para>模拟将使用已配置的拓扑配置中的参数。如果拓扑配置未完成，模拟将返回错误。</para>
/// </remarks>
public record TopologySimulationRequest
{
    /// <summary>
    /// 目标格口ID
    /// </summary>
    /// <example>1</example>
    [Required]
    [Range(1, long.MaxValue)]
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 是否模拟超时场景
    /// </summary>
    /// <example>false</example>
    public bool SimulateTimeout { get; init; } = false;

    /// <summary>
    /// 超时时额外延迟的毫秒数
    /// </summary>
    /// <example>5000</example>
    [Range(0, 60000)]
    public int TimeoutExtraDelayMs { get; init; } = 5000;

    /// <summary>
    /// 是否模拟丢包场景
    /// </summary>
    /// <example>false</example>
    public bool SimulateParcelLoss { get; init; } = false;

    /// <summary>
    /// 在第几个摆轮处模拟丢包（从1开始）
    /// </summary>
    /// <remarks>
    /// 仅在 SimulateParcelLoss 为 true 时生效
    /// </remarks>
    /// <example>2</example>
    [Range(1, 100)]
    public int ParcelLossAtDiverterIndex { get; init; } = 1;

    /// <summary>
    /// 路由请求延迟（毫秒）
    /// </summary>
    /// <example>50</example>
    [Range(0, 10000)]
    public int RoutingRequestDelayMs { get; init; } = 50;

    /// <summary>
    /// 传感器检测延迟（毫秒）
    /// </summary>
    /// <example>10</example>
    [Range(0, 1000)]
    public int SensorDetectionDelayMs { get; init; } = 10;

    /// <summary>
    /// 摆轮动作延迟（毫秒）
    /// </summary>
    /// <example>100</example>
    [Range(0, 5000)]
    public int DiverterActionDelayMs { get; init; } = 100;
}

/// <summary>
/// 拓扑模拟测试结果
/// </summary>
public record TopologySimulationResult
{
    /// <summary>
    /// 模拟包裹ID
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 实际到达的格口ID
    /// </summary>
    public long? ActualChuteId { get; set; }

    /// <summary>
    /// 目标是否为异常格口
    /// </summary>
    public bool IsExceptionChute { get; init; }

    /// <summary>
    /// 模拟开始时间
    /// </summary>
    public DateTime SimulationStartTime { get; init; }

    /// <summary>
    /// 模拟结束时间
    /// </summary>
    public DateTime SimulationEndTime { get; set; }

    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// 总运输距离（毫米）
    /// </summary>
    public double TotalDistanceMm { get; set; }

    /// <summary>
    /// 经过的摆轮数量
    /// </summary>
    public int DiverterCount { get; set; }

    /// <summary>
    /// 是否模拟超时
    /// </summary>
    public bool SimulateTimeout { get; init; }

    /// <summary>
    /// 是否模拟丢包
    /// </summary>
    public bool SimulateParcelLoss { get; init; }

    /// <summary>
    /// 模拟是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 是否发生丢包
    /// </summary>
    public bool IsParcelLost { get; set; }

    /// <summary>
    /// 是否发生超时
    /// </summary>
    public bool IsTimeout { get; set; }

    /// <summary>
    /// 模拟步骤列表
    /// </summary>
    public required List<SimulationStep> Steps { get; init; }

    /// <summary>
    /// 结果摘要
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>
/// 模拟步骤
/// </summary>
public record SimulationStep
{
    /// <summary>
    /// 步骤序号
    /// </summary>
    public int StepNumber { get; init; }

    /// <summary>
    /// 步骤类型
    /// </summary>
    public SimulationStepType StepType { get; init; }

    /// <summary>
    /// 步骤描述
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// 节点ID（摆轮ID或格口ID）
    /// </summary>
    public long? NodeId { get; init; }

    /// <summary>
    /// 节点名称
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// 步骤开始时间
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// 步骤结束时间
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// 步骤耗时（毫秒）
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// 累计耗时（毫秒）
    /// </summary>
    public long CumulativeTimeMs { get; init; }

    /// <summary>
    /// 步骤状态
    /// </summary>
    public StepStatus Status { get; set; }

    /// <summary>
    /// 附加详情
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}

/// <summary>
/// 模拟步骤类型
/// </summary>
public enum SimulationStepType
{
    /// <summary>
    /// 包裹创建
    /// </summary>
    ParcelCreation,

    /// <summary>
    /// 路由请求
    /// </summary>
    RoutingRequest,

    /// <summary>
    /// 运输中
    /// </summary>
    Transit,

    /// <summary>
    /// 传感器检测
    /// </summary>
    SensorDetection,

    /// <summary>
    /// 摆轮动作
    /// </summary>
    DiverterAction,

    /// <summary>
    /// 到达格口
    /// </summary>
    ChuteArrival
}

/// <summary>
/// 步骤状态
/// </summary>
public enum StepStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 超时
    /// </summary>
    Timeout,

    /// <summary>
    /// 路由到异常格口
    /// </summary>
    RoutedToException
}

/// <summary>
/// 拓扑图响应
/// </summary>
/// <remarks>
/// 返回ASCII格式的拓扑图，便于可视化查看配置的拓扑结构
/// </remarks>
public record TopologyDiagramResponse
{
    /// <summary>
    /// 拓扑配置名称
    /// </summary>
    /// <example>标准格口路径拓扑</example>
    public required string TopologyName { get; init; }

    /// <summary>
    /// 拓扑描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// ASCII格式的拓扑图
    /// </summary>
    /// <remarks>
    /// <para>示例：</para>
    /// <code>
    ///       格口B     格口D     格口F
    ///         ↑         ↑         ↑
    /// 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(异常口999)
    ///   ↓     ↓         ↓         ↓
    /// 传感器  格口A      格口C     格口E
    /// </code>
    /// </remarks>
    public required string Diagram { get; init; }

    /// <summary>
    /// 摆轮数量
    /// </summary>
    public int DiverterCount { get; init; }

    /// <summary>
    /// 格口总数
    /// </summary>
    public int TotalChuteCount { get; init; }

    /// <summary>
    /// 入口传感器ID
    /// </summary>
    public long EntrySensorId { get; init; }

    /// <summary>
    /// 异常格口ID
    /// </summary>
    public long ExceptionChuteId { get; init; }
}

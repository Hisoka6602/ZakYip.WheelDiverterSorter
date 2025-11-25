namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 线体段配置
/// </summary>
/// <remarks>
/// 描述线体上两个感应IO之间的物理段，包括长度和速度信息。
/// 
/// **拓扑规则**：
/// - 线体段由起点IO和终点IO定义，通过感应IO的Id引用
/// - 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）
/// - 最后一段线体的终点IO Id应该设为0，表示已到达末端
/// - 中间线体段的终点IO通常是摆轮前感应IO（WheelFront类型）
/// 
/// **计算用途**：
/// - 根据线体长度和速度计算包裹从上一个IO到下一个IO的理论时间
/// - 用于超时检测和丢包判断逻辑
/// 
/// **向后兼容**：
/// - 新代码应使用 StartIoId/EndIoId/SpeedMmPerSec
/// - 旧代码仍可使用 FromNodeId/ToNodeId/NominalSpeedMmPerSec（标记为废弃）
/// </remarks>
public record class LineSegmentConfig
{
    /// <summary>
    /// 线体段唯一标识符
    /// </summary>
    public required string SegmentId { get; init; }

    /// <summary>
    /// 线体段显示名称
    /// </summary>
    /// <example>入口到第一摆轮段</example>
    public string? SegmentName { get; init; }

    /// <summary>
    /// 起点感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）。
    /// 默认值为0，表示使用旧模型（FromNodeId）。
    /// </remarks>
    public long StartIoId { get; init; } = 0;

    /// <summary>
    /// 终点感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// - 中间线体段：通常是摆轮前感应IO（WheelFront类型）
    /// - 最后一段线体：设为0，表示已到达末端
    /// 默认值为0，表示使用旧模型（ToNodeId）或末端。
    /// </remarks>
    public long EndIoId { get; init; } = 0;

    /// <summary>
    /// 线体段物理长度（单位：毫米）
    /// </summary>
    /// <remarks>
    /// 从起点IO到终点IO的实际距离
    /// </remarks>
    public required double LengthMm { get; init; }

    // 内部速度字段，用于存储实际速度值
    private readonly double _speedMmPerSec;

    /// <summary>
    /// 线体运行速度（单位：毫米/秒）
    /// </summary>
    /// <remarks>
    /// 该线体段的标准运行速度，用于计算理论到达时间。
    /// 如果未设置，则使用 NominalSpeedMmPerSec 的值（向后兼容）。
    /// </remarks>
    public double SpeedMmPerSec
    {
        get => _speedMmPerSec > 0 ? _speedMmPerSec : _nominalSpeedMmPerSec;
        init => _speedMmPerSec = value;
    }

    /// <summary>
    /// 线体段描述（可选）
    /// </summary>
    public string? Description { get; init; }

    #region 向后兼容 - 废弃字段

    /// <summary>
    /// [废弃] 起始节点ID - 新代码请使用 StartIoId
    /// </summary>
    [Obsolete("新代码请使用 StartIoId")]
    public string? FromNodeId { get; init; }

    /// <summary>
    /// [废弃] 目标节点ID - 新代码请使用 EndIoId
    /// </summary>
    [Obsolete("新代码请使用 EndIoId")]
    public string? ToNodeId { get; init; }

    // 内部标称速度字段，用于向后兼容
    private readonly double _nominalSpeedMmPerSec;

    /// <summary>
    /// [废弃] 标称运行速度（单位：毫米/秒） - 新代码请使用 SpeedMmPerSec
    /// </summary>
    [Obsolete("新代码请使用 SpeedMmPerSec")]
    public double NominalSpeedMmPerSec
    {
        get => _nominalSpeedMmPerSec > 0 ? _nominalSpeedMmPerSec : _speedMmPerSec;
        init => _nominalSpeedMmPerSec = value;
    }

    #endregion

    /// <summary>
    /// 计算包裹通过该段的理论时间（毫秒）
    /// </summary>
    /// <param name="speedMmPerSec">实际速度，如果为null则使用配置的速度</param>
    /// <returns>通过该段所需的时间（毫秒）</returns>
    public double CalculateTransitTimeMs(double? speedMmPerSec = null)
    {
        var speed = speedMmPerSec ?? SpeedMmPerSec;
        if (speed <= 0)
        {
            throw new ArgumentException("速度必须大于0", nameof(speedMmPerSec));
        }
        return (LengthMm / speed) * 1000.0;
    }

    /// <summary>
    /// 检查是否为末端线体段（终点IO Id为0）
    /// </summary>
    public bool IsEndSegment => EndIoId == 0;
}

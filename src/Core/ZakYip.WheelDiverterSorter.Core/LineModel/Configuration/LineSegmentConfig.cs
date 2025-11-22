namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 线体段配置
/// </summary>
/// <remarks>
/// 描述线体上两个节点（入口、摆轮、格口）之间的物理段，包括长度和速度信息
/// </remarks>
public record class LineSegmentConfig
{
    /// <summary>
    /// 线体段唯一标识符
    /// </summary>
    public required string SegmentId { get; init; }

    /// <summary>
    /// 起始节点ID（入口节点、摆轮节点或虚拟节点）
    /// </summary>
    /// <remarks>
    /// 例如：
    /// - "ENTRY" - 入口节点
    /// - "WHEEL-1" - 第一个摆轮
    /// - "WHEEL-2" - 第二个摆轮
    /// </remarks>
    public required string FromNodeId { get; init; }

    /// <summary>
    /// 目标节点ID
    /// </summary>
    /// <remarks>
    /// 例如：
    /// - "WHEEL-1" - 第一个摆轮
    /// - "WHEEL-2" - 第二个摆轮
    /// - "CHUTE-001" - 格口
    /// </remarks>
    public required string ToNodeId { get; init; }

    /// <summary>
    /// 线体段物理长度（单位：毫米）
    /// </summary>
    /// <remarks>
    /// 从起始节点到目标节点的实际距离
    /// </remarks>
    public required double LengthMm { get; init; }

    /// <summary>
    /// 标称运行速度（单位：毫米/秒）
    /// </summary>
    /// <remarks>
    /// 该线体段的标准运行速度，用于计算理论到达时间
    /// </remarks>
    public required double NominalSpeedMmPerSec { get; init; }

    /// <summary>
    /// 线体段描述（可选）
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 计算包裹通过该段的理论时间（毫秒）
    /// </summary>
    /// <param name="speedMmPerSec">实际速度，如果为null则使用标称速度</param>
    /// <returns>通过该段所需的时间（毫秒）</returns>
    public double CalculateTransitTimeMs(double? speedMmPerSec = null)
    {
        var speed = speedMmPerSec ?? NominalSpeedMmPerSec;
        if (speed <= 0)
        {
            throw new ArgumentException("速度必须大于0", nameof(speedMmPerSec));
        }
        return (LengthMm / speed) * 1000.0;
    }
}

using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

/// <summary>
/// 表示摆轮路径中的单个段，包含摆轮动作和时间限制
/// </summary>
public record class SwitchingPathSegment
{
    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    public required int SequenceNumber { get; init; }

    /// <summary>
    /// 摆轮标识（数字ID）
    /// </summary>
    public required int DiverterId { get; init; }

    /// <summary>
    /// 目标摆轮转向方向
    /// </summary>
    public required DiverterDirection TargetDirection { get; init; }

    /// <summary>
    /// 单段生存时间（毫秒），超过此时间段无效
    /// </summary>
    public required int TtlMilliseconds { get; init; }
}

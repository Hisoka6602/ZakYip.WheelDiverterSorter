using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 摆轮命令模型，包含执行摆轮动作所需的全部信息
/// </summary>
/// <remarks>
/// <para>此模型封装了摆轮控制命令的业务语义，用于统一的命令执行流程。</para>
/// <para>执行器只需关注此模型，不需要关心底层驱动实现细节。</para>
/// </remarks>
public sealed record WheelCommand
{
    /// <summary>
    /// 摆轮标识符
    /// </summary>
    /// <example>D001</example>
    public required string DiverterId { get; init; }

    /// <summary>
    /// 目标方向
    /// </summary>
    /// <remarks>
    /// 摆轮动作的目标方向，包括：左转、右转、直通
    /// </remarks>
    public required DiverterDirection Direction { get; init; }

    /// <summary>
    /// 命令超时时间
    /// </summary>
    /// <remarks>
    /// 如果命令在此时间内未完成，执行器应返回超时错误
    /// </remarks>
    public required TimeSpan Timeout { get; init; }

    /// <summary>
    /// 路径段序号（可选）
    /// </summary>
    /// <remarks>
    /// 当命令来自路径执行时，此字段用于关联路径段信息
    /// </remarks>
    public int? SequenceNumber { get; init; }

    /// <summary>
    /// 创建左转命令
    /// </summary>
    /// <param name="diverterId">摆轮标识符</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="sequenceNumber">路径段序号（可选）</param>
    public static WheelCommand TurnLeft(string diverterId, TimeSpan timeout, int? sequenceNumber = null) => new()
    {
        DiverterId = diverterId,
        Direction = DiverterDirection.Left,
        Timeout = timeout,
        SequenceNumber = sequenceNumber
    };

    /// <summary>
    /// 创建右转命令
    /// </summary>
    /// <param name="diverterId">摆轮标识符</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="sequenceNumber">路径段序号（可选）</param>
    public static WheelCommand TurnRight(string diverterId, TimeSpan timeout, int? sequenceNumber = null) => new()
    {
        DiverterId = diverterId,
        Direction = DiverterDirection.Right,
        Timeout = timeout,
        SequenceNumber = sequenceNumber
    };

    /// <summary>
    /// 创建直通命令
    /// </summary>
    /// <param name="diverterId">摆轮标识符</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="sequenceNumber">路径段序号（可选）</param>
    public static WheelCommand PassThrough(string diverterId, TimeSpan timeout, int? sequenceNumber = null) => new()
    {
        DiverterId = diverterId,
        Direction = DiverterDirection.Straight,
        Timeout = timeout,
        SequenceNumber = sequenceNumber
    };
}

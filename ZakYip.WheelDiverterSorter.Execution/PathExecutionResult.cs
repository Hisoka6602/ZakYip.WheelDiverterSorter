using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 表示摆轮路径执行的结果
/// </summary>
public record class PathExecutionResult
{
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 实际落格的格口标识（数字ID）
    /// </summary>
    /// <remarks>
    /// <para>当执行成功时，此值应等于 <see cref="SwitchingPath.TargetChuteId"/>；</para>
    /// <para>当执行失败时，此值应为 <see cref="SwitchingPath.FallbackChuteId"/>（最终异常格口）。</para>
    /// <para>注意：如果旧代码中存在硬编码的异常口逻辑，应统一使用 
    /// <see cref="SwitchingPath.FallbackChuteId"/> 字段，删除旧的实现方式。</para>
    /// </remarks>
    public required int ActualChuteId { get; init; }

    /// <summary>
    /// 失败原因说明
    /// 当 IsSuccess = false 时，此字段描述失败的具体原因
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 失败的路径段（如果执行失败）
    /// </summary>
    /// <remarks>
    /// 当路径段执行失败时，此字段包含失败的段信息，用于定位失败位置
    /// </remarks>
    public SwitchingPathSegment? FailedSegment { get; init; }

    /// <summary>
    /// 失败时间
    /// </summary>
    public DateTimeOffset? FailureTime { get; init; }
}

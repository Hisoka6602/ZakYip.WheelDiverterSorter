using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;

/// <summary>
/// 改口决策结果记录。
/// </summary>
public record struct ChuteChangeDecision
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 原目标格口ID
    /// </summary>
    public required int OriginalChuteId { get; init; }

    /// <summary>
    /// 请求的新目标格口ID
    /// </summary>
    public required int RequestedChuteId { get; init; }

    /// <summary>
    /// 实际生效的格口ID（如果改口被接受，则等于RequestedChuteId；否则等于OriginalChuteId）
    /// </summary>
    public required int? AppliedChuteId { get; init; }

    /// <summary>
    /// 决策结果
    /// </summary>
    public required ChuteChangeOutcome Outcome { get; init; }

    /// <summary>
    /// 改口请求时间
    /// </summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>
    /// 决策处理时间
    /// </summary>
    public required DateTimeOffset DecidedAt { get; init; }

    /// <summary>
    /// 可选的原因说明
    /// </summary>
    public string? Reason { get; init; }
}

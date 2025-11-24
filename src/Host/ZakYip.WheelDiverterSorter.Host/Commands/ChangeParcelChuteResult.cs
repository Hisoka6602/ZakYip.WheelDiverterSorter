using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Host.Commands;

/// <summary>
/// 改口命令执行结果
/// </summary>
public sealed record class ChangeParcelChuteResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 原目标格口ID
    /// </summary>
    public long? OriginalChuteId { get; init; }

    /// <summary>
    /// 请求的新格口ID
    /// </summary>
    public required long RequestedChuteId { get; init; }

    /// <summary>
    /// 实际生效的格口ID
    /// </summary>
    public long? EffectiveChuteId { get; init; }

    /// <summary>
    /// 决策结果
    /// </summary>
    public ChuteChangeOutcome? Outcome { get; init; }

    /// <summary>
    /// 结果消息或原因
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 处理时间
    /// </summary>
    public DateTimeOffset ProcessedAt { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ChangeParcelChuteResult Success(
        long parcelId,
        long originalChuteId,
        long requestedChuteId,
        long effectiveChuteId,
        ChuteChangeOutcome outcome,
        DateTimeOffset processedAt,
        string? message = null)
    {
        return new ChangeParcelChuteResult
        {
            IsSuccess = true,
            ParcelId = parcelId,
            OriginalChuteId = originalChuteId,
            RequestedChuteId = requestedChuteId,
            EffectiveChuteId = effectiveChuteId,
            Outcome = outcome,
            Message = message ?? "Chute change processed successfully",
            ProcessedAt = processedAt
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ChangeParcelChuteResult Failure(
        long parcelId,
        long requestedChuteId,
        DateTimeOffset processedAt,
        string message)
    {
        return new ChangeParcelChuteResult
        {
            IsSuccess = false,
            ParcelId = parcelId,
            RequestedChuteId = requestedChuteId,
            Message = message,
            ProcessedAt = processedAt
        };
    }
}

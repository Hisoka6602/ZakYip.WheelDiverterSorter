using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

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
    public int? OriginalChuteId { get; init; }

    /// <summary>
    /// 请求的新格口ID
    /// </summary>
    public required int RequestedChuteId { get; init; }

    /// <summary>
    /// 实际生效的格口ID
    /// </summary>
    public int? EffectiveChuteId { get; init; }

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
        int originalChuteId,
        int requestedChuteId,
        int effectiveChuteId,
        ChuteChangeOutcome outcome,
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
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ChangeParcelChuteResult Failure(
        long parcelId,
        int requestedChuteId,
        string message)
    {
        return new ChangeParcelChuteResult
        {
            IsSuccess = false,
            ParcelId = parcelId,
            RequestedChuteId = requestedChuteId,
            Message = message,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }
}

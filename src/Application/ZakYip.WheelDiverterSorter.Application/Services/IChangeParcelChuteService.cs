using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// 改口服务接口
/// </summary>
/// <remarks>
/// 负责处理包裹目标格口变更请求
/// </remarks>
public interface IChangeParcelChuteService
{
    /// <summary>
    /// 处理改口请求
    /// </summary>
    /// <param name="command">改口命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>改口结果</returns>
    Task<ChangeParcelChuteResult> ChangeParcelChuteAsync(
        ChangeParcelChuteCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 改口命令
/// </summary>
public sealed record ChangeParcelChuteCommand
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 请求的新目标格口ID
    /// </summary>
    public required long RequestedChuteId { get; init; }

    /// <summary>
    /// 请求时间（可选，默认为服务器当前时间）
    /// </summary>
    public DateTimeOffset? RequestedAt { get; init; }
}

/// <summary>
/// 改口命令执行结果
/// </summary>
public sealed record ChangeParcelChuteResult
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

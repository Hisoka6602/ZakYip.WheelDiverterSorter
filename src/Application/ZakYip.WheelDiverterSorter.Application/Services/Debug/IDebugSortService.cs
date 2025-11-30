using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;

namespace ZakYip.WheelDiverterSorter.Application.Services.Debug;

/// <summary>
/// 调试分拣服务接口
/// </summary>
/// <remarks>
/// 提供调试分拣功能，用于测试直线摆轮分拣方案
/// </remarks>
public interface IDebugSortService
{
    /// <summary>
    /// 执行调试分拣操作
    /// </summary>
    /// <param name="parcelId">包裹标识</param>
    /// <param name="targetChuteId">目标格口标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调试分拣结果</returns>
    Task<DebugSortResult> ExecuteDebugSortAsync(
        string parcelId,
        long targetChuteId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 调试分拣结果
/// </summary>
public record DebugSortResult
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 执行是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 实际落格的格口标识
    /// </summary>
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 执行结果消息
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 生成的路径段数量
    /// </summary>
    public int PathSegmentCount { get; init; }
}

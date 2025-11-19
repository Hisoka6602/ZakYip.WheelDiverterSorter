using ZakYip.WheelDiverterSorter.Core.LineModel;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 路径重规划接口，用于在包裹执行过程中更新目标格口和路径。
/// </summary>
public interface IRouteReplanner
{
    /// <summary>
    /// 尝试为正在执行的包裹重新规划路径
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="newTargetChuteId">新的目标格口ID</param>
    /// <param name="replanAt">重规划请求时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重规划结果</returns>
    /// <remarks>
    /// <para>重规划逻辑：</para>
    /// <list type="number">
    /// <item>查询包裹当前位置和已执行的路径段</item>
    /// <item>判断是否还有足够时间重新生成路径（基于当前位置和关键摆轮位置）</item>
    /// <item>如果可以重规划：生成新路径，替换旧路径，返回成功</item>
    /// <item>如果太晚（已过关键节点）：返回失败，原因为TooLate</item>
    /// </list>
    /// <para><strong>硬约束：</strong>宁可异常，不得错分。
    /// 如果无法安全重规划，必须返回失败，不能强行修改路径。</para>
    /// </remarks>
    Task<ReplanResult> ReplanAsync(
        long parcelId,
        int newTargetChuteId,
        DateTimeOffset replanAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 重规划结果
/// </summary>
public record class ReplanResult
{
    /// <summary>
    /// 是否成功重规划
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
    /// 新目标格口ID
    /// </summary>
    public int? NewChuteId { get; init; }

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 新路径（如果成功）
    /// </summary>
    public SwitchingPath? NewPath { get; init; }

    /// <summary>
    /// 创建成功的重规划结果
    /// </summary>
    public static ReplanResult Success(long parcelId, int originalChuteId, int newChuteId, SwitchingPath newPath)
    {
        return new ReplanResult
        {
            IsSuccess = true,
            ParcelId = parcelId,
            OriginalChuteId = originalChuteId,
            NewChuteId = newChuteId,
            NewPath = newPath
        };
    }

    /// <summary>
    /// 创建失败的重规划结果
    /// </summary>
    public static ReplanResult Failure(long parcelId, string reason)
    {
        return new ReplanResult
        {
            IsSuccess = false,
            ParcelId = parcelId,
            FailureReason = reason
        };
    }
}

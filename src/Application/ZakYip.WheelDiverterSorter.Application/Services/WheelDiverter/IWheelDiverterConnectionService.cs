namespace ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;

/// <summary>
/// 摆轮连接管理服务接口
/// </summary>
/// <remarks>
/// 负责在系统启动时连接摆轮设备，并更新健康状态。
/// </remarks>
public interface IWheelDiverterConnectionService
{
    /// <summary>
    /// 连接所有配置的摆轮设备
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接结果</returns>
    Task<WheelDiverterConnectionResult> ConnectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动所有已连接的摆轮
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启动结果</returns>
    Task<WheelDiverterOperationResult> RunAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止所有摆轮
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止结果</returns>
    Task<WheelDiverterOperationResult> StopAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 让所有摆轮向前（直通）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<WheelDiverterOperationResult> PassThroughAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有摆轮的健康状态
    /// </summary>
    /// <returns>健康状态列表</returns>
    Task<IReadOnlyList<WheelDiverterHealthInfo>> GetHealthStatusesAsync();
}

/// <summary>
/// 摆轮连接结果
/// </summary>
public record class WheelDiverterConnectionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 成功连接的摆轮数量
    /// </summary>
    public required int ConnectedCount { get; init; }

    /// <summary>
    /// 配置的摆轮总数
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// 连接失败的摆轮ID列表
    /// </summary>
    public required IReadOnlyList<string> FailedDriverIds { get; init; }

    /// <summary>
    /// 错误消息（如果有）
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 摆轮操作结果
/// </summary>
public record class WheelDiverterOperationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 成功操作的摆轮数量
    /// </summary>
    public required int SuccessCount { get; init; }

    /// <summary>
    /// 摆轮总数
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// 操作失败的摆轮ID列表
    /// </summary>
    public required IReadOnlyList<string> FailedDriverIds { get; init; }

    /// <summary>
    /// 错误消息（如果有）
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 摆轮健康信息
/// </summary>
public record class WheelDiverterHealthInfo
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public required string DiverterId { get; init; }

    /// <summary>
    /// 是否健康
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// 当前状态描述
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 错误消息（如果不健康）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public required DateTimeOffset LastUpdated { get; init; }
}

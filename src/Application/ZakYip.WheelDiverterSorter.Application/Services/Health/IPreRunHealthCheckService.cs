using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Application.Services.Health;

/// <summary>
/// 运行前健康检查服务接口
/// Pre-run health check service interface
/// </summary>
/// <remarks>
/// 在系统启动后、开始实际分拣前，验证所有关键配置是否就绪
/// Validates all critical configurations are ready before starting actual sorting
/// </remarks>
public interface IPreRunHealthCheckService
{
    /// <summary>
    /// 执行运行前健康检查
    /// Execute pre-run health check
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>运行前健康检查结果</returns>
    Task<PreRunHealthCheckResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 运行前健康检查结果
/// Pre-run health check result
/// </summary>
public record PreRunHealthCheckResult
{
    /// <summary>
    /// 整体状态
    /// </summary>
    public required HealthStatus OverallStatus { get; init; }

    /// <summary>
    /// 各项检查结果列表
    /// </summary>
    public required List<HealthCheckItem> Checks { get; init; }

    /// <summary>
    /// 是否所有检查都通过
    /// </summary>
    public bool IsHealthy => OverallStatus == HealthStatus.Healthy;
}

/// <summary>
/// 单项健康检查结果
/// Individual health check item
/// </summary>
public record HealthCheckItem
{
    /// <summary>
    /// 检查项名称（英文标识符）
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 检查状态
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// 检查结果描述（中文消息）
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 是否健康
    /// </summary>
    public bool IsHealthy => Status == HealthStatus.Healthy;
}

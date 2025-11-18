using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 健康检查API控制器
/// </summary>
[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly ISystemStateManager _stateManager;
    private readonly PrometheusMetrics? _metrics;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ISystemStateManager stateManager,
        ILogger<HealthController> logger,
        PrometheusMetrics? metrics = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
    }

    /// <summary>
    /// 进程级健康检查端点
    /// </summary>
    /// <returns>简单的健康状态</returns>
    /// <remarks>
    /// 用于Kubernetes/负载均衡器的存活检查。
    /// 只依赖进程与基础依赖存活，不依赖驱动自检结果。
    /// </remarks>
    [HttpGet("healthz")]
    [ProducesResponseType(typeof(ProcessHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProcessHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetProcessHealth()
    {
        try
        {
            var currentState = _stateManager.CurrentState;
            
            // 进程级健康检查：只要进程能响应就认为健康
            var response = new ProcessHealthResponse
            {
                Status = "Healthy",
                Timestamp = DateTimeOffset.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "进程健康检查失败");
            var response = new ProcessHealthResponse
            {
                Status = "Unhealthy",
                Reason = "进程内部错误",
                Timestamp = DateTimeOffset.UtcNow
            };
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }

    /// <summary>
    /// 线体级健康检查端点
    /// </summary>
    /// <returns>详细的系统健康状态</returns>
    /// <remarks>
    /// 返回详细的自检结果，包括驱动器、上游系统、配置等状态。
    /// 200 OK: 系统Ready/Running且自检成功
    /// 503 ServiceUnavailable: 系统Faulted/EmergencyStop或自检失败
    /// </remarks>
    [HttpGet("health/line")]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetLineHealth()
    {
        try
        {
            var currentState = _stateManager.CurrentState;
            var lastReport = _stateManager.LastSelfTestReport;

            var response = new LineHealthResponse
            {
                SystemState = currentState.ToString(),
                IsSelfTestSuccess = lastReport?.IsSuccess ?? false,
                LastSelfTestAt = lastReport?.PerformedAt,
                Drivers = lastReport?.Drivers?.Select(d => new DriverHealthInfo
                {
                    DriverName = d.DriverName,
                    IsHealthy = d.IsHealthy,
                    ErrorCode = d.ErrorCode,
                    ErrorMessage = d.ErrorMessage,
                    CheckedAt = d.CheckedAt
                }).ToList(),
                Upstreams = lastReport?.Upstreams?.Select(u => new UpstreamHealthInfo
                {
                    EndpointName = u.EndpointName,
                    IsHealthy = u.IsHealthy,
                    ErrorCode = u.ErrorCode,
                    ErrorMessage = u.ErrorMessage,
                    CheckedAt = u.CheckedAt
                }).ToList(),
                Config = lastReport?.Config != null ? new ConfigHealthInfo
                {
                    IsValid = lastReport.Config.IsValid,
                    ErrorMessage = lastReport.Config.ErrorMessage
                } : null,
                Summary = _metrics != null ? new SystemSummary
                {
                    CurrentCongestionLevel = GetCongestionLevelDescription(currentState),
                    RecommendedCapacityParcelsPerMinute = null // 可从metrics读取，暂不实现
                } : null
            };

            // 判断HTTP状态码
            var isHealthy = (currentState == SystemState.Ready || currentState == SystemState.Running) &&
                           (lastReport?.IsSuccess ?? true);

            if (isHealthy)
            {
                return Ok(response);
            }
            else
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "线体健康检查失败");
            var response = new LineHealthResponse
            {
                SystemState = "Unknown",
                IsSelfTestSuccess = false,
                LastSelfTestAt = null
            };
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }

    private static string? GetCongestionLevelDescription(SystemState state)
    {
        return state switch
        {
            SystemState.Faulted => "Critical",
            SystemState.EmergencyStop => "Critical",
            _ => "Normal"
        };
    }
}

/// <summary>
/// 进程级健康响应
/// </summary>
public class ProcessHealthResponse
{
    /// <summary>健康状态: Healthy/Unhealthy</summary>
    public required string Status { get; init; }

    /// <summary>失败原因（如果不健康）</summary>
    public string? Reason { get; init; }

    /// <summary>检查时间</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// 线体级健康响应
/// </summary>
public class LineHealthResponse
{
    /// <summary>系统状态</summary>
    public required string SystemState { get; init; }

    /// <summary>自检是否成功</summary>
    public bool IsSelfTestSuccess { get; init; }

    /// <summary>最近一次自检时间</summary>
    public DateTimeOffset? LastSelfTestAt { get; init; }

    /// <summary>驱动器健康状态列表</summary>
    public List<DriverHealthInfo>? Drivers { get; init; }

    /// <summary>上游系统健康状态列表</summary>
    public List<UpstreamHealthInfo>? Upstreams { get; init; }

    /// <summary>配置健康状态</summary>
    public ConfigHealthInfo? Config { get; init; }

    /// <summary>系统概要信息（可选）</summary>
    public SystemSummary? Summary { get; init; }
}

/// <summary>
/// 驱动器健康信息
/// </summary>
public class DriverHealthInfo
{
    public required string DriverName { get; init; }
    public bool IsHealthy { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}

/// <summary>
/// 上游系统健康信息
/// </summary>
public class UpstreamHealthInfo
{
    public required string EndpointName { get; init; }
    public bool IsHealthy { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}

/// <summary>
/// 配置健康信息
/// </summary>
public class ConfigHealthInfo
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 系统概要信息
/// </summary>
public class SystemSummary
{
    /// <summary>当前拥堵级别</summary>
    public string? CurrentCongestionLevel { get; init; }

    /// <summary>推荐产能（包裹/分钟）</summary>
    public double? RecommendedCapacityParcelsPerMinute { get; init; }
}

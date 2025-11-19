using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Runtime.Health;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 健康检查API控制器
/// </summary>
/// <remarks>
/// 提供进程级和线体级的健康检查端点，支持Kubernetes等容器编排平台的健康探测
/// </remarks>
[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly ISystemStateManager _stateManager;
    private readonly IHealthStatusProvider _healthStatusProvider;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ISystemStateManager stateManager,
        IHealthStatusProvider healthStatusProvider,
        ILogger<HealthController> logger)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _healthStatusProvider = healthStatusProvider ?? throw new ArgumentNullException(nameof(healthStatusProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 进程级健康检查端点
    /// </summary>
    /// <returns>简单的健康状态</returns>
    /// <response code="200">进程健康</response>
    /// <response code="503">进程不健康</response>
    /// <remarks>
    /// 用于Kubernetes/负载均衡器的存活检查（liveness probe）。
    /// 只依赖进程与基础依赖存活，不依赖驱动自检结果。
    /// 只要进程能够响应请求，就认为是健康的。
    /// </remarks>
    [HttpGet("healthz")]
    [SwaggerOperation(
        Summary = "进程级健康检查",
        Description = "用于容器编排平台的存活检查，只要进程能响应就返回健康状态",
        OperationId = "GetProcessHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "进程健康", typeof(ProcessHealthResponse))]
    [SwaggerResponse(503, "进程不健康", typeof(ProcessHealthResponse))]
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
    /// <response code="200">线体健康且自检成功</response>
    /// <response code="503">线体故障或自检失败</response>
    /// <remarks>
    /// 用于容器编排平台的就绪检查（readiness probe）。
    /// 返回详细的自检结果，包括驱动器、上游系统、配置等状态。
    /// 
    /// 状态码说明：
    /// - 200 OK: 系统状态为Ready/Running且自检成功
    /// - 503 ServiceUnavailable: 系统状态为Faulted/EmergencyStop或自检失败
    /// 
    /// 响应包含：
    /// - 系统当前状态（SystemState）
    /// - 自检是否成功（IsSelfTestSuccess）
    /// - 驱动器健康状态列表
    /// - 上游系统连接状态
    /// - 配置有效性检查
    /// - 降级模式和降级节点信息（如有）
    /// - 近期Critical告警摘要
    /// </remarks>
    [HttpGet("health/line")]
    [SwaggerOperation(
        Summary = "线体级健康检查",
        Description = "返回详细的自检结果，包括驱动器、上游系统、配置等健康状态，用于就绪检查",
        OperationId = "GetLineHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "线体健康且自检成功", typeof(LineHealthResponse))]
    [SwaggerResponse(503, "线体故障或自检失败", typeof(LineHealthResponse))]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetLineHealth()
    {
        try
        {
            // 使用 IHealthStatusProvider 获取健康快照
            var snapshot = await _healthStatusProvider.GetHealthSnapshotAsync();

            // 映射到响应DTO
            var response = MapSnapshotToResponse(snapshot);

            // 判断HTTP状态码
            if (snapshot.IsLineAvailable && snapshot.IsSelfTestSuccess)
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

    private static LineHealthResponse MapSnapshotToResponse(LineHealthSnapshot snapshot)
    {
        return new LineHealthResponse
        {
            SystemState = snapshot.SystemState,
            IsSelfTestSuccess = snapshot.IsSelfTestSuccess,
            LastSelfTestAt = snapshot.LastSelfTestAt,
            Drivers = snapshot.Drivers?.Select(d => new DriverHealthInfo
            {
                DriverName = d.DriverName,
                IsHealthy = d.IsHealthy,
                ErrorCode = d.ErrorCode,
                ErrorMessage = d.ErrorMessage,
                CheckedAt = d.CheckedAt
            }).ToList(),
            Upstreams = snapshot.Upstreams?.Select(u => new UpstreamHealthInfo
            {
                EndpointName = u.EndpointName,
                IsHealthy = u.IsHealthy,
                ErrorCode = u.ErrorCode,
                ErrorMessage = u.ErrorMessage,
                CheckedAt = u.CheckedAt
            }).ToList(),
            Config = snapshot.Config != null ? new ConfigHealthInfo
            {
                IsValid = snapshot.Config.IsValid,
                ErrorMessage = snapshot.Config.ErrorMessage
            } : null,
            Summary = new SystemSummary
            {
                CurrentCongestionLevel = snapshot.CurrentCongestionLevel,
                RecommendedCapacityParcelsPerMinute = null
            },
            DegradationMode = snapshot.DegradationMode,
            DegradedNodesCount = snapshot.DegradedNodesCount,
            DegradedNodes = snapshot.DegradedNodes?.Select(n => new NodeHealthInfo
            {
                NodeId = n.NodeId,
                NodeType = n.NodeType,
                IsHealthy = n.IsHealthy,
                ErrorCode = n.ErrorCode,
                ErrorMessage = n.ErrorMessage,
                CheckedAt = n.CheckedAt
            }).ToList(),
            DiagnosticsLevel = snapshot.DiagnosticsLevel,
            ConfigVersion = snapshot.ConfigVersion,
            RecentCriticalAlerts = null // 可以从 AlertHistoryService 获取，但快照中已有计数
        };
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

    /// <summary>降级模式（PR-14：节点级降级）</summary>
    public string? DegradationMode { get; init; }

    /// <summary>降级节点数量（PR-14：节点级降级）</summary>
    public int? DegradedNodesCount { get; init; }

    /// <summary>降级节点列表摘要（PR-14：节点级降级）</summary>
    public List<NodeHealthInfo>? DegradedNodes { get; init; }

    /// <summary>当前诊断级别（PR-23：可观测性）</summary>
    public string? DiagnosticsLevel { get; init; }

    /// <summary>配置版本号（PR-23：可观测性）</summary>
    public string? ConfigVersion { get; init; }

    /// <summary>最近Critical告警摘要（PR-23：可观测性）</summary>
    public List<AlertSummary>? RecentCriticalAlerts { get; init; }
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

/// <summary>
/// 节点健康信息（PR-14：节点级降级）
/// </summary>
public class NodeHealthInfo
{
    public int NodeId { get; init; }
    public string? NodeType { get; init; }
    public bool IsHealthy { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}

/// <summary>
/// 告警摘要信息（PR-23：可观测性）
/// </summary>
public class AlertSummary
{
    public required string AlertCode { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset RaisedAt { get; init; }
}

using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
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
    private readonly IPreRunHealthCheckService? _preRunHealthCheckService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ISystemStateManager stateManager,
        IHealthStatusProvider healthStatusProvider,
        ILogger<HealthController> logger,
        IPreRunHealthCheckService? preRunHealthCheckService = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _healthStatusProvider = healthStatusProvider ?? throw new ArgumentNullException(nameof(healthStatusProvider));
        _preRunHealthCheckService = preRunHealthCheckService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 进程级健康检查端点（Kubernetes liveness probe）
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
    [HttpGet("health/live")]
    [SwaggerOperation(
        Summary = "进程级健康检查（Liveness）",
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
    /// 启动阶段健康检查端点（Kubernetes startup probe）
    /// </summary>
    /// <returns>启动状态</returns>
    /// <response code="200">启动完成</response>
    /// <response code="503">仍在启动中</response>
    /// <remarks>
    /// 用于Kubernetes启动探测（startup probe）。
    /// 检查系统是否已完成初始化和自检，进入Ready或Running状态。
    /// </remarks>
    [HttpGet("health/startup")]
    [SwaggerOperation(
        Summary = "启动阶段健康检查（Startup）",
        Description = "检查系统是否已完成初始化，用于Kubernetes startup probe",
        OperationId = "GetStartupHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "启动完成", typeof(ProcessHealthResponse))]
    [SwaggerResponse(503, "仍在启动中", typeof(ProcessHealthResponse))]
    [ProducesResponseType(typeof(ProcessHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProcessHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetStartupHealth()
    {
        try
        {
            var currentState = _stateManager.CurrentState;
            
            // 启动检查：系统不在Booting状态即认为启动完成
            var isStartupComplete = currentState != SystemState.Booting;
            
            var response = new ProcessHealthResponse
            {
                Status = isStartupComplete ? "Healthy" : "Starting",
                Reason = isStartupComplete ? null : "系统正在启动中",
                Timestamp = DateTimeOffset.UtcNow
            };

            if (isStartupComplete)
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
            _logger.LogError(ex, "启动健康检查失败");
            var response = new ProcessHealthResponse
            {
                Status = "Unhealthy",
                Reason = "启动检查失败",
                Timestamp = DateTimeOffset.UtcNow
            };
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }

    /// <summary>
    /// 就绪状态健康检查端点（Kubernetes readiness probe）
    /// </summary>
    /// <returns>详细的系统健康状态</returns>
    /// <response code="200">系统就绪，可接收流量</response>
    /// <response code="503">系统未就绪</response>
    /// <remarks>
    /// 用于容器编排平台的就绪检查（readiness probe）。
    /// 返回详细的健康检查结果，包括：
    /// 
    /// 关键模块健康状态：
    /// - RuleEngine 连接状态（push模型）
    /// - TTL 调度线程状态（TODO: 待实现）
    /// - 驱动器健康状态
    /// - 传感器健康状态
    /// - 系统状态（Ready/Running/Faulted等）
    /// - 自检结果
    /// - 降级模式和降级节点信息
    /// - 近期Critical告警统计
    /// 
    /// 状态码说明：
    /// - 200 OK: 系统状态为Ready/Running且所有关键模块健康
    /// - 503 ServiceUnavailable: 系统故障或关键模块不健康
    /// </remarks>
    [HttpGet("health/ready")]
    [SwaggerOperation(
        Summary = "就绪状态健康检查（Readiness）",
        Description = "返回详细的健康检查结果，包括RuleEngine连接、TTL调度、驱动器等关键模块状态",
        OperationId = "GetReadinessHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "系统就绪", typeof(LineHealthResponse))]
    [SwaggerResponse(503, "系统未就绪", typeof(LineHealthResponse))]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadinessHealth()
    {
        try
        {
            // 使用 IHealthStatusProvider 获取健康快照
            var snapshot = await _healthStatusProvider.GetHealthSnapshotAsync();

            // 映射到响应DTO
            var response = MapSnapshotToResponse(snapshot);

            // 判断HTTP状态码：检查所有关键模块
            var isReady = snapshot.IsLineAvailable && snapshot.IsSelfTestSuccess;
            
            // 检查 RuleEngine 连接状态
            var ruleEngineHealthy = snapshot.Upstreams?.All(u => u.IsHealthy) ?? true;
            
            // 检查驱动器状态
            var driversHealthy = snapshot.Drivers?.All(d => d.IsHealthy) ?? true;
            
            isReady = isReady && ruleEngineHealthy && driversHealthy;

            if (isReady)
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
            _logger.LogError(ex, "就绪状态健康检查失败");
            var response = new LineHealthResponse
            {
                SystemState = "Unknown",
                IsSelfTestSuccess = false,
                LastSelfTestAt = null
            };
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }

    /// <summary>
    /// 线体级健康检查端点（向后兼容）
    /// </summary>
    /// <returns>详细的系统健康状态</returns>
    /// <response code="200">线体健康且自检成功</response>
    /// <response code="503">线体故障或自检失败</response>
    /// <remarks>
    /// 此端点保持向后兼容性。建议使用 /health/ready 端点。
    /// </remarks>
    [HttpGet("health/line")]
    [SwaggerOperation(
        Summary = "线体级健康检查（向后兼容）",
        Description = "返回详细的自检结果，建议使用 /health/ready 端点",
        OperationId = "GetLineHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "线体健康且自检成功", typeof(LineHealthResponse))]
    [SwaggerResponse(503, "线体故障或自检失败", typeof(LineHealthResponse))]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> GetLineHealth()
    {
        // 重定向到新的 ready 端点
        return GetReadinessHealth();
    }

    /// <summary>
    /// 运行前健康检查端点（Pre-Run Check）
    /// </summary>
    /// <returns>运行前检查结果</returns>
    /// <response code="200">系统配置完整，可以开始运行</response>
    /// <response code="503">系统配置不完整，不可运行</response>
    /// <remarks>
    /// 在开始实际分拣/仿真前验证系统基础运行条件：
    /// 
    /// 检查项目：
    /// - 异常口配置是否正确且存在于拓扑中
    /// - 启动/停止/急停/面板灯等 IO 是否全部配置完毕
    /// - 摆轮拓扑是否完整（至少有一条从入口到首个摆轮的有效路径）
    /// - 线体拓扑中线体长度和线速度是否配置完整且合法
    /// - 其他关键基础运行条件是否满足
    /// 
    /// 返回结构化 JSON，包含 overallStatus 与逐项检查状态，所有 message 为中文描述。
    /// 用于运维/工程在上线前确认系统"具备基本可运行条件"。
    /// </remarks>
    [HttpGet("health/prerun")]
    [SwaggerOperation(
        Summary = "运行前健康检查（Pre-Run Check）",
        Description = "验证系统基础运行条件是否就绪，包括异常口、面板IO、拓扑、线体段配置等",
        OperationId = "GetPreRunHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "系统配置完整，可以开始运行", typeof(PreRunHealthCheckResponse))]
    [SwaggerResponse(503, "系统配置不完整，不可运行", typeof(PreRunHealthCheckResponse))]
    [ProducesResponseType(typeof(PreRunHealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PreRunHealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPreRunHealth()
    {
        try
        {
            if (_preRunHealthCheckService == null)
            {
                _logger.LogWarning("PreRunHealthCheckService 未注册，返回服务不可用");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new PreRunHealthCheckResponse
                {
                    OverallStatus = "Unhealthy",
                    Checks = new List<HealthCheckItemResponse>
                    {
                        new HealthCheckItemResponse
                        {
                            Name = "ServiceAvailability",
                            Status = "Unhealthy",
                            Message = "运行前健康检查服务未启用"
                        }
                    }
                });
            }

            var result = await _preRunHealthCheckService.ExecuteAsync(HttpContext.RequestAborted);

            var response = new PreRunHealthCheckResponse
            {
                OverallStatus = result.OverallStatus,
                Checks = result.Checks.Select(c => new HealthCheckItemResponse
                {
                    Name = c.Name,
                    Status = c.Status,
                    Message = c.Message
                }).ToList()
            };

            if (result.IsHealthy)
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
            _logger.LogError(ex, "运行前健康检查失败");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new PreRunHealthCheckResponse
            {
                OverallStatus = "Unhealthy",
                Checks = new List<HealthCheckItemResponse>
                {
                    new HealthCheckItemResponse
                    {
                        Name = "PreRunCheck",
                        Status = "Unhealthy",
                        Message = "运行前健康检查执行时发生异常"
                    }
                }
            });
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
    public long NodeId { get; init; }
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

/// <summary>
/// 运行前健康检查响应
/// </summary>
public class PreRunHealthCheckResponse
{
    /// <summary>整体状态: Healthy/Unhealthy</summary>
    public required string OverallStatus { get; init; }

    /// <summary>各项检查结果列表</summary>
    public required List<HealthCheckItemResponse> Checks { get; init; }
}

/// <summary>
/// 单项健康检查结果
/// </summary>
public class HealthCheckItemResponse
{
    /// <summary>检查项名称（英文标识符）</summary>
    public required string Name { get; init; }

    /// <summary>检查状态: Healthy/Unhealthy</summary>
    public required string Status { get; init; }

    /// <summary>检查结果描述（中文消息）</summary>
    public required string Message { get; init; }
}

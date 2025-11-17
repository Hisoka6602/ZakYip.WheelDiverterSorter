using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.Sorting.Core.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 统一配置管理 API 控制器
/// </summary>
/// <remarks>
/// 提供拓扑、分拣模式、异常策略、仿真场景配置的统一管理接口
/// </remarks>
[ApiController]
[Route("api/config")]
[Produces("application/json")]
public class ConfigurationController : ControllerBase
{
    private readonly ILineTopologyConfigProvider _topologyProvider;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        ILineTopologyConfigProvider topologyProvider,
        ISystemConfigurationRepository systemConfigRepository,
        ILogger<ConfigurationController> logger)
    {
        _topologyProvider = topologyProvider;
        _systemConfigRepository = systemConfigRepository;
        _logger = logger;
    }

    #region 线体拓扑配置

    /// <summary>
    /// 获取线体拓扑配置
    /// </summary>
    /// <returns>线体拓扑配置</returns>
    /// <response code="200">成功返回拓扑配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("topology")]
    [SwaggerOperation(
        Summary = "获取线体拓扑配置",
        Description = "返回当前系统的完整线体拓扑配置，包括所有摆轮节点和格口信息",
        OperationId = "GetTopology",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "成功返回拓扑配置", typeof(LineTopologyConfig))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(LineTopologyConfig), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<LineTopologyConfig>> GetTopology()
    {
        try
        {
            var topology = await _topologyProvider.GetTopologyAsync();
            return Ok(topology);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取线体拓扑配置失败");
            return StatusCode(500, new { message = "获取线体拓扑配置失败" });
        }
    }

    /// <summary>
    /// 更新线体拓扑配置
    /// </summary>
    /// <param name="config">线体拓扑配置</param>
    /// <returns>更新后的拓扑配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 注意：当前实现使用的是JSON文件作为配置源，更新操作暂不支持。
    /// 如需修改拓扑配置，请直接编辑配置文件并重启服务。
    /// 未来可扩展为支持数据库存储的动态更新。
    /// </remarks>
    [HttpPut("topology")]
    [SwaggerOperation(
        Summary = "更新线体拓扑配置",
        Description = "更新线体拓扑配置（当前版本暂不支持，需直接修改配置文件）",
        OperationId = "UpdateTopology",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(LineTopologyConfig))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(501, "功能暂未实现")]
    [ProducesResponseType(typeof(LineTopologyConfig), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 501)]
    public async Task<ActionResult<LineTopologyConfig>> UpdateTopology([FromBody] LineTopologyConfig config)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效", errors });
            }

            // 基本验证
            if (string.IsNullOrWhiteSpace(config.TopologyId))
            {
                return BadRequest(new { message = "拓扑ID不能为空" });
            }

            if (config.WheelNodes == null || config.WheelNodes.Count == 0)
            {
                return BadRequest(new { message = "摆轮节点列表不能为空" });
            }

            if (config.Chutes == null || config.Chutes.Count == 0)
            {
                return BadRequest(new { message = "格口列表不能为空" });
            }

            _logger.LogWarning("拓扑配置更新功能暂未实现，当前使用JSON文件配置");
            return StatusCode(501, new { message = "拓扑配置更新功能暂未实现，请直接修改配置文件" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新线体拓扑配置失败");
            return StatusCode(500, new { message = "更新线体拓扑配置失败" });
        }
    }

    #endregion

    #region 分拣模式配置

    /// <summary>
    /// 获取当前分拣模式
    /// </summary>
    /// <returns>分拣模式配置</returns>
    /// <response code="200">成功返回分拣模式</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("sorting-mode")]
    [SwaggerOperation(
        Summary = "获取当前分拣模式",
        Description = "返回系统当前使用的分拣模式（Formal/FixedChute/RoundRobin）",
        OperationId = "GetSortingMode",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "成功返回分拣模式", typeof(SortingModeResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SortingModeResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SortingModeResponse> GetSortingMode()
    {
        try
        {
            var config = _systemConfigRepository.Get();
            return Ok(new SortingModeResponse
            {
                SortingMode = config.SortingMode,
                FixedChuteId = config.FixedChuteId,
                AvailableChuteIds = config.AvailableChuteIds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分拣模式失败");
            return StatusCode(500, new { message = "获取分拣模式失败" });
        }
    }

    /// <summary>
    /// 更新分拣模式
    /// </summary>
    /// <param name="request">分拣模式配置请求</param>
    /// <returns>更新后的分拣模式</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/sorting-mode
    ///     {
    ///         "sortingMode": "RoundRobin",
    ///         "availableChuteIds": [1, 2, 3, 4, 5]
    ///     }
    /// 
    /// 分拣模式说明：
    /// - Formal：正式模式，根据上游RuleEngine分配的格口进行分拣
    /// - FixedChute：固定格口模式，所有包裹分拣到指定的固定格口，需设置fixedChuteId
    /// - RoundRobin：循环格口模式，包裹依次分拣到可用格口列表中的格口，需设置availableChuteIds
    /// </remarks>
    [HttpPut("sorting-mode")]
    [SwaggerOperation(
        Summary = "更新分拣模式",
        Description = "更新系统分拣模式，配置立即生效",
        OperationId = "UpdateSortingMode",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(SortingModeResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SortingModeResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SortingModeResponse> UpdateSortingMode([FromBody] SortingModeRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效", errors });
            }

            // 验证分拣模式相关配置
            if (request.SortingMode == SortingMode.FixedChute)
            {
                if (!request.FixedChuteId.HasValue || request.FixedChuteId.Value <= 0)
                {
                    return BadRequest(new { message = "指定落格分拣模式下，固定格口ID必须配置且大于0" });
                }
            }

            if (request.SortingMode == SortingMode.RoundRobin)
            {
                if (request.AvailableChuteIds == null || request.AvailableChuteIds.Count == 0)
                {
                    return BadRequest(new { message = "循环格口落格模式下，必须配置至少一个可用格口" });
                }

                if (request.AvailableChuteIds.Any(id => id <= 0))
                {
                    return BadRequest(new { message = "可用格口ID列表中不能包含小于等于0的值" });
                }
            }

            // 获取当前配置并更新
            var config = _systemConfigRepository.Get();
            config.SortingMode = request.SortingMode;
            config.FixedChuteId = request.FixedChuteId;
            config.AvailableChuteIds = request.AvailableChuteIds ?? new List<int>();
            config.UpdatedAt = DateTime.UtcNow;

            _systemConfigRepository.Update(config);

            _logger.LogInformation(
                "分拣模式已更新: SortingMode={SortingMode}",
                request.SortingMode);

            return Ok(new SortingModeResponse
            {
                SortingMode = config.SortingMode,
                FixedChuteId = config.FixedChuteId,
                AvailableChuteIds = config.AvailableChuteIds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新分拣模式失败");
            return StatusCode(500, new { message = "更新分拣模式失败" });
        }
    }

    #endregion

    #region 异常策略配置

    /// <summary>
    /// 获取异常路由策略
    /// </summary>
    /// <returns>异常路由策略</returns>
    /// <response code="200">成功返回异常策略</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("exception-policy")]
    [SwaggerOperation(
        Summary = "获取异常路由策略",
        Description = "返回系统当前的异常路由策略配置",
        OperationId = "GetExceptionPolicy",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "成功返回异常策略", typeof(ExceptionRoutingPolicy))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ExceptionRoutingPolicy), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ExceptionRoutingPolicy> GetExceptionPolicy()
    {
        try
        {
            var config = _systemConfigRepository.Get();
            return Ok(new ExceptionRoutingPolicy
            {
                ExceptionChuteId = config.ExceptionChuteId,
                UpstreamTimeoutMs = config.ChuteAssignmentTimeoutMs,
                RetryOnTimeout = config.RetryCount > 0,
                RetryCount = config.RetryCount,
                RetryDelayMs = config.RetryDelayMs,
                UseExceptionOnTopologyUnreachable = true,
                UseExceptionOnTtlFailure = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取异常路由策略失败");
            return StatusCode(500, new { message = "获取异常路由策略失败" });
        }
    }

    /// <summary>
    /// 更新异常路由策略
    /// </summary>
    /// <param name="policy">异常路由策略</param>
    /// <returns>更新后的异常策略</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/exception-policy
    ///     {
    ///         "exceptionChuteId": 999,
    ///         "upstreamTimeoutMs": 10000,
    ///         "retryOnTimeout": true,
    ///         "retryCount": 3,
    ///         "retryDelayMs": 1000,
    ///         "useExceptionOnTopologyUnreachable": true,
    ///         "useExceptionOnTtlFailure": true
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// </remarks>
    [HttpPut("exception-policy")]
    [SwaggerOperation(
        Summary = "更新异常路由策略",
        Description = "更新系统异常路由策略，配置立即生效",
        OperationId = "UpdateExceptionPolicy",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ExceptionRoutingPolicy))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ExceptionRoutingPolicy), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ExceptionRoutingPolicy> UpdateExceptionPolicy([FromBody] ExceptionRoutingPolicy policy)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效", errors });
            }

            // 验证异常格口ID
            if (policy.ExceptionChuteId <= 0)
            {
                return BadRequest(new { message = "异常格口ID必须大于0" });
            }

            if (policy.UpstreamTimeoutMs < 1000 || policy.UpstreamTimeoutMs > 60000)
            {
                return BadRequest(new { message = "上游超时时间必须在1000-60000毫秒之间" });
            }

            if (policy.RetryCount < 0 || policy.RetryCount > 10)
            {
                return BadRequest(new { message = "重试次数必须在0-10之间" });
            }

            if (policy.RetryDelayMs < 100 || policy.RetryDelayMs > 10000)
            {
                return BadRequest(new { message = "重试延迟必须在100-10000毫秒之间" });
            }

            // 获取当前配置并更新
            var config = _systemConfigRepository.Get();
            config.ExceptionChuteId = policy.ExceptionChuteId;
            config.ChuteAssignmentTimeoutMs = policy.UpstreamTimeoutMs;
            config.RetryCount = policy.RetryOnTimeout ? policy.RetryCount : 0;
            config.RetryDelayMs = policy.RetryDelayMs;
            config.UpdatedAt = DateTime.UtcNow;

            _systemConfigRepository.Update(config);

            _logger.LogInformation(
                "异常路由策略已更新: ExceptionChuteId={ExceptionChuteId}, Timeout={Timeout}ms",
                policy.ExceptionChuteId,
                policy.UpstreamTimeoutMs);

            return Ok(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新异常路由策略失败");
            return StatusCode(500, new { message = "更新异常路由策略失败" });
        }
    }

    #endregion

    #region 仿真场景配置

    /// <summary>
    /// 获取仿真场景配置
    /// </summary>
    /// <returns>仿真场景配置</returns>
    /// <response code="200">成功返回仿真配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前的仿真场景配置，包括包裹数量、间隔时间、分拣模式等参数。
    /// 实际配置由 SimulationConfigController 管理，此接口提供统一访问入口。
    /// </remarks>
    [HttpGet("simulation-scenario")]
    [SwaggerOperation(
        Summary = "获取仿真场景配置",
        Description = "返回当前的仿真场景配置参数",
        OperationId = "GetSimulationScenario",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "成功返回仿真配置")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult GetSimulationScenario()
    {
        try
        {
            _logger.LogInformation("获取仿真场景配置，请使用 /api/config/simulation 端点");
            return Ok(new 
            { 
                message = "请使用 GET /api/config/simulation 获取仿真配置",
                redirectUrl = "/api/config/simulation"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仿真场景配置失败");
            return StatusCode(500, new { message = "获取仿真场景配置失败" });
        }
    }

    /// <summary>
    /// 更新仿真场景配置
    /// </summary>
    /// <returns>更新结果</returns>
    /// <response code="200">提示使用正确的端点</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 仿真配置的更新请使用 PUT /api/config/simulation 端点。
    /// 此接口提供统一的配置管理入口导航。
    /// </remarks>
    [HttpPut("simulation-scenario")]
    [SwaggerOperation(
        Summary = "更新仿真场景配置",
        Description = "更新仿真场景配置（请使用 /api/config/simulation 端点）",
        OperationId = "UpdateSimulationScenario",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "提示使用正确的端点")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult UpdateSimulationScenario()
    {
        try
        {
            _logger.LogInformation("更新仿真场景配置，请使用 /api/config/simulation 端点");
            return Ok(new 
            { 
                message = "请使用 PUT /api/config/simulation 更新仿真配置",
                redirectUrl = "/api/config/simulation"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新仿真场景配置失败");
            return StatusCode(500, new { message = "更新仿真场景配置失败" });
        }
    }

    #endregion
}

/// <summary>
/// 分拣模式响应模型
/// </summary>
public class SortingModeResponse
{
    /// <summary>分拣模式</summary>
    public SortingMode SortingMode { get; set; }

    /// <summary>固定格口ID（仅在FixedChute模式下使用）</summary>
    public int? FixedChuteId { get; set; }

    /// <summary>可用格口ID列表（仅在RoundRobin模式下使用）</summary>
    public List<int> AvailableChuteIds { get; set; } = new();
}

/// <summary>
/// 分拣模式请求模型
/// </summary>
public class SortingModeRequest
{
    /// <summary>分拣模式</summary>
    public SortingMode SortingMode { get; set; }

    /// <summary>固定格口ID（仅在FixedChute模式下使用）</summary>
    public int? FixedChuteId { get; set; }

    /// <summary>可用格口ID列表（仅在RoundRobin模式下使用）</summary>
    public List<int>? AvailableChuteIds { get; set; }
}

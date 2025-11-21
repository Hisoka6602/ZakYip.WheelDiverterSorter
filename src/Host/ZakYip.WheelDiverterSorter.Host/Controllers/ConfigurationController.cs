using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

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
    private readonly ISystemClock _clock;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        ILineTopologyConfigProvider topologyProvider,
        ISystemConfigurationRepository systemConfigRepository,
        ISystemClock clock,
        ILogger<ConfigurationController> logger)
    {
        _topologyProvider = topologyProvider;
        _systemConfigRepository = systemConfigRepository;
        _clock = clock;
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
    /// 
    /// **拓扑配置 vs 路由配置的区别：**
    /// 
    /// **拓扑配置**（本端点）：
    /// - 定义物理设备和连接关系（哪些摆轮、哪些格口、如何连接）
    /// - 低频变化（硬件安装、线体改造时）
    /// - 当前需要通过编辑配置文件并重启服务来更新
    /// 
    /// **路由配置**（/api/config/routes）：
    /// - 定义业务规则（包裹如何分拣到格口、摆轮如何切换）
    /// - 较高频变化（业务规则调整时）
    /// - 支持通过 API 热更新，立即生效
    /// 
    /// **推荐实践**：
    /// - 日常业务调整请使用路由配置 API
    /// - 仅在硬件变更时才需要修改拓扑配置
    /// 
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
        await Task.Yield();
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

            if (config.WheelNodes.Count == 0)
            {
                return BadRequest(new { message = "摆轮节点列表不能为空" });
            }

            if (config.Chutes.Count == 0)
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

    #endregion 线体拓扑配置

    // 注意：分拣模式配置已移至 SystemConfigController (/api/config/system/sorting-mode)
    // 原 /api/config/sorting-mode 端点已删除以避免重复

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
            config.UpdatedAt = _clock.LocalNow;

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

    #endregion 异常策略配置

    // 注意：仿真场景配置已移至 SimulationConfigController (/api/config/simulation)
    // 原 /api/config/simulation-scenario 端点已删除以避免重复

    #region 放包节流配置

    /// <summary>
    /// 获取放包节流配置
    /// </summary>
    /// <returns>放包节流配置</returns>
    /// <response code="200">成功返回节流配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("release-throttle")]
    [SwaggerOperation(
        Summary = "获取放包节流配置",
        Description = "返回当前的拥堵检测与放包节流配置",
        OperationId = "GetReleaseThrottle",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "成功返回节流配置", typeof(ReleaseThrottleConfigResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ReleaseThrottleConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ReleaseThrottleConfigResponse> GetReleaseThrottle()
    {
        try
        {
            var config = _systemConfigRepository.Get();
            return Ok(new ReleaseThrottleConfigResponse
            {
                WarningThresholdLatencyMs = config.ThrottleWarningLatencyMs,
                SevereThresholdLatencyMs = config.ThrottleSevereLatencyMs,
                WarningThresholdSuccessRate = config.ThrottleWarningSuccessRate,
                SevereThresholdSuccessRate = config.ThrottleSevereSuccessRate,
                WarningThresholdInFlightParcels = config.ThrottleWarningInFlightParcels,
                SevereThresholdInFlightParcels = config.ThrottleSevereInFlightParcels,
                NormalReleaseIntervalMs = config.ThrottleNormalIntervalMs,
                WarningReleaseIntervalMs = config.ThrottleWarningIntervalMs,
                SevereReleaseIntervalMs = config.ThrottleSevereIntervalMs,
                ShouldPauseOnSevere = config.ThrottleShouldPauseOnSevere,
                EnableThrottling = config.ThrottleEnabled,
                MetricsTimeWindowSeconds = config.ThrottleMetricsWindowSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取放包节流配置失败");
            return StatusCode(500, new { message = "获取放包节流配置失败" });
        }
    }

    /// <summary>
    /// 更新放包节流配置
    /// </summary>
    /// <param name="request">放包节流配置请求</param>
    /// <returns>更新后的节流配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     PUT /api/config/release-throttle
    ///     {
    ///         "warningThresholdLatencyMs": 5000,
    ///         "severeThresholdLatencyMs": 10000,
    ///         "warningThresholdSuccessRate": 0.9,
    ///         "severeThresholdSuccessRate": 0.7,
    ///         "warningThresholdInFlightParcels": 50,
    ///         "severeThresholdInFlightParcels": 100,
    ///         "normalReleaseIntervalMs": 300,
    ///         "warningReleaseIntervalMs": 500,
    ///         "severeReleaseIntervalMs": 1000,
    ///         "shouldPauseOnSevere": false,
    ///         "enableThrottling": true,
    ///         "metricsTimeWindowSeconds": 60
    ///     }
    ///
    /// 配置说明：
    /// - 拥堵检测基于三个维度：延迟、成功率、在途包裹数
    /// - 任一维度达到阈值即触发相应拥堵级别
    /// - 根据拥堵级别动态调整放包间隔
    /// - 可配置严重拥堵时是否暂停放包
    /// </remarks>
    [HttpPut("release-throttle")]
    [SwaggerOperation(
        Summary = "更新放包节流配置",
        Description = "更新拥堵检测与放包节流配置，配置立即生效",
        OperationId = "UpdateReleaseThrottle",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ReleaseThrottleConfigResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ReleaseThrottleConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ReleaseThrottleConfigResponse> UpdateReleaseThrottle([FromBody] ReleaseThrottleConfigRequest request)
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

            // 验证延迟阈值
            if (request.WarningThresholdLatencyMs < 1000 || request.WarningThresholdLatencyMs > 60000)
            {
                return BadRequest(new { message = "警告延迟阈值必须在1000-60000毫秒之间" });
            }

            if (request.SevereThresholdLatencyMs < request.WarningThresholdLatencyMs || request.SevereThresholdLatencyMs > 120000)
            {
                return BadRequest(new { message = "严重延迟阈值必须大于警告阈值且不超过120000毫秒" });
            }

            // 验证成功率阈值
            if (request.WarningThresholdSuccessRate < 0.5 || request.WarningThresholdSuccessRate > 1.0)
            {
                return BadRequest(new { message = "警告成功率阈值必须在0.5-1.0之间" });
            }

            if (request.SevereThresholdSuccessRate < 0.0 || request.SevereThresholdSuccessRate > request.WarningThresholdSuccessRate)
            {
                return BadRequest(new { message = "严重成功率阈值必须小于警告阈值且不低于0.0" });
            }

            // 验证在途包裹数阈值
            if (request.WarningThresholdInFlightParcels < 10 || request.WarningThresholdInFlightParcels > 1000)
            {
                return BadRequest(new { message = "警告在途包裹数阈值必须在10-1000之间" });
            }

            if (request.SevereThresholdInFlightParcels < request.WarningThresholdInFlightParcels || request.SevereThresholdInFlightParcels > 2000)
            {
                return BadRequest(new { message = "严重在途包裹数阈值必须大于警告阈值且不超过2000" });
            }

            // 验证放包间隔
            if (request.NormalReleaseIntervalMs < 100 || request.NormalReleaseIntervalMs > 5000)
            {
                return BadRequest(new { message = "正常放包间隔必须在100-5000毫秒之间" });
            }

            if (request.WarningReleaseIntervalMs < request.NormalReleaseIntervalMs || request.WarningReleaseIntervalMs > 10000)
            {
                return BadRequest(new { message = "警告放包间隔必须大于正常间隔且不超过10000毫秒" });
            }

            if (request.SevereReleaseIntervalMs < request.WarningReleaseIntervalMs || request.SevereReleaseIntervalMs > 30000)
            {
                return BadRequest(new { message = "严重放包间隔必须大于警告间隔且不超过30000毫秒" });
            }

            // 验证指标时间窗口
            if (request.MetricsTimeWindowSeconds < 10 || request.MetricsTimeWindowSeconds > 600)
            {
                return BadRequest(new { message = "指标时间窗口必须在10-600秒之间" });
            }

            // 获取当前配置并更新
            var config = _systemConfigRepository.Get();
            config.ThrottleWarningLatencyMs = request.WarningThresholdLatencyMs;
            config.ThrottleSevereLatencyMs = request.SevereThresholdLatencyMs;
            config.ThrottleWarningSuccessRate = request.WarningThresholdSuccessRate;
            config.ThrottleSevereSuccessRate = request.SevereThresholdSuccessRate;
            config.ThrottleWarningInFlightParcels = request.WarningThresholdInFlightParcels;
            config.ThrottleSevereInFlightParcels = request.SevereThresholdInFlightParcels;
            config.ThrottleNormalIntervalMs = request.NormalReleaseIntervalMs;
            config.ThrottleWarningIntervalMs = request.WarningReleaseIntervalMs;
            config.ThrottleSevereIntervalMs = request.SevereReleaseIntervalMs;
            config.ThrottleShouldPauseOnSevere = request.ShouldPauseOnSevere;
            config.ThrottleEnabled = request.EnableThrottling;
            config.ThrottleMetricsWindowSeconds = request.MetricsTimeWindowSeconds;
            config.UpdatedAt = _clock.LocalNow;

            _systemConfigRepository.Update(config);

            _logger.LogInformation(
                "放包节流配置已更新: EnableThrottling={EnableThrottling}, NormalInterval={NormalInterval}ms",
                request.EnableThrottling,
                request.NormalReleaseIntervalMs);

            return Ok(new ReleaseThrottleConfigResponse
            {
                WarningThresholdLatencyMs = config.ThrottleWarningLatencyMs,
                SevereThresholdLatencyMs = config.ThrottleSevereLatencyMs,
                WarningThresholdSuccessRate = config.ThrottleWarningSuccessRate,
                SevereThresholdSuccessRate = config.ThrottleSevereSuccessRate,
                WarningThresholdInFlightParcels = config.ThrottleWarningInFlightParcels,
                SevereThresholdInFlightParcels = config.ThrottleSevereInFlightParcels,
                NormalReleaseIntervalMs = config.ThrottleNormalIntervalMs,
                WarningReleaseIntervalMs = config.ThrottleWarningIntervalMs,
                SevereReleaseIntervalMs = config.ThrottleSevereIntervalMs,
                ShouldPauseOnSevere = config.ThrottleShouldPauseOnSevere,
                EnableThrottling = config.ThrottleEnabled,
                MetricsTimeWindowSeconds = config.ThrottleMetricsWindowSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新放包节流配置失败");
            return StatusCode(500, new { message = "更新放包节流配置失败" });
        }
    }

    #endregion 放包节流配置
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

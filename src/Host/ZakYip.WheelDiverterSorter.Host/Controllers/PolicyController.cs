using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 策略管理 API 控制器
/// </summary>
/// <remarks>
/// 提供分拣策略的统一管理接口，包括：
/// - 异常路由策略
/// - 放包节流策略
/// - 超载处置策略
/// 所有策略配置支持热更新，立即生效无需重启服务
/// </remarks>
[ApiController]
[Route("api/policy")]
[Produces("application/json")]
public class PolicyController : ApiControllerBase
{
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly IOptionsMonitor<OverloadPolicyConfiguration> _overloadPolicyOptions;
    private readonly ISystemClock _clock;
    private readonly ILogger<PolicyController> _logger;
    private static OverloadPolicyConfiguration? _runtimeOverloadConfig;
    private static readonly object _overloadConfigLock = new();

    public PolicyController(
        ISystemConfigurationRepository systemConfigRepository,
        IOptionsMonitor<OverloadPolicyConfiguration> overloadPolicyOptions,
        ISystemClock clock,
        ILogger<PolicyController> logger)
    {
        _systemConfigRepository = systemConfigRepository;
        _overloadPolicyOptions = overloadPolicyOptions;
        _clock = clock;
        _logger = logger;
    }

    #region 异常路由策略

    /// <summary>
    /// 获取异常路由策略
    /// </summary>
    /// <returns>异常路由策略</returns>
    /// <response code="200">成功返回异常策略</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("exception-routing")]
    [SwaggerOperation(
        Summary = "获取异常路由策略",
        Description = "返回系统当前的异常路由策略配置",
        OperationId = "GetExceptionRoutingPolicy",
        Tags = new[] { "策略管理" }
    )]
    [SwaggerResponse(200, "成功返回异常策略", typeof(ApiResponse<ExceptionRoutingPolicy>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<ExceptionRoutingPolicy>))]
    [ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 200)]
    [ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 500)]
    public ActionResult<ApiResponse<ExceptionRoutingPolicy>> GetExceptionRoutingPolicy()
    {
        try
        {
            var config = _systemConfigRepository.Get();
            var policy = new ExceptionRoutingPolicy
            {
                ExceptionChuteId = config.ExceptionChuteId,
                UpstreamTimeoutMs = (int)(config.ChuteAssignmentTimeout?.FallbackTimeoutSeconds ?? 5m) * 1000,
                // 注意：重试相关配置已迁移到 CommunicationConfiguration
                RetryOnTimeout = false,  // 默认不重试，通信重试由 CommunicationConfiguration 管理
                RetryCount = 0,
                RetryDelayMs = 1000,
                UseExceptionOnTopologyUnreachable = true,
                UseExceptionOnTtlFailure = true
            };
            
            return Success(policy, "获取异常路由策略成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取异常路由策略失败");
            return ServerError<ExceptionRoutingPolicy>("获取异常路由策略失败");
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
    ///     PUT /api/policy/exception-routing
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
    [HttpPut("exception-routing")]
    [SwaggerOperation(
        Summary = "更新异常路由策略",
        Description = "更新系统异常路由策略，配置立即生效",
        OperationId = "UpdateExceptionRoutingPolicy",
        Tags = new[] { "策略管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<ExceptionRoutingPolicy>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<ExceptionRoutingPolicy>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<ExceptionRoutingPolicy>))]
    [ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 200)]
    [ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 400)]
    [ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 500)]
    public ActionResult<ApiResponse<ExceptionRoutingPolicy>> UpdateExceptionRoutingPolicy([FromBody] ExceptionRoutingPolicy policy)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationError<ExceptionRoutingPolicy>();
            }

            // 验证异常格口ID
            if (policy.ExceptionChuteId <= 0)
            {
                return ValidationError<ExceptionRoutingPolicy>("异常格口ID必须大于0");
            }

            if (policy.UpstreamTimeoutMs < 1000 || policy.UpstreamTimeoutMs > 60000)
            {
                return ValidationError<ExceptionRoutingPolicy>("上游超时时间必须在1000-60000毫秒之间");
            }

            if (policy.RetryCount < 0 || policy.RetryCount > 10)
            {
                return ValidationError<ExceptionRoutingPolicy>("重试次数必须在0-10之间");
            }

            if (policy.RetryDelayMs < 100 || policy.RetryDelayMs > 10000)
            {
                return ValidationError<ExceptionRoutingPolicy>("重试延迟必须在100-10000毫秒之间");
            }

            // 获取当前配置并更新
            var config = _systemConfigRepository.Get();
            config.ExceptionChuteId = policy.ExceptionChuteId;
            // 更新超时配置 - 将毫秒转换为秒
            if (config.ChuteAssignmentTimeout == null)
            {
                config.ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions();
            }
            config.ChuteAssignmentTimeout.FallbackTimeoutSeconds = policy.UpstreamTimeoutMs / 1000m;
            config.UpdatedAt = _clock.LocalNow;

            _systemConfigRepository.Update(config);

            _logger.LogInformation(
                "异常路由策略已更新: ExceptionChuteId={ExceptionChuteId}, Timeout={Timeout}ms",
                policy.ExceptionChuteId,
                policy.UpstreamTimeoutMs);

            return Success(policy, "异常路由策略已更新");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新异常路由策略失败");
            return ServerError<ExceptionRoutingPolicy>("更新异常路由策略失败");
        }
    }

    #endregion 异常路由策略

    #region 放包节流策略

    /// <summary>
    /// 获取放包节流策略
    /// </summary>
    /// <returns>放包节流策略</returns>
    /// <response code="200">成功返回节流策略</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("release-throttle")]
    [SwaggerOperation(
        Summary = "获取放包节流策略",
        Description = "返回当前的拥堵检测与放包节流配置",
        OperationId = "GetReleaseThrottlePolicy",
        Tags = new[] { "策略管理" }
    )]
    [SwaggerResponse(200, "成功返回节流策略", typeof(ApiResponse<ReleaseThrottleConfigResponse>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<ReleaseThrottleConfigResponse>))]
    [ProducesResponseType(typeof(ApiResponse<ReleaseThrottleConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseThrottleConfigResponse>), 500)]
    public ActionResult<ApiResponse<ReleaseThrottleConfigResponse>> GetReleaseThrottlePolicy()
    {
        try
        {
            var config = _systemConfigRepository.Get();
            var response = new ReleaseThrottleConfigResponse
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
            };
            
            return Success(response, "获取放包节流策略成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取放包节流策略失败");
            return ServerError<ReleaseThrottleConfigResponse>("获取放包节流策略失败");
        }
    }

    /// <summary>
    /// 更新放包节流策略
    /// </summary>
    /// <param name="request">放包节流策略请求</param>
    /// <returns>更新后的节流策略</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     PUT /api/policy/release-throttle
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
        Summary = "更新放包节流策略",
        Description = "更新拥堵检测与放包节流配置，配置立即生效",
        OperationId = "UpdateReleaseThrottlePolicy",
        Tags = new[] { "策略管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<ReleaseThrottleConfigResponse>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<ReleaseThrottleConfigResponse>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<ReleaseThrottleConfigResponse>))]
    [ProducesResponseType(typeof(ApiResponse<ReleaseThrottleConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseThrottleConfigResponse>), 400)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseThrottleConfigResponse>), 500)]
    public ActionResult<ApiResponse<ReleaseThrottleConfigResponse>> UpdateReleaseThrottlePolicy([FromBody] ReleaseThrottleConfigRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationError<ReleaseThrottleConfigResponse>();
            }

            // 验证延迟阈值
            if (request.WarningThresholdLatencyMs < 1000 || request.WarningThresholdLatencyMs > 60000)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("警告延迟阈值必须在1000-60000毫秒之间");
            }

            if (request.SevereThresholdLatencyMs < request.WarningThresholdLatencyMs || request.SevereThresholdLatencyMs > 120000)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("严重延迟阈值必须大于警告阈值且不超过120000毫秒");
            }

            // 验证成功率阈值
            if (request.WarningThresholdSuccessRate < 0.5 || request.WarningThresholdSuccessRate > 1.0)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("警告成功率阈值必须在0.5-1.0之间");
            }

            if (request.SevereThresholdSuccessRate < 0.0 || request.SevereThresholdSuccessRate > request.WarningThresholdSuccessRate)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("严重成功率阈值必须小于警告阈值且不低于0.0");
            }

            // 验证在途包裹数阈值
            if (request.WarningThresholdInFlightParcels < 10 || request.WarningThresholdInFlightParcels > 1000)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("警告在途包裹数阈值必须在10-1000之间");
            }

            if (request.SevereThresholdInFlightParcels < request.WarningThresholdInFlightParcels || request.SevereThresholdInFlightParcels > 2000)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("严重在途包裹数阈值必须大于警告阈值且不超过2000");
            }

            // 验证放包间隔
            if (request.NormalReleaseIntervalMs < 100 || request.NormalReleaseIntervalMs > 5000)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("正常放包间隔必须在100-5000毫秒之间");
            }

            if (request.WarningReleaseIntervalMs < request.NormalReleaseIntervalMs || request.WarningReleaseIntervalMs > 10000)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("警告放包间隔必须大于正常间隔且不超过10000毫秒");
            }

            if (request.SevereReleaseIntervalMs < request.WarningReleaseIntervalMs || request.SevereReleaseIntervalMs > 30000)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("严重放包间隔必须大于警告间隔且不超过30000毫秒");
            }

            // 验证指标时间窗口
            if (request.MetricsTimeWindowSeconds < 10 || request.MetricsTimeWindowSeconds > 600)
            {
                return ValidationError<ReleaseThrottleConfigResponse>("指标时间窗口必须在10-600秒之间");
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
                "放包节流策略已更新: EnableThrottling={EnableThrottling}, NormalInterval={NormalInterval}ms",
                request.EnableThrottling,
                request.NormalReleaseIntervalMs);

            var response = new ReleaseThrottleConfigResponse
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
            };
            
            return Success(response, "放包节流策略已更新");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新放包节流策略失败");
            return ServerError<ReleaseThrottleConfigResponse>("更新放包节流策略失败");
        }
    }

    #endregion 放包节流策略

    #region 超载处置策略

    /// <summary>
    /// 获取超载处置策略
    /// </summary>
    /// <returns>超载处置策略</returns>
    /// <response code="200">成功返回超载策略</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("overload")]
    [SwaggerOperation(
        Summary = "获取超载处置策略",
        Description = "返回当前系统的超载处置策略配置，包括各项阈值和开关设置",
        OperationId = "GetOverloadPolicy",
        Tags = new[] { "策略管理" }
    )]
    [SwaggerResponse(200, "成功返回超载策略", typeof(ApiResponse<OverloadPolicyDto>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<OverloadPolicyDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<OverloadPolicyDto>> GetOverloadPolicy()
    {
        try
        {
            lock (_overloadConfigLock)
            {
                var config = _runtimeOverloadConfig ?? _overloadPolicyOptions.CurrentValue;
                
                var dto = new OverloadPolicyDto
                {
                    Enabled = config.Enabled,
                    ForceExceptionOnSevere = config.ForceExceptionOnSevere,
                    ForceExceptionOnOverCapacity = config.ForceExceptionOnOverCapacity,
                    ForceExceptionOnTimeout = config.ForceExceptionOnTimeout,
                    ForceExceptionOnWindowMiss = config.ForceExceptionOnWindowMiss,
                    MaxInFlightParcels = config.MaxInFlightParcels,
                    MinRequiredTtlMs = config.MinRequiredTtlMs,
                    MinArrivalWindowMs = config.MinArrivalWindowMs
                };
                return Success(dto, "获取超载处置策略成功");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取超载处置策略失败");
            return ServerError<OverloadPolicyDto>("获取超载处置策略失败");
        }
    }

    /// <summary>
    /// 更新超载处置策略
    /// </summary>
    /// <param name="dto">超载处置策略</param>
    /// <returns>更新后的超载策略</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/policy/overload
    ///     {
    ///         "enabled": true,
    ///         "forceExceptionOnSevere": true,
    ///         "forceExceptionOnOverCapacity": false,
    ///         "forceExceptionOnTimeout": true,
    ///         "forceExceptionOnWindowMiss": false,
    ///         "maxInFlightParcels": 120,
    ///         "minRequiredTtlMs": 500,
    ///         "minArrivalWindowMs": 200
    ///     }
    /// 
    /// 配置说明：
    /// - enabled: 是否启用超载策略检查
    /// - forceExceptionOnSevere: 严重拥堵时是否强制走异常格口
    /// - forceExceptionOnOverCapacity: 超容量时是否强制走异常格口
    /// - forceExceptionOnTimeout: 超时时是否强制走异常格口
    /// - forceExceptionOnWindowMiss: 错过窗口期时是否强制走异常格口
    /// - maxInFlightParcels: 最大在途包裹数
    /// - minRequiredTtlMs: 最小所需TTL（毫秒）
    /// - minArrivalWindowMs: 最小到达窗口（毫秒）
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// </remarks>
    [HttpPut("overload")]
    [SwaggerOperation(
        Summary = "更新超载处置策略",
        Description = "更新系统超载处置策略配置，配置立即生效",
        OperationId = "UpdateOverloadPolicy",
        Tags = new[] { "策略管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<OverloadPolicyDto>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<OverloadPolicyDto>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<OverloadPolicyDto>))]
    [ProducesResponseType(typeof(ApiResponse<OverloadPolicyDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<OverloadPolicyDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse<OverloadPolicyDto>), 500)]
    public ActionResult<ApiResponse<OverloadPolicyDto>> UpdateOverloadPolicy([FromBody] OverloadPolicyDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationError<OverloadPolicyDto>();
            }

            // 验证参数
            if (dto.MaxInFlightParcels < 1 || dto.MaxInFlightParcels > 1000)
            {
                return ValidationError<OverloadPolicyDto>("最大在途包裹数必须在1-1000之间");
            }

            if (dto.MinRequiredTtlMs < 100 || dto.MinRequiredTtlMs > 10000)
            {
                return ValidationError<OverloadPolicyDto>("最小所需TTL必须在100-10000毫秒之间");
            }

            if (dto.MinArrivalWindowMs < 50 || dto.MinArrivalWindowMs > 5000)
            {
                return ValidationError<OverloadPolicyDto>("最小到达窗口必须在50-5000毫秒之间");
            }

            // 创建新的配置
            var config = new OverloadPolicyConfiguration
            {
                Enabled = dto.Enabled,
                ForceExceptionOnSevere = dto.ForceExceptionOnSevere,
                ForceExceptionOnOverCapacity = dto.ForceExceptionOnOverCapacity,
                ForceExceptionOnTimeout = dto.ForceExceptionOnTimeout,
                ForceExceptionOnWindowMiss = dto.ForceExceptionOnWindowMiss,
                MaxInFlightParcels = dto.MaxInFlightParcels,
                MinRequiredTtlMs = dto.MinRequiredTtlMs,
                MinArrivalWindowMs = dto.MinArrivalWindowMs
            };

            // 保存到运行时配置（热更新）
            lock (_overloadConfigLock)
            {
                _runtimeOverloadConfig = config;
            }

            _logger.LogInformation(
                "超载处置策略已更新: Enabled={Enabled}, MaxInFlightParcels={MaxInFlightParcels}, " +
                "ForceExceptionOnSevere={ForceExceptionOnSevere}",
                config.Enabled,
                config.MaxInFlightParcels,
                config.ForceExceptionOnSevere);

            return Success(dto, "超载处置策略已更新");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新超载处置策略失败");
            return ServerError<OverloadPolicyDto>("更新超载处置策略失败");
        }
    }

    /// <summary>
    /// 获取当前运行时的超载策略配置（用于内部服务访问）
    /// </summary>
    /// <returns>超载策略配置</returns>
    internal static OverloadPolicyConfiguration? GetRuntimeOverloadPolicy()
    {
        lock (_overloadConfigLock)
        {
            return _runtimeOverloadConfig;
        }
    }

    #endregion 超载处置策略
}

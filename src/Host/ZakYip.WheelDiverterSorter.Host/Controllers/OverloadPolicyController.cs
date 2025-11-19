using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 超载策略配置 API 控制器
/// </summary>
/// <remarks>
/// 提供超载策略的配置管理接口，用于动态调整系统超载处置行为
/// </remarks>
[ApiController]
[Route("api/config")]
[Produces("application/json")]
public class OverloadPolicyController : ControllerBase
{
    private readonly IOptionsMonitor<OverloadPolicyConfiguration> _policyOptions;
    private readonly ILogger<OverloadPolicyController> _logger;
    private static OverloadPolicyConfiguration? _runtimeConfig;
    private static readonly object _configLock = new object();

    public OverloadPolicyController(
        IOptionsMonitor<OverloadPolicyConfiguration> policyOptions,
        ILogger<OverloadPolicyController> logger)
    {
        _policyOptions = policyOptions;
        _logger = logger;
    }

    #region 超载策略配置

    /// <summary>
    /// 获取当前超载策略配置
    /// </summary>
    /// <returns>超载策略配置</returns>
    /// <response code="200">成功返回超载策略配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("overload-policy")]
    [SwaggerOperation(
        Summary = "获取超载策略配置",
        Description = "返回当前系统的超载策略配置，包括各项阈值和开关设置",
        OperationId = "GetOverloadPolicy",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "成功返回超载策略配置", typeof(OverloadPolicyDto))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(OverloadPolicyDto), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<OverloadPolicyDto> GetOverloadPolicy()
    {
        try
        {
            lock (_configLock)
            {
                var config = _runtimeConfig ?? _policyOptions.CurrentValue;
                
                return Ok(new OverloadPolicyDto
                {
                    Enabled = config.Enabled,
                    ForceExceptionOnSevere = config.ForceExceptionOnSevere,
                    ForceExceptionOnOverCapacity = config.ForceExceptionOnOverCapacity,
                    ForceExceptionOnTimeout = config.ForceExceptionOnTimeout,
                    ForceExceptionOnWindowMiss = config.ForceExceptionOnWindowMiss,
                    MaxInFlightParcels = config.MaxInFlightParcels,
                    MinRequiredTtlMs = config.MinRequiredTtlMs,
                    MinArrivalWindowMs = config.MinArrivalWindowMs
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取超载策略配置失败");
            return StatusCode(500, new { message = "获取超载策略配置失败" });
        }
    }

    /// <summary>
    /// 更新超载策略配置
    /// </summary>
    /// <param name="dto">超载策略配置</param>
    /// <returns>更新后的超载策略配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/overload-policy
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
    /// - enabled: 是否启用超载检测和处置
    /// - forceExceptionOnSevere: 严重拥堵时是否直接路由到异常口
    /// - forceExceptionOnOverCapacity: 超过在途包裹容量时是否强制异常
    /// - forceExceptionOnTimeout: TTL不足时是否强制异常
    /// - forceExceptionOnWindowMiss: 到达窗口不足时是否强制异常
    /// - maxInFlightParcels: 最大允许在途包裹数（null表示不限制）
    /// - minRequiredTtlMs: 最小所需剩余TTL（毫秒）
    /// - minArrivalWindowMs: 最小到达窗口（毫秒）
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// </remarks>
    [HttpPut("overload-policy")]
    [SwaggerOperation(
        Summary = "更新超载策略配置",
        Description = "更新系统超载策略配置，配置立即生效",
        OperationId = "UpdateOverloadPolicy",
        Tags = new[] { "配置管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(OverloadPolicyDto))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(OverloadPolicyDto), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<OverloadPolicyDto> UpdateOverloadPolicy([FromBody] OverloadPolicyDto dto)
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

            // 验证参数
            if (dto.MinRequiredTtlMs < 0 || dto.MinRequiredTtlMs > 60000)
            {
                return BadRequest(new { message = "最小TTL必须在0-60000毫秒之间" });
            }

            if (dto.MinArrivalWindowMs < 0 || dto.MinArrivalWindowMs > 30000)
            {
                return BadRequest(new { message = "最小到达窗口必须在0-30000毫秒之间" });
            }

            if (dto.MaxInFlightParcels.HasValue && 
                (dto.MaxInFlightParcels.Value < 10 || dto.MaxInFlightParcels.Value > 2000))
            {
                return BadRequest(new { message = "最大在途包裹数必须在10-2000之间" });
            }

            // 创建新配置
            var newConfig = new OverloadPolicyConfiguration
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

            // 更新运行时配置
            lock (_configLock)
            {
                _runtimeConfig = newConfig;
            }

            _logger.LogInformation(
                "超载策略配置已更新: Enabled={Enabled}, MaxInFlightParcels={MaxInFlight}",
                newConfig.Enabled,
                newConfig.MaxInFlightParcels);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新超载策略配置失败");
            return StatusCode(500, new { message = "更新超载策略配置失败" });
        }
    }

    /// <summary>
    /// 获取当前运行时超载策略配置（用于依赖注入）
    /// </summary>
    public static OverloadPolicyConfiguration? GetRuntimeConfig()
    {
        lock (_configLock)
        {
            return _runtimeConfig;
        }
    }

    #endregion
}

/// <summary>
/// 超载策略配置 DTO
/// </summary>
public class OverloadPolicyDto
{
    /// <summary>是否启用超载检测和处置（默认：true）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>严重拥堵时是否直接路由到异常口（默认：true）</summary>
    public bool ForceExceptionOnSevere { get; set; } = true;

    /// <summary>超过在途包裹容量时是否强制异常（默认：false，仅打标记）</summary>
    public bool ForceExceptionOnOverCapacity { get; set; } = false;

    /// <summary>TTL不足时是否强制异常（默认：true）</summary>
    public bool ForceExceptionOnTimeout { get; set; } = true;

    /// <summary>到达窗口不足时是否强制异常（默认：false）</summary>
    public bool ForceExceptionOnWindowMiss { get; set; } = false;

    /// <summary>最大允许在途包裹数（null表示不限制，默认：null）</summary>
    public int? MaxInFlightParcels { get; set; }

    /// <summary>最小所需剩余TTL（毫秒，默认：500）</summary>
    public double MinRequiredTtlMs { get; set; } = 500;

    /// <summary>最小到达窗口（毫秒，默认：200）</summary>
    public double MinArrivalWindowMs { get; set; } = 200;
}

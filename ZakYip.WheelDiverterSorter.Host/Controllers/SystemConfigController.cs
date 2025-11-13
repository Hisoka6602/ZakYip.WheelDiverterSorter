using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 系统配置管理API控制器
/// </summary>
/// <remarks>
/// 提供系统配置的查询和更新功能，支持热更新
/// </remarks>
[ApiController]
[Route("api/config/system")]
[Produces("application/json")]
public class SystemConfigController : ControllerBase
{
    private readonly ISystemConfigurationRepository _repository;
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ILogger<SystemConfigController> _logger;

    public SystemConfigController(
        ISystemConfigurationRepository repository,
        IRouteConfigurationRepository routeRepository,
        ILogger<SystemConfigController> logger)
    {
        _repository = repository;
        _routeRepository = routeRepository;
        _logger = logger;
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <returns>系统配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(SystemConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SystemConfigResponse> GetSystemConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统配置失败");
            return StatusCode(500, new { message = "获取系统配置失败" });
        }
    }

    /// <summary>
    /// 获取默认系统配置模板
    /// </summary>
    /// <returns>默认系统配置模板</returns>
    /// <response code="200">成功返回模板</response>
    /// <remarks>
    /// 返回系统默认配置，可用作配置文件模板
    /// </remarks>
    [HttpGet("template")]
    [ProducesResponseType(typeof(SystemConfigRequest), 200)]
    public ActionResult<SystemConfigRequest> GetTemplate()
    {
        var defaultConfig = SystemConfiguration.GetDefault();
        return Ok(new SystemConfigRequest
        {
            ExceptionChuteId = ChuteIdHelper.FormatChuteId(defaultConfig.ExceptionChuteId),
            MqttDefaultPort = defaultConfig.MqttDefaultPort,
            TcpDefaultPort = defaultConfig.TcpDefaultPort,
            ChuteAssignmentTimeoutMs = defaultConfig.ChuteAssignmentTimeoutMs,
            RequestTimeoutMs = defaultConfig.RequestTimeoutMs,
            RetryCount = defaultConfig.RetryCount,
            RetryDelayMs = defaultConfig.RetryDelayMs,
            EnableAutoReconnect = defaultConfig.EnableAutoReconnect
        });
    }

    /// <summary>
    /// 更新系统配置（支持热更新）
    /// </summary>
    /// <param name="request">系统配置请求</param>
    /// <returns>更新后的系统配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/system
    ///     {
    ///         "exceptionChuteId": "CHUTE_EXCEPTION",
    ///         "mqttDefaultPort": 1883,
    ///         "tcpDefaultPort": 8888,
    ///         "chuteAssignmentTimeoutMs": 10000,
    ///         "requestTimeoutMs": 5000,
    ///         "retryCount": 3,
    ///         "retryDelayMs": 1000,
    ///         "enableAutoReconnect": true
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务
    /// </remarks>
    [HttpPut]
    [ProducesResponseType(typeof(SystemConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SystemConfigResponse> UpdateSystemConfig([FromBody] SystemConfigRequest request)
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

            var config = MapToConfiguration(request);
            
            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            // 验证异常格口是否存在于路由配置中
            var exceptionRoute = _routeRepository.GetByChuteId(config.ExceptionChuteId);
            if (exceptionRoute == null)
            {
                _logger.LogWarning("异常格口 {ExceptionChuteId} 不存在于路由配置中", 
                    LoggingHelper.SanitizeForLogging(config.ExceptionChuteId));
                return BadRequest(new { message = $"异常格口 {config.ExceptionChuteId} 不存在于路由配置中，请先创建对应的路由配置" });
            }

            if (!exceptionRoute.IsEnabled)
            {
                _logger.LogWarning("异常格口 {ExceptionChuteId} 的路由配置未启用", 
                    LoggingHelper.SanitizeForLogging(config.ExceptionChuteId));
                return BadRequest(new { message = $"异常格口 {config.ExceptionChuteId} 的路由配置未启用" });
            }

            _repository.Update(config);

            _logger.LogInformation(
                "系统配置已更新: ExceptionChuteId={ExceptionChuteId}, Version={Version}",
                LoggingHelper.SanitizeForLogging(config.ExceptionChuteId),
                config.Version);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "系统配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系统配置失败");
            return StatusCode(500, new { message = "更新系统配置失败" });
        }
    }

    /// <summary>
    /// 重置系统配置为默认值
    /// </summary>
    /// <returns>重置后的系统配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 将系统配置重置为默认值
    /// </remarks>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(SystemConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SystemConfigResponse> ResetSystemConfig()
    {
        try
        {
            var defaultConfig = SystemConfiguration.GetDefault();
            _repository.Update(defaultConfig);

            _logger.LogInformation("系统配置已重置为默认值");

            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置系统配置失败");
            return StatusCode(500, new { message = "重置系统配置失败" });
        }
    }

    /// <summary>
    /// 将请求模型映射到配置模型
    /// </summary>
    private SystemConfiguration MapToConfiguration(SystemConfigRequest request)
    {
        // 解析异常格口ID
        if (!ChuteIdHelper.TryParseChuteId(request.ExceptionChuteId, out var exceptionChuteId))
        {
            // 如果解析失败，使用默认值999
            exceptionChuteId = 999;
        }

        return new SystemConfiguration
        {
            ExceptionChuteId = exceptionChuteId,
            MqttDefaultPort = request.MqttDefaultPort,
            TcpDefaultPort = request.TcpDefaultPort,
            ChuteAssignmentTimeoutMs = request.ChuteAssignmentTimeoutMs,
            RequestTimeoutMs = request.RequestTimeoutMs,
            RetryCount = request.RetryCount,
            RetryDelayMs = request.RetryDelayMs,
            EnableAutoReconnect = request.EnableAutoReconnect
        };
    }

    /// <summary>
    /// 将配置模型映射到响应模型
    /// </summary>
    private SystemConfigResponse MapToResponse(SystemConfiguration config)
    {
        return new SystemConfigResponse
        {
            Id = config.Id,
            ExceptionChuteId = ChuteIdHelper.FormatChuteId(config.ExceptionChuteId),
            MqttDefaultPort = config.MqttDefaultPort,
            TcpDefaultPort = config.TcpDefaultPort,
            ChuteAssignmentTimeoutMs = config.ChuteAssignmentTimeoutMs,
            RequestTimeoutMs = config.RequestTimeoutMs,
            RetryCount = config.RetryCount,
            RetryDelayMs = config.RetryDelayMs,
            EnableAutoReconnect = config.EnableAutoReconnect,
            Version = config.Version,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}

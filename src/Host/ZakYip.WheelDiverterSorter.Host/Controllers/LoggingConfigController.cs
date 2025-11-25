using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 日志配置管理API控制器
/// </summary>
/// <remarks>
/// 提供日志开关配置的查询和更新功能，用于控制各类日志的输出。
/// 
/// **日志文件分类**：
/// 每个日志分类对应独立的日志文件，便于查看和分析：
/// 
/// | 配置项 | 日志文件 | 说明 |
/// |--------|----------|------|
/// | - | error-{date}.log | 异常日志（始终输出，不受开关控制） |
/// | EnableParcelLifecycleLog | parcel-lifecycle-{date}.log | 包裹生命周期日志 |
/// | EnableParcelTraceLog | parcel-trace-{date}.log | 包裹追踪日志 |
/// | EnablePathExecutionLog | path-execution-{date}.log | 路径执行日志 |
/// | EnableCommunicationLog | communication-{date}.log | 通信日志 |
/// | EnableDriverLog | driver-{date}.log | 硬件驱动日志 |
/// | EnablePerformanceLog | performance-{date}.log | 性能监控日志 |
/// | EnableAlarmLog | alarm-{date}.log | 告警日志 |
/// | EnableDebugLog | debug-{date}.log | 调试日志 |
/// | - | all-{date}.log | 汇总日志（所有日志的备份） |
/// 
/// **注意**：异常日志始终输出到 error-{date}.log，不受任何开关控制。
/// </remarks>
[ApiController]
[Route("api/config/logging")]
[Produces("application/json")]
public class LoggingConfigController : ApiControllerBase
{
    private readonly ILoggingConfigService _configService;
    private readonly ILogger<LoggingConfigController> _logger;

    public LoggingConfigController(
        ILoggingConfigService configService,
        ILogger<LoggingConfigController> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取日志配置
    /// </summary>
    /// <returns>日志配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取当前日志配置",
        Description = "返回系统当前的日志开关配置，包括各类日志的启用状态",
        OperationId = "GetLoggingConfig",
        Tags = new[] { "日志配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<LoggingConfigResponse>))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<LoggingConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<LoggingConfigResponse>> GetLoggingConfig()
    {
        try
        {
            var config = _configService.GetLoggingConfig();
            var response = MapToResponse(config);
            return Success(response, "获取日志配置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志配置失败");
            return ServerError<LoggingConfigResponse>("获取日志配置失败");
        }
    }

    /// <summary>
    /// 获取默认日志配置模板
    /// </summary>
    /// <returns>默认日志配置模板</returns>
    /// <response code="200">成功返回模板</response>
    /// <remarks>
    /// 返回系统默认配置，可用作配置文件模板
    /// </remarks>
    [HttpGet("template")]
    [SwaggerOperation(
        Summary = "获取默认日志配置模板",
        Description = "返回系统默认日志配置作为配置模板，可用于初始化或参考",
        OperationId = "GetLoggingConfigTemplate",
        Tags = new[] { "日志配置" }
    )]
    [SwaggerResponse(200, "成功返回模板", typeof(ApiResponse<LoggingConfigRequest>))]
    [ProducesResponseType(typeof(ApiResponse<LoggingConfigRequest>), 200)]
    public ActionResult<ApiResponse<LoggingConfigRequest>> GetTemplate()
    {
        var defaultConfig = _configService.GetDefaultTemplate();
        var request = new LoggingConfigRequest
        {
            EnableParcelLifecycleLog = defaultConfig.EnableParcelLifecycleLog,
            EnableParcelTraceLog = defaultConfig.EnableParcelTraceLog,
            EnablePathExecutionLog = defaultConfig.EnablePathExecutionLog,
            EnableCommunicationLog = defaultConfig.EnableCommunicationLog,
            EnableDriverLog = defaultConfig.EnableDriverLog,
            EnablePerformanceLog = defaultConfig.EnablePerformanceLog,
            EnableAlarmLog = defaultConfig.EnableAlarmLog,
            EnableDebugLog = defaultConfig.EnableDebugLog
        };
        return Success(request, "获取默认配置模板成功");
    }

    /// <summary>
    /// 更新日志配置（支持热更新）
    /// </summary>
    /// <param name="request">日志配置请求</param>
    /// <returns>更新后的日志配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新日志配置",
        Description = "更新日志开关配置参数，配置会立即生效无需重启",
        OperationId = "UpdateLoggingConfig",
        Tags = new[] { "日志配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<LoggingConfigResponse>))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<LoggingConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<LoggingConfigResponse>>> UpdateLoggingConfig([FromBody] LoggingConfigRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<LoggingConfigResponse>.BadRequest(
                    "请求参数无效",
                    default));
            }

            var result = await _configService.UpdateLoggingConfigAsync(request);

            if (!result.Success)
            {
                return ValidationError<LoggingConfigResponse>(result.ErrorMessage ?? "配置验证失败");
            }

            var response = MapToResponse(result.UpdatedConfig!);
            return Success(response, "日志配置更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新日志配置失败");
            return ServerError<LoggingConfigResponse>("更新日志配置失败");
        }
    }

    /// <summary>
    /// 重置日志配置为默认值
    /// </summary>
    /// <returns>重置后的日志配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("reset")]
    [SwaggerOperation(
        Summary = "重置日志配置为默认值",
        Description = "将所有日志开关配置参数重置为默认值，配置立即生效",
        OperationId = "ResetLoggingConfig",
        Tags = new[] { "日志配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(ApiResponse<LoggingConfigResponse>))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<LoggingConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<LoggingConfigResponse>>> ResetLoggingConfig()
    {
        try
        {
            var config = await _configService.ResetLoggingConfigAsync();
            var response = MapToResponse(config);
            return Success(response, "日志配置已重置为默认值");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置日志配置失败");
            return ServerError<LoggingConfigResponse>("重置日志配置失败");
        }
    }

    private static LoggingConfigResponse MapToResponse(Core.LineModel.Configuration.LoggingConfiguration config)
    {
        return new LoggingConfigResponse
        {
            Id = config.Id,
            EnableParcelLifecycleLog = config.EnableParcelLifecycleLog,
            EnableParcelTraceLog = config.EnableParcelTraceLog,
            EnablePathExecutionLog = config.EnablePathExecutionLog,
            EnableCommunicationLog = config.EnableCommunicationLog,
            EnableDriverLog = config.EnableDriverLog,
            EnablePerformanceLog = config.EnablePerformanceLog,
            EnableAlarmLog = config.EnableAlarmLog,
            EnableDebugLog = config.EnableDebugLog,
            Version = config.Version,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}

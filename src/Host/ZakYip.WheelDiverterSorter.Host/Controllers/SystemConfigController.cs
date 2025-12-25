using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Simulation;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Application.Services.Debug;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

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
public class SystemConfigController : ApiControllerBase
{
    private readonly ISystemConfigService _configService;
    private readonly ILogger<SystemConfigController> _logger;

    public SystemConfigController(
        ISystemConfigService configService,
        ILogger<SystemConfigController> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <returns>系统配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取当前系统配置",
        Description = "返回系统当前的全局配置参数，包括异常处理、通信参数、分拣模式等",
        OperationId = "GetSystemConfig",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(SystemConfigResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SystemConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SystemConfigResponse> GetSystemConfig()
    {
        try
        {
            var config = _configService.GetSystemConfig();
            var response = MapToResponse(config);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统配置失败: {ErrorMessage}", ex.Message);
            return StatusCode(500, new { message = $"获取系统配置失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 更新系统配置（支持热更新）
    /// </summary>
    /// <param name="request">系统配置请求</param>
    /// <returns>更新后的系统配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新系统配置",
        Description = "更新系统全局配置参数，配置会立即生效无需重启",
        OperationId = "UpdateSystemConfig",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<SystemConfigResponse>))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<SystemConfigResponse>>> UpdateSystemConfig([FromBody] SystemConfigRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                
                return BadRequest(ApiResponse<SystemConfigResponse>.BadRequest(
                    "请求参数无效", 
                    default));
            }

            var command = new UpdateSystemConfigCommand
            {
                ExceptionChuteId = request.ExceptionChuteId,
                DriverStartupDelaySeconds = request.DriverStartupDelaySeconds,
                SortingMode = request.SortingMode,
                FixedChuteId = request.FixedChuteId,
                AvailableChuteIds = request.AvailableChuteIds
            };

            var result = await _configService.UpdateSystemConfigAsync(command);

            if (!result.Success)
            {
                return ValidationError<SystemConfigResponse>(result.ErrorMessage ?? "配置验证失败");
            }

            var response = MapToResponse(result.UpdatedConfig!);
            return Success(response, "系统配置更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系统配置失败");
            return ServerError<SystemConfigResponse>("更新系统配置失败");
        }
    }

    private static SystemConfigResponse MapToResponse(ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models.SystemConfiguration config)
    {
#pragma warning disable CS0618 // 向后兼容
        return new SystemConfigResponse
        {
            ExceptionChuteId = config.ExceptionChuteId,
            DriverStartupDelaySeconds = config.DriverStartupDelaySeconds,
            SortingMode = config.SortingMode,
            FixedChuteId = config.FixedChuteId,
            AvailableChuteIds = config.AvailableChuteIds ?? new List<long>(),
            Version = config.Version,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
#pragma warning restore CS0618
    }
}

// SortingModeResponse - defined in ConfigurationController.cs to avoid duplication

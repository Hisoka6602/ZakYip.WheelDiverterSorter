using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
using Swashbuckle.AspNetCore.Annotations;

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
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<SystemConfigResponse>))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SystemConfigResponse>> GetSystemConfig()
    {
        try
        {
            var config = _configService.GetSystemConfig();
            var response = MapToResponse(config);
            return Success(response, "获取系统配置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统配置失败");
            return ServerError<SystemConfigResponse>("获取系统配置失败");
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
    [SwaggerOperation(
        Summary = "获取默认系统配置模板",
        Description = "返回系统默认配置作为配置模板，可用于初始化或参考",
        OperationId = "GetSystemConfigTemplate",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "成功返回模板", typeof(ApiResponse<SystemConfigRequest>))]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigRequest>), 200)]
    public ActionResult<ApiResponse<SystemConfigRequest>> GetTemplate()
    {
        var defaultConfig = _configService.GetDefaultTemplate();
#pragma warning disable CS0618 // 向后兼容
        var request = new SystemConfigRequest
        {
            ExceptionChuteId = defaultConfig.ExceptionChuteId,
            SortingMode = defaultConfig.SortingMode,
            FixedChuteId = defaultConfig.FixedChuteId,
            AvailableChuteIds = defaultConfig.AvailableChuteIds
        };
#pragma warning restore CS0618
        return Success(request, "获取默认配置模板成功");
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

            var result = await _configService.UpdateSystemConfigAsync(request);

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

    /// <summary>
    /// 重置系统配置为默认值
    /// </summary>
    /// <returns>重置后的系统配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("reset")]
    [SwaggerOperation(
        Summary = "重置系统配置为默认值",
        Description = "将所有系统配置参数重置为默认值，配置立即生效",
        OperationId = "ResetSystemConfig",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(ApiResponse<SystemConfigResponse>))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<SystemConfigResponse>>> ResetSystemConfig()
    {
        try
        {
            var config = await _configService.ResetSystemConfigAsync();
            var response = MapToResponse(config);
            return Success(response, "系统配置已重置为默认值");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置系统配置失败");
            return ServerError<SystemConfigResponse>("重置系统配置失败");
        }
    }

    /// <summary>
    /// 获取当前分拣模式配置
    /// </summary>
    /// <returns>当前分拣模式配置信息</returns>
    /// <response code="200">成功返回分拣模式配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("sorting-mode")]
    [SwaggerOperation(
        Summary = "获取当前分拣模式",
        Description = "返回系统当前的分拣模式配置，包括模式类型和相关参数",
        OperationId = "GetSortingMode",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "成功返回分拣模式配置", typeof(ApiResponse<SortingModeResponse>))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<SortingModeResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SortingModeResponse>> GetSortingMode()
    {
        try
        {
            var modeInfo = _configService.GetSortingMode();
            var response = new SortingModeResponse
            {
                SortingMode = modeInfo.SortingMode,
                FixedChuteId = modeInfo.FixedChuteId.HasValue ? (int?)modeInfo.FixedChuteId.Value : null,
                AvailableChuteIds = modeInfo.AvailableChuteIds.Select(id => (int)id).ToList()
            };
            return Success(response, "获取分拣模式配置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分拣模式配置失败");
            return ServerError<SortingModeResponse>("获取分拣模式配置失败");
        }
    }

    /// <summary>
    /// 更新分拣模式配置
    /// </summary>
    /// <param name="request">分拣模式配置请求</param>
    /// <returns>更新后的分拣模式配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("sorting-mode")]
    [SwaggerOperation(
        Summary = "更新分拣模式配置",
        Description = "更新系统的分拣模式配置，配置会立即生效无需重启",
        OperationId = "UpdateSortingMode",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<SortingModeResponse>))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<SortingModeResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<SortingModeResponse>>> UpdateSortingMode([FromBody] ZakYip.WheelDiverterSorter.Host.Models.Config.SortingModeRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                
                return BadRequest(ApiResponse<SortingModeResponse>.BadRequest(
                    "请求参数无效", 
                    default));
            }

            var result = await _configService.UpdateSortingModeAsync(request);

            if (!result.Success)
            {
                return ValidationError<SortingModeResponse>(result.ErrorMessage ?? "配置验证失败");
            }

            var response = new SortingModeResponse
            {
                SortingMode = result.UpdatedMode!.SortingMode,
                FixedChuteId = result.UpdatedMode.FixedChuteId.HasValue ? (int?)result.UpdatedMode.FixedChuteId.Value : null,
                AvailableChuteIds = result.UpdatedMode.AvailableChuteIds.Select(id => (int)id).ToList()
            };

            return Success(response, "分拣模式配置更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新分拣模式配置失败");
            return ServerError<SortingModeResponse>("更新分拣模式配置失败");
        }
    }

    private static SystemConfigResponse MapToResponse(ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models.SystemConfiguration config)
    {
#pragma warning disable CS0618 // 向后兼容
        return new SystemConfigResponse
        {
            Id = config.Id,
            ExceptionChuteId = config.ExceptionChuteId,
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

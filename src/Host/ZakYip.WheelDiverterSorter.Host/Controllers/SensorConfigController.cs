// 本文件使用向后兼容API，抑制废弃警告
#pragma warning disable CS0618 // Type or member is obsolete

using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// [已废弃] 感应IO配置管理API控制器
/// [Obsolete] Sensor IO configuration management API controller
/// </summary>
/// <remarks>
/// **此API已废弃，请使用 /api/config/io-driver/sensors 代替。**
/// 
/// 感应IO配置已并入IO驱动器配置中，以提供统一的IO配置管理体验。
/// 
/// **迁移指南**：
/// - GET /api/config/sensor → GET /api/config/io-driver/sensors
/// - PUT /api/config/sensor → PUT /api/config/io-driver/sensors
/// 
/// 此API仍可使用，但将在未来版本中移除。
/// </remarks>
[ApiController]
[Route("api/config/sensor")]
[Produces("application/json")]
[Obsolete("此API已废弃，请使用 /api/config/io-driver/sensors 代替 - This API is obsolete, please use /api/config/io-driver/sensors instead")]
public class SensorConfigController : ControllerBase
{
    private readonly ISensorConfigurationRepository _repository;
    private readonly ILogger<SensorConfigController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="repository">感应IO配置仓储</param>
    /// <param name="logger">日志记录器</param>
    public SensorConfigController(
        ISensorConfigurationRepository repository,
        ILogger<SensorConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// [已废弃] 获取感应IO配置
    /// [Obsolete] Get sensor IO configuration
    /// </summary>
    /// <returns>感应IO配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **此API已废弃，请使用 GET /api/config/io-driver/sensors 代替。**
    /// 
    /// 返回当前系统的感应IO配置，包括：
    /// - 感应IO列表及其业务类型
    /// - IO点位绑定关系
    /// - 配置版本号
    /// </remarks>
    [HttpGet]
    [SwaggerOperation(
        Summary = "[已废弃] 获取感应IO配置",
        Description = "此API已废弃，请使用 GET /api/config/io-driver/sensors 代替。返回当前系统的感应IO配置。",
        OperationId = "GetSensorConfig",
        Tags = new[] { "感应IO配置（已废弃）" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> GetSensorConfig()
    {
        try
        {
            _logger.LogWarning("使用已废弃的API: GET /api/config/sensor，请迁移到 GET /api/config/io-driver/sensors - Deprecated API: GET /api/config/sensor, please migrate to GET /api/config/io-driver/sensors");
            
            var config = _repository.Get();
            return Ok(ApiResponse<SensorConfiguration>.Ok(config, "成功（此API已废弃，请使用 /api/config/io-driver/sensors）- Success (this API is deprecated, please use /api/config/io-driver/sensors)"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取感应IO配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取感应IO配置失败 - Failed to get sensor configuration"));
        }
    }

    /// <summary>
    /// [已废弃] 更新感应IO配置（支持热更新）
    /// [Obsolete] Update sensor IO configuration (hot reload supported)
    /// </summary>
    /// <param name="request">感应IO配置请求</param>
    /// <returns>更新后的感应IO配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **此API已废弃，请使用 PUT /api/config/io-driver/sensors 代替。**
    /// 
    /// 更新系统感应IO配置，配置立即生效无需重启。
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "[已废弃] 更新感应IO配置",
        Description = "此API已废弃，请使用 PUT /api/config/io-driver/sensors 代替。更新系统感应IO配置。",
        OperationId = "UpdateSensorConfig",
        Tags = new[] { "感应IO配置（已废弃）" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> UpdateSensorConfig(
        [FromBody, SwaggerRequestBody("感应IO配置请求", Required = true)] SensorConfiguration request)
    {
        try
        {
            _logger.LogWarning("使用已废弃的API: PUT /api/config/sensor，请迁移到 PUT /api/config/io-driver/sensors - Deprecated API: PUT /api/config/sensor, please migrate to PUT /api/config/io-driver/sensors");
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.BadRequest($"请求参数无效: {string.Join(", ", errors)}"));
            }

            // 验证配置
            var (isValid, errorMessage) = request.Validate();
            if (!isValid)
            {
                return BadRequest(ApiResponse<object>.BadRequest(errorMessage ?? "配置验证失败"));
            }

            _repository.Update(request);

            _logger.LogInformation(
                "感应IO配置已更新: SensorCount={SensorCount}, Version={Version}",
                request.Sensors?.Count ?? 0,
                request.Version);

            var updatedConfig = _repository.Get();
            return Ok(ApiResponse<SensorConfiguration>.Ok(updatedConfig, "更新成功（此API已废弃，请使用 /api/config/io-driver/sensors）"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "感应IO配置验证失败");
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新感应IO配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("更新感应IO配置失败 - Failed to update sensor configuration"));
        }
    }
}

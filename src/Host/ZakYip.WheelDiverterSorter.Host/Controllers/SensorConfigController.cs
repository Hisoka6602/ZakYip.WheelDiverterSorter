using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 传感器配置管理API控制器
/// </summary>
/// <remarks>
/// 提供传感器配置的查询和更新功能，支持热更新。
/// 
/// **传感器配置说明**：
/// - 配置传感器类型（硬件/仿真）
/// - 配置传感器厂商（雷赛、研华、倍福等）
/// - 配置传感器IO绑定和防抖参数
/// - 配置传感器的轮询行为
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。正在运行的传感器读取任务会在下一个轮询周期使用新配置。
/// 
/// **注意事项**：
/// - 切换硬件/仿真模式可能需要重启服务才能完全生效
/// - 修改 IO 绑定配置时，请确保 IO 地址未被其他设备占用
/// - 防抖时间设置过小可能导致误触发，过大可能导致响应迟钝
/// </remarks>
[ApiController]
[Route("api/config/sensor")]
[Produces("application/json")]
public class SensorConfigController : ControllerBase
{
    private readonly ISensorConfigurationRepository _repository;
    private readonly ILogger<SensorConfigController> _logger;

    public SensorConfigController(
        ISensorConfigurationRepository repository,
        ILogger<SensorConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取传感器配置
    /// </summary>
    /// <returns>传感器配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前系统的传感器配置，包括：
    /// - 使用硬件传感器还是仿真传感器
    /// - 传感器厂商类型（Leadshine、Advantech、Beckhoff等）
    /// - 各传感器的 IO 绑定配置
    /// - 传感器轮询间隔和防抖参数
    /// 
    /// 示例响应：
    /// ```json
    /// {
    ///   "useHardwareSensor": true,
    ///   "vendorType": 1,
    ///   "pollingIntervalMs": 50,
    ///   "debounceMs": 10,
    ///   "version": 1
    /// }
    /// ```
    /// </remarks>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取传感器配置",
        Description = "返回当前系统的传感器配置，包括厂商类型、IO绑定、轮询参数等",
        OperationId = "GetSensorConfig",
        Tags = new[] { "传感器配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(SensorConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SensorConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SensorConfiguration> GetSensorConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取传感器配置失败");
            return StatusCode(500, new { message = "获取传感器配置失败" });
        }
    }

    /// <summary>
    /// 更新传感器配置（支持热更新）
    /// </summary>
    /// <param name="request">传感器配置请求，包含厂商类型、IO绑定、轮询参数等</param>
    /// <returns>更新后的传感器配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求：
    /// 
    ///     PUT /api/config/sensor
    ///     {
    ///         "useHardwareSensor": true,
    ///         "vendorType": 1,
    ///         "pollingIntervalMs": 50,
    ///         "debounceMs": 10
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 
    /// **重要提示**：
    /// - 切换硬件/仿真模式（useHardwareSensor）可能需要重启服务才能完全生效
    /// - 修改轮询间隔会影响传感器响应速度和CPU占用
    /// - 防抖时间设置需要根据实际传感器特性调整
    /// 
    /// **支持的厂商类型（vendorType）**：
    /// - 0: Simulated（仿真传感器）
    /// - 1: Leadshine（雷赛传感器）
    /// - 2: Advantech（研华传感器）
    /// - 3: Beckhoff（倍福传感器）
    /// 
    /// **参数验证规则**：
    /// - pollingIntervalMs: 10-1000 毫秒
    /// - debounceMs: 5-500 毫秒
    /// - debounceMs 必须小于 pollingIntervalMs
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新传感器配置",
        Description = "更新系统传感器配置，配置立即生效无需重启。支持配置硬件/仿真传感器切换、厂商选择和传感器参数",
        OperationId = "UpdateSensorConfig",
        Tags = new[] { "传感器配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(SensorConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SensorConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SensorConfiguration> UpdateSensorConfig([FromBody] SensorConfiguration request)
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

            // 验证配置
            var (isValid, errorMessage) = request.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            _repository.Update(request);

            _logger.LogInformation(
                "传感器配置已更新: VendorType={VendorType}, UseHardware={UseHardware}, Version={Version}",
                request.VendorType,
                request.UseHardwareSensor,
                request.Version);

            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "传感器配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新传感器配置失败");
            return StatusCode(500, new { message = "更新传感器配置失败" });
        }
    }

    /// <summary>
    /// 重置传感器配置为默认值
    /// </summary>
    /// <returns>重置后的传感器配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 将传感器配置重置为系统默认值：
    /// - 使用仿真传感器（useHardwareSensor: false）
    /// - 默认厂商类型：Simulated
    /// - 轮询间隔：100ms
    /// - 防抖时间：50ms
    /// 
    /// 重置操作会立即生效，适用于：
    /// - 配置出错需要恢复默认
    /// - 切换回仿真模式进行测试
    /// - 清除不正确的配置参数
    /// </remarks>
    [HttpPost("reset")]
    [SwaggerOperation(
        Summary = "重置传感器配置",
        Description = "将传感器配置重置为系统默认值（仿真模式）",
        OperationId = "ResetSensorConfig",
        Tags = new[] { "传感器配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(SensorConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SensorConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SensorConfiguration> ResetSensorConfig()
    {
        try
        {
            var defaultConfig = SensorConfiguration.GetDefault();
            _repository.Update(defaultConfig);

            _logger.LogInformation("传感器配置已重置为默认值");

            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置传感器配置失败");
            return StatusCode(500, new { message = "重置传感器配置失败" });
        }
    }
}

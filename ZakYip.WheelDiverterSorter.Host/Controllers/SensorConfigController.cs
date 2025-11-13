using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 传感器配置管理API控制器
/// </summary>
/// <remarks>
/// 提供传感器配置的查询和更新功能，支持热更新
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
    [HttpGet]
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
    /// <param name="request">传感器配置请求</param>
    /// <returns>更新后的传感器配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 配置更新后立即生效，无需重启服务
    /// </remarks>
    [HttpPut]
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
    [HttpPost("reset")]
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

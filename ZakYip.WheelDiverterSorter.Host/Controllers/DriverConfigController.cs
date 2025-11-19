using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Host.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 驱动器配置管理API控制器
/// </summary>
/// <remarks>
/// 提供驱动器配置的查询和更新功能，支持热更新
/// </remarks>
[ApiController]
[Route("api/config/driver")]
[Produces("application/json")]
public class DriverConfigController : ControllerBase
{
    private readonly IDriverConfigurationRepository _repository;
    private readonly ILogger<DriverConfigController> _logger;

    public DriverConfigController(
        IDriverConfigurationRepository repository,
        ILogger<DriverConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取驱动器配置
    /// </summary>
    /// <returns>驱动器配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(DriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<DriverConfiguration> GetDriverConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取驱动器配置失败");
            return StatusCode(500, new { message = "获取驱动器配置失败" });
        }
    }

    /// <summary>
    /// 更新驱动器配置（支持热更新）
    /// </summary>
    /// <param name="request">驱动器配置请求</param>
    /// <returns>更新后的驱动器配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/driver
    ///     {
    ///         "useHardwareDriver": false,
    ///         "vendorType": 1,
    ///         "leadshine": {
    ///             "cardNo": 0,
    ///             "diverters": [
    ///                 {
    ///                     "diverterId": "D1",
    ///                     "outputStartBit": 0,
    ///                     "feedbackInputBit": 10
    ///                 }
    ///             ]
    ///         }
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务（注意：运行中的分拣任务不受影响）
    /// </remarks>
    [HttpPut]
    [ProducesResponseType(typeof(DriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<DriverConfiguration> UpdateDriverConfig([FromBody] DriverConfiguration request)
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
                "驱动器配置已更新: VendorType={VendorType}, UseHardware={UseHardware}, Version={Version}",
                request.VendorType,
                request.UseHardwareDriver,
                request.Version);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "驱动器配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新驱动器配置失败");
            return StatusCode(500, new { message = "更新驱动器配置失败" });
        }
    }

    /// <summary>
    /// 重置驱动器配置为默认值
    /// </summary>
    /// <returns>重置后的驱动器配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(DriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<DriverConfiguration> ResetDriverConfig()
    {
        try
        {
            var defaultConfig = DriverConfiguration.GetDefault();
            _repository.Update(defaultConfig);

            _logger.LogInformation("驱动器配置已重置为默认值");

            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置驱动器配置失败");
            return StatusCode(500, new { message = "重置驱动器配置失败" });
        }
    }
}

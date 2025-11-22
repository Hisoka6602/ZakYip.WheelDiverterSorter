using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Core.LineModel.Utilities;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 驱动器配置管理API控制器
/// </summary>
/// <remarks>
/// 提供驱动器配置的查询和更新功能，支持热更新。
/// 可配置是否使用硬件驱动器、选择厂商类型、配置各厂商的特定参数等。
/// 
/// **驱动器配置说明**：
/// - 配置驱动器类型（硬件/仿真）
/// - 配置驱动器厂商（雷赛、研华、倍福等）
/// - 配置摆轮驱动器的 IO 绑定
/// - 配置驱动器的动作参数（速度、加速度等）
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。正在运行的分拣任务不受影响，只对新的分拣任务生效。
/// 
/// **与线体/模块的关系**：
/// - 驱动器配置定义了物理设备的访问方式和参数
/// - 线体配置引用驱动器配置中定义的摆轮ID
/// - 修改驱动器配置不会影响线体拓扑结构
/// 
/// **注意事项**：
/// - 切换硬件/仿真模式可能需要重启服务才能完全生效
/// - 修改 IO 绑定配置时，请确保 IO 地址未被其他设备占用
/// - 不同厂商的驱动器有不同的配置参数要求
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
    /// <returns>当前驱动器配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取驱动器配置",
        Description = "返回当前系统的驱动器配置，包括是否使用硬件驱动、厂商类型和厂商特定参数",
        OperationId = "GetDriverConfig",
        Tags = new[] { "驱动器配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(DriverConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
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
    /// <param name="request">驱动器配置请求，包含使用硬件/模拟驱动的选择、厂商类型和厂商特定参数</param>
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
    /// 配置更新后立即生效，无需重启服务。
    /// 注意：正在运行中的分拣任务不受影响，只对新的分拣任务生效。
    /// 
    /// 支持的厂商类型（vendorType）：
    /// - 0: Simulated（模拟驱动器）
    /// - 1: Leadshine（雷赛驱动器）
    /// - 2: Advantech（研华驱动器）
    /// - 3: Beckhoff（倍福驱动器）
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新驱动器配置",
        Description = "更新系统驱动器配置，配置立即生效无需重启。支持配置硬件/模拟驱动器切换、厂商选择和厂商特定参数",
        OperationId = "UpdateDriverConfig",
        Tags = new[] { "驱动器配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(DriverConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
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
    /// <remarks>
    /// 将驱动器配置重置为系统默认值：
    /// - 使用仿真驱动器（useHardwareDriver: false）
    /// - 默认厂商类型：Simulated
    /// - 清除所有厂商特定配置参数
    /// 
    /// 重置操作会立即生效，适用于：
    /// - 配置出错需要恢复默认
    /// - 切换回仿真模式进行测试
    /// - 清除不正确的配置参数
    /// 
    /// **注意**：重置后正在运行的分拣任务不受影响，只对新的分拣任务生效。
    /// </remarks>
    [HttpPost("reset")]
    [SwaggerOperation(
        Summary = "重置驱动器配置",
        Description = "将驱动器配置重置为系统默认值（仿真模式）",
        OperationId = "ResetDriverConfig",
        Tags = new[] { "驱动器配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(DriverConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
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

using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Core.LineModel.Utilities;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// IO驱动器配置管理API控制器
/// </summary>
/// <remarks>
/// 提供IO驱动器配置的查询和更新功能，支持热更新。
/// 
/// **重要说明**：
/// 此API用于配置【IO驱动器】，而非【摆轮驱动器】。两者是不同的概念：
/// 
/// - **IO驱动器**（本API）: 控制IO端点（输入/输出位），用于传感器信号读取和继电器控制
///   - 雷赛（Leadshine）: 雷赛运动控制卡的IO接口
///   - 西门子（Siemens）: S7系列PLC的IO模块
///   - 三菱（Mitsubishi）: 三菱PLC的IO模块
///   - 欧姆龙（Omron）: 欧姆龙PLC的IO模块
/// 
/// - **摆轮驱动器**（/api/config/wheeldiverter/* API）: 控制摆轮转向（左转、右转、回中）
///   - 数递鸟（ShuDiNiao）: 通过TCP协议直接控制
///   - 莫迪（Modi）: 通过TCP协议直接控制
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。正在运行的分拣任务不受影响，只对新的分拣任务生效。
/// 
/// **与摆轮硬件绑定的关系**：
/// - 本API配置IO驱动器的硬件参数（卡号、IO映射等）
/// - /api/config/wheel-bindings API配置逻辑摆轮节点与驱动器的映射关系
/// </remarks>
[ApiController]
[Route("api/config/io-driver")]
[Produces("application/json")]
public class IoDriverConfigController : ControllerBase
{
    private readonly IDriverConfigurationRepository _repository;
    private readonly ILogger<IoDriverConfigController> _logger;

    public IoDriverConfigController(
        IDriverConfigurationRepository repository,
        ILogger<IoDriverConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取IO驱动器配置
    /// </summary>
    /// <returns>当前IO驱动器配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取IO驱动器配置",
        Description = "返回当前系统的IO驱动器配置，包括是否使用硬件驱动、厂商类型和厂商特定参数。注意：这是IO驱动器（用于传感器和继电器），而非摆轮驱动器。",
        OperationId = "GetIoDriverConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(IoDriverConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IoDriverConfiguration> GetIoDriverConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(MapToIoDriverConfiguration(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取IO驱动器配置失败");
            return StatusCode(500, new { message = "获取IO驱动器配置失败" });
        }
    }

    /// <summary>
    /// 更新IO驱动器配置（支持热更新）
    /// </summary>
    /// <param name="request">IO驱动器配置请求，包含使用硬件/模拟驱动的选择、厂商类型和厂商特定参数</param>
    /// <returns>更新后的IO驱动器配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **重要说明**：此API用于配置【IO驱动器】，而非【摆轮驱动器】。
    /// - IO驱动器: 用于控制传感器信号读取和继电器输出
    /// - 摆轮驱动器: 请使用 /api/config/wheeldiverter/* API
    /// 
    /// 示例请求:
    /// 
    ///     PUT /api/config/io-driver
    ///     {
    ///         "useHardwareDriver": false,
    ///         "vendorType": "Leadshine",
    ///         "leadshine": {
    ///             "cardNo": 0,
    ///             "ioMappings": [
    ///                 {
    ///                     "pointId": "IN_START",
    ///                     "bitIndex": 0
    ///                 }
    ///             ]
    ///         }
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 注意：正在运行中的分拣任务不受影响，只对新的分拣任务生效。
    /// 
    /// 支持的IO驱动厂商类型（vendorType）：
    /// - Mock: 模拟驱动器（用于测试）
    /// - Leadshine: 雷赛运动控制卡
    /// - Siemens: 西门子PLC
    /// - Mitsubishi: 三菱PLC
    /// - Omron: 欧姆龙PLC
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新IO驱动器配置",
        Description = "更新系统IO驱动器配置，配置立即生效无需重启。支持配置硬件/模拟驱动器切换、厂商选择和厂商特定参数。注意：这是IO驱动器，摆轮驱动器请使用 /api/config/wheeldiverter/* API。",
        OperationId = "UpdateIoDriverConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(IoDriverConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IoDriverConfiguration> UpdateIoDriverConfig([FromBody] DriverConfiguration request)
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
                "IO驱动器配置已更新: VendorType={VendorType}, UseHardware={UseHardware}, Version={Version}",
                request.VendorType,
                request.UseHardwareDriver,
                request.Version);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(MapToIoDriverConfiguration(updatedConfig));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "IO驱动器配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新IO驱动器配置失败");
            return StatusCode(500, new { message = "更新IO驱动器配置失败" });
        }
    }

    /// <summary>
    /// 重置IO驱动器配置为默认值
    /// </summary>
    /// <returns>重置后的IO驱动器配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 将IO驱动器配置重置为系统默认值：
    /// - 使用仿真驱动器（useHardwareDriver: false）
    /// - 默认厂商类型：Leadshine（雷赛）
    /// - 使用默认的IO映射配置
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
        Summary = "重置IO驱动器配置",
        Description = "将IO驱动器配置重置为系统默认值（仿真模式）",
        OperationId = "ResetIoDriverConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(IoDriverConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IoDriverConfiguration> ResetIoDriverConfig()
    {
        try
        {
            var defaultConfig = DriverConfiguration.GetDefault();
            _repository.Update(defaultConfig);

            _logger.LogInformation("IO驱动器配置已重置为默认值");

            var updatedConfig = _repository.Get();
            return Ok(MapToIoDriverConfiguration(updatedConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置IO驱动器配置失败");
            return StatusCode(500, new { message = "重置IO驱动器配置失败" });
        }
    }
    
    /// <summary>
    /// 将 DriverConfiguration 映射到 IoDriverConfiguration（更语义化的名称）
    /// </summary>
    private static IoDriverConfiguration MapToIoDriverConfiguration(DriverConfiguration config)
    {
        return new IoDriverConfiguration
        {
            Id = config.Id,
            UseHardwareDriver = config.UseHardwareDriver,
            VendorType = config.VendorType,
            Leadshine = config.Leadshine,
            Version = config.Version,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}

/// <summary>
/// IO驱动器配置响应模型（与DriverConfiguration结构相同，但命名更清晰）
/// </summary>
public class IoDriverConfiguration
{
    /// <summary>
    /// 配置唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// </summary>
    public bool UseHardwareDriver { get; set; }

    /// <summary>
    /// IO驱动器厂商类型
    /// </summary>
    public DriverVendorType VendorType { get; set; }

    /// <summary>
    /// 雷赛运动控制卡配置
    /// </summary>
    public LeadshineDriverConfig? Leadshine { get; set; }

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

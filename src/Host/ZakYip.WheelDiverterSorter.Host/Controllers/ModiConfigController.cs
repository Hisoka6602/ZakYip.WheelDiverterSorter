using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 莫迪摆轮配置管理API控制器
/// </summary>
/// <remarks>
/// 提供莫迪摆轮设备的专用配置管理功能，包括：
/// - 查询莫迪摆轮设备配置
/// - 添加/更新/删除莫迪设备
/// - 切换仿真模式
/// 
/// **莫迪摆轮设备说明**：
/// - 通过TCP协议通信
/// - 每个设备有独立的IP、端口和设备编号
/// - 支持多设备配置（不同设备编号）
/// - 支持仿真模式用于测试
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。正在运行的分拣任务不受影响，只对新的分拣任务生效。
/// </remarks>
[ApiController]
[Route("api/config/driver/modi")]
[Produces("application/json")]
public class ModiConfigController : ControllerBase
{
    private readonly IDriverConfigurationRepository _repository;
    private readonly ILogger<ModiConfigController> _logger;

    public ModiConfigController(
        IDriverConfigurationRepository repository,
        ILogger<ModiConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取莫迪摆轮配置
    /// </summary>
    /// <returns>莫迪摆轮配置信息，如果未配置则返回null</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取莫迪摆轮配置",
        Description = "返回当前系统的莫迪摆轮设备配置，包括所有设备列表和仿真模式设置",
        OperationId = "GetModiConfig",
        Tags = new[] { "莫迪摆轮配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ModiDriverConfig))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ModiDriverConfig), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ModiDriverConfig?> GetModiConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(config.Modi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取莫迪摆轮配置失败");
            return StatusCode(500, new { message = "获取莫迪摆轮配置失败" });
        }
    }

    /// <summary>
    /// 更新莫迪摆轮配置
    /// </summary>
    /// <param name="request">莫迪摆轮配置请求</param>
    /// <returns>更新后的完整驱动器配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/driver/modi
    ///     {
    ///         "devices": [
    ///             {
    ///                 "diverterId": "D1",
    ///                 "host": "192.168.1.100",
    ///                 "port": 8000,
    ///                 "deviceId": 1,
    ///                 "isEnabled": true
    ///             },
    ///             {
    ///                 "diverterId": "D2",
    ///                 "host": "192.168.1.100",
    ///                 "port": 8000,
    ///                 "deviceId": 2,
    ///                 "isEnabled": true
    ///             }
    ///         ],
    ///         "useSimulation": false
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 注意：正在运行的分拣任务不受影响，只对新的分拣任务生效。
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新莫迪摆轮配置",
        Description = "更新莫迪摆轮设备配置，支持多设备和仿真模式切换",
        OperationId = "UpdateModiConfig",
        Tags = new[] { "莫迪摆轮配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(DriverConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(DriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<DriverConfiguration> UpdateModiConfig(
        [FromBody] UpdateModiConfigRequest request)
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

            // 获取当前配置
            var config = _repository.Get();

            // 更新莫迪配置
            config.Modi = new ModiDriverConfig
            {
                Devices = request.Devices.Select(d => new ModiDeviceEntry
                {
                    DiverterId = d.DiverterId,
                    Host = d.Host,
                    Port = d.Port,
                    DeviceId = d.DeviceId,
                    IsEnabled = d.IsEnabled
                }).ToList(),
                UseSimulation = request.UseSimulation
            };

            // 如果启用了莫迪配置，则将厂商类型设置为Modi
            if (request.Devices.Any())
            {
                config.VendorType = Core.Enums.Hardware.DriverVendorType.Modi;
            }

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            // 保存配置
            _repository.Update(config);

            _logger.LogInformation(
                "莫迪摆轮配置已更新: 设备数量={DeviceCount}, 仿真模式={UseSimulation}",
                request.Devices.Count,
                request.UseSimulation);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "莫迪摆轮配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新莫迪摆轮配置失败");
            return StatusCode(500, new { message = "更新莫迪摆轮配置失败" });
        }
    }

    /// <summary>
    /// 切换莫迪摆轮仿真模式
    /// </summary>
    /// <param name="useSimulation">是否使用仿真模式</param>
    /// <returns>更新后的完整驱动器配置</returns>
    /// <response code="200">切换成功</response>
    /// <response code="400">请求参数无效或未配置莫迪设备</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     POST /api/config/driver/modi/simulation?useSimulation=true
    /// 
    /// 仿真模式说明：
    /// - true：使用仿真设备，不连接真实硬件
    /// - false：连接真实莫迪摆轮设备
    /// 
    /// 切换后立即生效，无需重启服务。
    /// </remarks>
    [HttpPost("simulation")]
    [SwaggerOperation(
        Summary = "切换仿真模式",
        Description = "切换莫迪摆轮设备的仿真模式，用于测试和实际运行切换",
        OperationId = "ToggleModiSimulation",
        Tags = new[] { "莫迪摆轮配置" }
    )]
    [SwaggerResponse(200, "切换成功", typeof(DriverConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(DriverConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<DriverConfiguration> ToggleSimulation(
        [FromQuery, Required] bool useSimulation)
    {
        try
        {
            // 获取当前配置
            var config = _repository.Get();

            if (config.Modi == null)
            {
                return BadRequest(new { message = "未配置莫迪摆轮设备" });
            }

            // 更新仿真模式
            config.Modi = config.Modi with { UseSimulation = useSimulation };

            // 保存配置
            _repository.Update(config);

            _logger.LogInformation(
                "莫迪摆轮仿真模式已切换: UseSimulation={UseSimulation}",
                useSimulation);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换莫迪摆轮仿真模式失败");
            return StatusCode(500, new { message = "切换莫迪摆轮仿真模式失败" });
        }
    }
}

/// <summary>
/// 更新莫迪摆轮配置请求
/// </summary>
public record class UpdateModiConfigRequest
{
    /// <summary>
    /// 莫迪摆轮设备列表
    /// </summary>
    [Required(ErrorMessage = "设备列表不能为空")]
    public required List<ModiDeviceRequest> Devices { get; init; }

    /// <summary>
    /// 是否启用仿真模式
    /// </summary>
    public bool UseSimulation { get; init; } = false;
}

/// <summary>
/// 莫迪摆轮设备请求
/// </summary>
public record class ModiDeviceRequest
{
    /// <summary>
    /// 摆轮标识符
    /// </summary>
    /// <example>D1</example>
    [Required(ErrorMessage = "摆轮标识符不能为空")]
    [StringLength(50, ErrorMessage = "摆轮标识符长度不能超过50个字符")]
    public required string DiverterId { get; init; }

    /// <summary>
    /// TCP连接主机地址
    /// </summary>
    /// <example>192.168.1.100</example>
    [Required(ErrorMessage = "主机地址不能为空")]
    [StringLength(255, ErrorMessage = "主机地址长度不能超过255个字符")]
    public required string Host { get; init; }

    /// <summary>
    /// TCP连接端口
    /// </summary>
    /// <example>8000</example>
    [Range(1, 65535, ErrorMessage = "端口号必须在1到65535之间")]
    public required int Port { get; init; }

    /// <summary>
    /// 设备编号
    /// </summary>
    /// <remarks>
    /// 莫迪设备的内部编号，用于协议通信
    /// </remarks>
    /// <example>1</example>
    [Range(1, int.MaxValue, ErrorMessage = "设备编号必须大于0")]
    public required int DeviceId { get; init; }

    /// <summary>
    /// 是否启用该设备
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; init; } = true;
}

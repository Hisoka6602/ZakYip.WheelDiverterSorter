using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 数递鸟摆轮配置管理API控制器
/// </summary>
/// <remarks>
/// 提供数递鸟摆轮设备的专用配置管理功能，包括：
/// - 查询数递鸟摆轮设备配置
/// - 添加/更新/删除数递鸟设备
/// - 切换仿真模式
/// 
/// **数递鸟摆轮设备说明**：
/// - 通过TCP协议通信
/// - 每个设备有独立的IP、端口和设备地址
/// - 支持多设备配置（不同设备地址）
/// - 支持仿真模式用于测试
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。正在运行的分拣任务不受影响，只对新的分拣任务生效。
/// </remarks>
[ApiController]
[Route("api/config/wheeldiverter/shudiniao")]
[Produces("application/json")]
public class ShuDiNiaoConfigController : ControllerBase
{
    private readonly IWheelDiverterConfigurationRepository _repository;
    private readonly ILogger<ShuDiNiaoConfigController> _logger;

    public ShuDiNiaoConfigController(
        IWheelDiverterConfigurationRepository repository,
        ILogger<ShuDiNiaoConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取数递鸟摆轮配置
    /// </summary>
    /// <returns>数递鸟摆轮配置信息，如果未配置则返回null</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取数递鸟摆轮配置",
        Description = "返回当前系统的数递鸟摆轮设备配置，包括所有设备列表和仿真模式设置",
        OperationId = "GetShuDiNiaoConfig",
        Tags = new[] { "数递鸟摆轮配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ShuDiNiaoWheelDiverterConfig))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ShuDiNiaoWheelDiverterConfig), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ShuDiNiaoWheelDiverterConfig?> GetShuDiNiaoConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(config.ShuDiNiao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取数递鸟摆轮配置失败");
            return StatusCode(500, new { message = "获取数递鸟摆轮配置失败" });
        }
    }

    /// <summary>
    /// 更新数递鸟摆轮配置
    /// </summary>
    /// <param name="request">数递鸟摆轮配置请求</param>
    /// <returns>更新后的完整摆轮配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/wheeldiverter/shudiniao
    ///     {
    ///         "devices": [
    ///             {
    ///                 "diverterId": "D1",
    ///                 "host": "192.168.0.100",
    ///                 "port": 2000,
    ///                 "deviceAddress": 81,
    ///                 "isEnabled": true
    ///             },
    ///             {
    ///                 "diverterId": "D2",
    ///                 "host": "192.168.0.100",
    ///                 "port": 2000,
    ///                 "deviceAddress": 82,
    ///                 "isEnabled": true
    ///             }
    ///         ],
    ///         "useSimulation": false
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 注意：正在运行的分拣任务不受影响，只对新的分拣任务生效。
    /// 
    /// **设备地址说明**：
    /// - 0x51 (十进制81) 表示 1 号设备
    /// - 0x52 (十进制82) 表示 2 号设备
    /// - 0x53 (十进制83) 表示 3 号设备
    /// - 依次类推
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新数递鸟摆轮配置",
        Description = "更新数递鸟摆轮设备配置，支持多设备和仿真模式切换",
        OperationId = "UpdateShuDiNiaoConfig",
        Tags = new[] { "数递鸟摆轮配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(WheelDiverterConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(WheelDiverterConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<WheelDiverterConfiguration> UpdateShuDiNiaoConfig(
        [FromBody] UpdateShuDiNiaoConfigRequest request)
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

            // 更新数递鸟配置
            config.ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
            {
                Devices = request.Devices.Select(d => new ShuDiNiaoDeviceEntry
                {
                    DiverterId = d.DiverterId,
                    Host = d.Host,
                    Port = d.Port,
                    DeviceAddress = d.DeviceAddress,
                    IsEnabled = d.IsEnabled
                }).ToList(),
                UseSimulation = request.UseSimulation
            };

            // 如果启用了数递鸟配置，则将厂商类型设置为ShuDiNiao
            if (request.Devices.Any())
            {
                config.VendorType = Core.Enums.Hardware.WheelDiverterVendorType.ShuDiNiao;
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
                "数递鸟摆轮配置已更新: 设备数量={DeviceCount}, 仿真模式={UseSimulation}",
                request.Devices.Count,
                request.UseSimulation);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "数递鸟摆轮配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新数递鸟摆轮配置失败");
            return StatusCode(500, new { message = "更新数递鸟摆轮配置失败" });
        }
    }

    /// <summary>
    /// 切换数递鸟摆轮仿真模式
    /// </summary>
    /// <param name="useSimulation">是否使用仿真模式</param>
    /// <returns>更新后的完整摆轮配置</returns>
    /// <response code="200">切换成功</response>
    /// <response code="400">请求参数无效或未配置数递鸟设备</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     POST /api/config/wheeldiverter/shudiniao/simulation?useSimulation=true
    /// 
    /// 仿真模式说明：
    /// - true：使用仿真设备，不连接真实硬件
    /// - false：连接真实数递鸟摆轮设备
    /// 
    /// 切换后立即生效，无需重启服务。
    /// </remarks>
    [HttpPost("simulation")]
    [SwaggerOperation(
        Summary = "切换仿真模式",
        Description = "切换数递鸟摆轮设备的仿真模式，用于测试和实际运行切换",
        OperationId = "ToggleShuDiNiaoSimulation",
        Tags = new[] { "数递鸟摆轮配置" }
    )]
    [SwaggerResponse(200, "切换成功", typeof(WheelDiverterConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(WheelDiverterConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<WheelDiverterConfiguration> ToggleSimulation(
        [FromQuery, Required] bool useSimulation)
    {
        try
        {
            // 获取当前配置
            var config = _repository.Get();

            if (config.ShuDiNiao == null)
            {
                return BadRequest(new { message = "未配置数递鸟摆轮设备" });
            }

            // 更新仿真模式
            config.ShuDiNiao = config.ShuDiNiao with { UseSimulation = useSimulation };

            // 保存配置
            _repository.Update(config);

            _logger.LogInformation(
                "数递鸟摆轮仿真模式已切换: UseSimulation={UseSimulation}",
                useSimulation);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换数递鸟摆轮仿真模式失败");
            return StatusCode(500, new { message = "切换数递鸟摆轮仿真模式失败" });
        }
    }
}

/// <summary>
/// 更新数递鸟摆轮配置请求
/// </summary>
public record class UpdateShuDiNiaoConfigRequest
{
    /// <summary>
    /// 数递鸟摆轮设备列表
    /// </summary>
    [Required(ErrorMessage = "设备列表不能为空")]
    public required List<ShuDiNiaoDeviceRequest> Devices { get; init; }

    /// <summary>
    /// 是否启用仿真模式
    /// </summary>
    public bool UseSimulation { get; init; } = false;
}

/// <summary>
/// 数递鸟摆轮设备请求
/// </summary>
public record class ShuDiNiaoDeviceRequest
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
    /// <example>192.168.0.100</example>
    [Required(ErrorMessage = "主机地址不能为空")]
    [StringLength(255, ErrorMessage = "主机地址长度不能超过255个字符")]
    public required string Host { get; init; }

    /// <summary>
    /// TCP连接端口
    /// </summary>
    /// <example>2000</example>
    [Range(1, 65535, ErrorMessage = "端口号必须在1到65535之间")]
    public required int Port { get; init; }

    /// <summary>
    /// 设备地址（协议中的设备地址字节）
    /// </summary>
    /// <remarks>
    /// 81 (0x51) 表示 1 号设备
    /// 82 (0x52) 表示 2 号设备
    /// 依次类推
    /// </remarks>
    /// <example>81</example>
    [Range(0x51, 0xFF, ErrorMessage = "设备地址必须在0x51到0xFF之间")]
    public required byte DeviceAddress { get; init; }

    /// <summary>
    /// 是否启用该设备
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; init; } = true;
}

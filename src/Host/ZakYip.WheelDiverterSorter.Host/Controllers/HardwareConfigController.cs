using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.LineModel.Utilities;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 硬件配置管理 API 控制器
/// Hardware Configuration Management API Controller
/// </summary>
/// <remarks>
/// 提供统一的硬件配置管理功能，包括：
/// - 雷赛IO驱动器配置（/api/hardware/leadshine）
/// - 数递鸟摆轮配置（/api/hardware/shudiniao）
/// 
/// **概念区分**：
/// - **IO驱动器**（/api/hardware/leadshine）: 基于雷赛运动控制卡，控制IO端点（输入/输出位），用于传感器信号读取和继电器控制
/// - **摆轮驱动器**（/api/hardware/shudiniao）: 控制摆轮转向（左转、右转、回中）
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。正在运行的分拣任务不受影响，只对新的分拣任务生效。
/// </remarks>
[ApiController]
[Route("api/hardware")]
[Produces("application/json")]
public class HardwareConfigController : ControllerBase
{
    private readonly IDriverConfigurationRepository _driverRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly IWheelDiverterConfigurationRepository _wheelRepository;
    private readonly IWheelDiverterDriverManager? _driverManager;
    private readonly IEmcController? _emcController;
    private readonly ISystemStateManager _stateManager;
    private readonly ILogger<HardwareConfigController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public HardwareConfigController(
        IDriverConfigurationRepository driverRepository,
        ISensorConfigurationRepository sensorRepository,
        IWheelDiverterConfigurationRepository wheelRepository,
        ISystemStateManager stateManager,
        ILogger<HardwareConfigController> logger,
        IWheelDiverterDriverManager? driverManager = null,
        IEmcController? emcController = null)
    {
        _driverRepository = driverRepository;
        _sensorRepository = sensorRepository;
        _wheelRepository = wheelRepository;
        _stateManager = stateManager;
        _logger = logger;
        _driverManager = driverManager;
        _emcController = emcController;
    }

    #region 雷赛IO驱动器配置 (Leadshine IO Driver)

    /// <summary>
    /// 获取雷赛IO驱动器配置
    /// </summary>
    /// <returns>当前IO驱动器配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("leadshine")]
    [SwaggerOperation(
        Summary = "获取雷赛IO驱动器配置",
        Description = "返回当前系统的IO驱动器连接配置，包括是否使用硬件驱动、厂商类型和连接参数。",
        OperationId = "GetLeadshineIoDriverConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(IoDriverConfiguration))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<IoDriverConfiguration> GetLeadshineIoDriverConfig()
    {
        try
        {
            var config = _driverRepository.Get();
            return Ok(MapToIoDriverConfiguration(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取IO驱动器配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取IO驱动器配置失败 - Failed to get IO driver configuration"));
        }
    }

    /// <summary>
    /// 更新雷赛IO驱动器配置（支持热更新）
    /// </summary>
    /// <param name="request">IO驱动器配置请求</param>
    /// <returns>更新后的IO驱动器配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("leadshine")]
    [SwaggerOperation(
        Summary = "更新雷赛IO驱动器配置",
        Description = "更新系统IO驱动器配置，配置立即生效无需重启。",
        OperationId = "UpdateLeadshineIoDriverConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(IoDriverConfiguration))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<IoDriverConfiguration> UpdateLeadshineIoDriverConfig(
        [FromBody, SwaggerRequestBody("IO驱动器配置请求", Required = true)] DriverConfiguration request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.BadRequest($"请求参数无效: {string.Join(", ", errors)}"));
            }

            var (isValid, errorMessage) = request.Validate();
            if (!isValid)
            {
                return BadRequest(ApiResponse<object>.BadRequest(errorMessage ?? "配置验证失败"));
            }

            _driverRepository.Update(request);

            _logger.LogInformation(
                "IO驱动器配置已更新: VendorType={VendorType}, UseHardware={UseHardware}, Version={Version}",
                request.VendorType,
                request.UseHardwareDriver,
                request.Version);

            var updatedConfig = _driverRepository.Get();
            return Ok(MapToIoDriverConfiguration(updatedConfig));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "IO驱动器配置验证失败");
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新IO驱动器配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("更新IO驱动器配置失败 - Failed to update IO driver configuration"));
        }
    }

    /// <summary>
    /// 重置雷赛IO驱动器配置为默认值
    /// </summary>
    /// <returns>重置后的IO驱动器配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("leadshine/reset")]
    [SwaggerOperation(
        Summary = "重置雷赛IO驱动器配置",
        Description = "将IO驱动器配置重置为系统默认值（仿真模式）",
        OperationId = "ResetLeadshineIoDriverConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(IoDriverConfiguration))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<IoDriverConfiguration> ResetLeadshineIoDriverConfig()
    {
        try
        {
            var defaultConfig = DriverConfiguration.GetDefault();
            _driverRepository.Update(defaultConfig);

            _logger.LogInformation("IO驱动器配置已重置为默认值");

            var updatedConfig = _driverRepository.Get();
            return Ok(MapToIoDriverConfiguration(updatedConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置IO驱动器配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("重置IO驱动器配置失败 - Failed to reset IO driver configuration"));
        }
    }

    /// <summary>
    /// 重启雷赛IO驱动器（热重置）
    /// </summary>
    /// <param name="coldReset">是否执行冷重置（硬件重启，耗时较长约10秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重启操作结果</returns>
    /// <response code="200">重启成功</response>
    /// <response code="400">EMC控制器不可用（可能是仿真模式）</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("leadshine/restart")]
    [SwaggerOperation(
        Summary = "重启雷赛IO驱动器",
        Description = "重启雷赛IO驱动器，支持热重置（默认）和冷重置两种模式。",
        OperationId = "RestartLeadshineIoDriver",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "重启成功", typeof(ApiResponse<LeadshineRestartResult>))]
    [SwaggerResponse(400, "EMC控制器不可用", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<LeadshineRestartResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<LeadshineRestartResult>>> RestartLeadshineIoDriver(
        [FromQuery] bool coldReset = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_emcController == null)
            {
                _logger.LogWarning("尝试重启IO驱动器，但EMC控制器不可用（可能是仿真模式）");
                return BadRequest(ApiResponse<object>.BadRequest("EMC控制器不可用，可能是仿真模式或服务启动阶段"));
            }

            var resetType = coldReset ? "冷重置" : "热重置";
            _logger.LogInformation("开始执行雷赛IO驱动器{ResetType}，卡号: {CardNo}", resetType, _emcController.CardNo);

            bool success;
            if (coldReset)
            {
                success = await _emcController.ColdResetAsync(cancellationToken);
            }
            else
            {
                success = await _emcController.HotResetAsync(cancellationToken);
            }

            if (success)
            {
                _logger.LogInformation("雷赛IO驱动器{ResetType}成功，卡号: {CardNo}", resetType, _emcController.CardNo);
                return Ok(ApiResponse<LeadshineRestartResult>.Ok(
                    new LeadshineRestartResult
                    {
                        IsSuccess = true,
                        ResetType = coldReset ? "Cold" : "Hot",
                        CardNo = _emcController.CardNo,
                        Message = $"雷赛IO驱动器{resetType}成功"
                    },
                    $"雷赛IO驱动器{resetType}成功 - Leadshine IO driver {(coldReset ? "cold" : "hot")} reset successful"));
            }
            else
            {
                _logger.LogError("雷赛IO驱动器{ResetType}失败，卡号: {CardNo}", resetType, _emcController.CardNo);
                return StatusCode(500, ApiResponse<LeadshineRestartResult>.ServerError(
                    $"雷赛IO驱动器{resetType}失败 - Leadshine IO driver {(coldReset ? "cold" : "hot")} reset failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启雷赛IO驱动器时发生异常");
            return StatusCode(500, ApiResponse<object>.ServerError($"重启雷赛IO驱动器失败 - Failed to restart Leadshine IO driver: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取感应IO配置
    /// </summary>
    /// <returns>感应IO配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("leadshine/sensors")]
    [SwaggerOperation(
        Summary = "获取感应IO配置",
        Description = "返回当前系统的感应IO配置，包括所有感应IO的业务类型和绑定关系",
        OperationId = "GetLeadshineSensorIoConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> GetLeadshineSensorIoConfig()
    {
        try
        {
            var config = _sensorRepository.Get();
            return Ok(ApiResponse<SensorConfiguration>.Ok(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取感应IO配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取感应IO配置失败 - Failed to get sensor IO configuration"));
        }
    }

    /// <summary>
    /// 更新感应IO配置（支持热更新）
    /// </summary>
    /// <param name="request">感应IO配置请求</param>
    /// <returns>更新后的感应IO配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("leadshine/sensors")]
    [SwaggerOperation(
        Summary = "更新感应IO配置",
        Description = "更新系统感应IO配置，配置立即生效无需重启。",
        OperationId = "UpdateLeadshineSensorIoConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> UpdateLeadshineSensorIoConfig(
        [FromBody, SwaggerRequestBody("感应IO配置请求", Required = true)] SensorConfiguration request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.BadRequest($"请求参数无效: {string.Join(", ", errors)}"));
            }

            var (isValid, errorMessage) = request.Validate();
            if (!isValid)
            {
                return BadRequest(ApiResponse<object>.BadRequest(errorMessage ?? "配置验证失败"));
            }

            _sensorRepository.Update(request);

            _logger.LogInformation(
                "感应IO配置已更新: SensorCount={SensorCount}, Version={Version}",
                request.Sensors?.Count ?? 0,
                request.Version);

            var updatedConfig = _sensorRepository.Get();
            return Ok(ApiResponse<SensorConfiguration>.Ok(updatedConfig, "感应IO配置已更新 - Sensor IO configuration updated"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "感应IO配置验证失败");
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新感应IO配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("更新感应IO配置失败 - Failed to update sensor IO configuration"));
        }
    }

    /// <summary>
    /// 重置感应IO配置为默认值
    /// </summary>
    /// <returns>重置后的感应IO配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("leadshine/sensors/reset")]
    [SwaggerOperation(
        Summary = "重置感应IO配置",
        Description = "将感应IO配置重置为系统默认值",
        OperationId = "ResetLeadshineSensorIoConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> ResetLeadshineSensorIoConfig()
    {
        try
        {
            var defaultConfig = SensorConfiguration.GetDefault();
            _sensorRepository.Update(defaultConfig);

            _logger.LogInformation("感应IO配置已重置为默认值");

            var updatedConfig = _sensorRepository.Get();
            return Ok(ApiResponse<SensorConfiguration>.Ok(updatedConfig, "感应IO配置已重置为默认值 - Sensor IO configuration reset to default"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置感应IO配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("重置感应IO配置失败 - Failed to reset sensor IO configuration"));
        }
    }

    #endregion

    #region 数递鸟摆轮配置 (ShuDiNiao Wheel Diverter)

    /// <summary>
    /// 获取数递鸟摆轮配置
    /// </summary>
    /// <returns>数递鸟摆轮配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("shudiniao")]
    [SwaggerOperation(
        Summary = "获取数递鸟摆轮配置",
        Description = "返回当前系统的数递鸟摆轮设备配置，包括所有设备列表和仿真模式设置",
        OperationId = "GetShuDiNiaoWheelConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ShuDiNiaoWheelDiverterConfig))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ShuDiNiaoWheelDiverterConfig), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ShuDiNiaoWheelDiverterConfig?> GetShuDiNiaoConfig()
    {
        try
        {
            var config = _wheelRepository.Get();
            if (config.ShuDiNiao == null)
            {
                return Ok(new ShuDiNiaoWheelDiverterConfig
                {
                    Devices = new List<ShuDiNiaoDeviceEntry>()
                });
            }
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
    /// <param name="request">数递鸟摆轮配置请求（包含通信模式和设备列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的完整摆轮配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("shudiniao")]
    [SwaggerOperation(
        Summary = "更新数递鸟摆轮配置",
        Description = "更新数递鸟摆轮设备配置，支持配置通信模式（Client/Server）和设备列表。配置更新后会自动执行热更新。",
        OperationId = "UpdateShuDiNiaoWheelConfig",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(WheelDiverterConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(WheelDiverterConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<WheelDiverterConfiguration>> UpdateShuDiNiaoConfig(
        [FromBody] UpdateShuDiNiaoConfigRequest request,
        CancellationToken cancellationToken = default)
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

            var config = _wheelRepository.Get();

            config.ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
            {
                Mode = request.Mode,
                Devices = request.Devices.Select(d => new ShuDiNiaoDeviceEntry
                {
                    DiverterId = d.DiverterId,
                    Host = d.Host,
                    Port = d.Port,
                    DeviceAddress = d.DeviceAddress,
                    IsEnabled = d.IsEnabled
                }).ToList()
            };

            if (request.Devices.Any())
            {
                config.VendorType = WheelDiverterVendorType.ShuDiNiao;
            }

            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            _wheelRepository.Update(config);

            _logger.LogInformation(
                "数递鸟摆轮配置已更新: 设备数量={DeviceCount}",
                request.Devices.Count);

            if (_driverManager != null)
            {
                var applyResult = await _driverManager.ApplyConfigurationAsync(config, cancellationToken);

                if (applyResult.IsSuccess)
                {
                    _logger.LogInformation(
                        "数递鸟摆轮驱动器热更新成功: 已连接={ConnectedCount}/{TotalCount}",
                        applyResult.ConnectedCount,
                        applyResult.TotalCount);
                }
                else
                {
                    _logger.LogWarning(
                        "数递鸟摆轮驱动器热更新部分失败: {ErrorMessage}, 失败设备={FailedDrivers}",
                        applyResult.ErrorMessage,
                        string.Join(", ", applyResult.FailedDriverIds));
                }
            }
            else
            {
                _logger.LogDebug("驱动管理器未注册，跳过热更新");
            }

            var updatedConfig = _wheelRepository.Get();
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
    /// 测试数递鸟摆轮转向（调试/测试用）
    /// </summary>
    /// <param name="request">测试请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>测试执行结果，包含每个摆轮的执行状态、连接信息和发送的指令</returns>
    /// <response code="200">测试执行成功</response>
    /// <response code="400">请求参数无效或驱动管理器未注册</response>
    /// <response code="409">系统正在运行中或处于急停状态</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 测试指定摆轮的转向功能，系统运行中或急停状态下禁止使用。
    /// 
    /// **响应内容说明**：
    /// - **connectionInfo**: 摆轮的TCP连接地址（IP:端口格式），例如 "192.168.0.200:200"
    /// - **commandSent**: 发送到摆轮的实际指令（十六进制格式），例如 "51 52 57 51 52 51 FE"
    ///   - 指令格式：起始字节(2) + 长度(1) + 设备地址(1) + 消息类型(1) + 命令码(1) + 结束字节(1)
    ///   - 左转命令码：0x51，右转命令码：0x52，回中命令码：0x53
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "totalCount": 1,
    ///   "successCount": 1,
    ///   "failedCount": 0,
    ///   "results": [
    ///     {
    ///       "diverterId": 1,
    ///       "direction": "Left",
    ///       "isSuccess": true,
    ///       "message": "摆轮 1 已执行 Left 操作",
    ///       "connectionInfo": "192.168.0.200:200",
    ///       "commandSent": "51 52 57 51 52 51 FE"
    ///     }
    ///   ]
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 此端点仅用于调试和测试，生产环境使用需谨慎
    /// - 系统处于运行或急停状态时无法使用
    /// - 命令会立即发送到物理设备，请确保设备连接正常
    /// </remarks>
    [HttpPost("shudiniao/test")]
    [SwaggerOperation(
        Summary = "测试数递鸟摆轮转向",
        Description = "测试指定摆轮的转向功能，返回执行结果及连接信息、发送的指令（十六进制格式）",
        OperationId = "TestShuDiNiaoDiverters",
        Tags = new[] { "硬件配置" }
    )]
    [SwaggerResponse(200, "测试执行成功", typeof(WheelDiverterTestResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(409, "系统正在运行中或处于急停状态")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(WheelDiverterTestResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<WheelDiverterTestResponse>> TestShuDiNiaoDiverters(
        [FromBody] WheelDiverterTestRequest request,
        CancellationToken cancellationToken = default)
    {
        return await TestDivertersInternal(request, "数递鸟", cancellationToken);
    }

    #endregion

    #region 私有方法

    private static IoDriverConfiguration MapToIoDriverConfiguration(DriverConfiguration config)
    {
        return new IoDriverConfiguration
        {
            Id = config.Id,
            UseHardwareDriver = config.UseHardwareDriver,
            VendorType = config.VendorType,
            VendorDisplayName = GetVendorDisplayName(config.VendorType),
            Leadshine = config.Leadshine != null ? new LeadshineIoConnectionConfig
            {
                ControllerIp = config.Leadshine.ControllerIp,
                CardNo = config.Leadshine.CardNo,
                PortNo = config.Leadshine.PortNo
            } : null,
            Version = config.Version,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    private static string GetVendorDisplayName(DriverVendorType vendorType)
    {
        return vendorType switch
        {
            DriverVendorType.Mock => "模拟驱动器",
            DriverVendorType.Leadshine => "雷赛",
            DriverVendorType.Siemens => "西门子",
            DriverVendorType.Mitsubishi => "三菱",
            DriverVendorType.Omron => "欧姆龙",
            _ => vendorType.ToString()
        };
    }

    private async Task<ActionResult<WheelDiverterTestResponse>> TestDivertersInternal(
        WheelDiverterTestRequest request,
        string vendorName,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentState = _stateManager.CurrentState;
            if (currentState == SystemState.Running || currentState == SystemState.EmergencyStop)
            {
                var stateMessage = currentState == SystemState.Running ? "运行中" : "急停";
                _logger.LogWarning("系统处于{State}状态，禁止使用摆轮测试端点", stateMessage);
                return Conflict(new { message = $"系统处于{stateMessage}状态，禁止使用摆轮测试端点" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效", errors });
            }

            if (request.DiverterIds == null || !request.DiverterIds.Any())
            {
                return BadRequest(new { message = "摆轮ID列表不能为空" });
            }

            if (_driverManager == null)
            {
                return BadRequest(new { message = "摆轮驱动管理器未注册，可能是仿真模式或系统启动阶段" });
            }

            // Get wheel diverter configuration to retrieve connection info
            var wheelConfig = _wheelRepository.Get();
            var deviceConfigMap = wheelConfig.ShuDiNiao?.Devices?
                .ToDictionary(d => d.DiverterId, d => d) ?? new Dictionary<long, ShuDiNiaoDeviceEntry>();

            var results = new List<WheelDiverterTestResult>();
            var activeDrivers = _driverManager.GetActiveDrivers();

            foreach (var diverterId in request.DiverterIds)
            {
                WheelDiverterTestResult result;

                try
                {
                    // Get device configuration
                    string? connectionInfo = null;
                    string? commandSent = null;
                    
                    if (deviceConfigMap.TryGetValue(diverterId, out var deviceConfig))
                    {
                        connectionInfo = $"{deviceConfig.Host}:{deviceConfig.Port}";
                        
                        // Build the protocol command frame to show what would be sent
                        var command = request.Direction switch
                        {
                            DiverterDirection.Left => ShuDiNiaoControlCommand.TurnLeft,
                            DiverterDirection.Right => ShuDiNiaoControlCommand.TurnRight,
                            DiverterDirection.Straight => ShuDiNiaoControlCommand.ReturnCenter,
                            _ => ShuDiNiaoControlCommand.Stop
                        };
                        
                        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceConfig.DeviceAddress, command);
                        commandSent = ShuDiNiaoProtocol.FormatBytes(frame);
                    }

                    if (!activeDrivers.TryGetValue(diverterId.ToString(), out var driver))
                    {
                        result = new WheelDiverterTestResult
                        {
                            DiverterId = diverterId,
                            Direction = request.Direction,
                            IsSuccess = false,
                            Message = $"未找到摆轮 {diverterId} 的驱动器",
                            ConnectionInfo = connectionInfo,
                            CommandSent = commandSent
                        };
                        results.Add(result);
                        continue;
                    }

                    bool operationSuccess = request.Direction switch
                    {
                        DiverterDirection.Left => await driver.TurnLeftAsync(cancellationToken),
                        DiverterDirection.Right => await driver.TurnRightAsync(cancellationToken),
                        DiverterDirection.Straight => await driver.PassThroughAsync(cancellationToken),
                        _ => false
                    };

                    result = new WheelDiverterTestResult
                    {
                        DiverterId = diverterId,
                        Direction = request.Direction,
                        IsSuccess = operationSuccess,
                        Message = operationSuccess
                            ? $"摆轮 {diverterId} 已执行 {request.Direction} 操作"
                            : $"摆轮 {diverterId} 执行 {request.Direction} 操作失败",
                        ConnectionInfo = connectionInfo,
                        CommandSent = commandSent
                    };

                    _logger.LogInformation(
                        "测试{VendorName}摆轮转向: DiverterId={DiverterId}, Direction={Direction}, Success={Success}, Connection={Connection}, Command={Command}",
                        vendorName, diverterId, request.Direction, operationSuccess, connectionInfo, commandSent);
                }
                catch (Exception ex)
                {
                    // Try to get connection info even on error
                    string? connectionInfo = null;
                    string? commandSent = null;
                    
                    if (deviceConfigMap.TryGetValue(diverterId, out var deviceConfig))
                    {
                        connectionInfo = $"{deviceConfig.Host}:{deviceConfig.Port}";
                        var command = request.Direction switch
                        {
                            DiverterDirection.Left => ShuDiNiaoControlCommand.TurnLeft,
                            DiverterDirection.Right => ShuDiNiaoControlCommand.TurnRight,
                            DiverterDirection.Straight => ShuDiNiaoControlCommand.ReturnCenter,
                            _ => ShuDiNiaoControlCommand.Stop
                        };
                        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceConfig.DeviceAddress, command);
                        commandSent = ShuDiNiaoProtocol.FormatBytes(frame);
                    }
                    
                    result = new WheelDiverterTestResult
                    {
                        DiverterId = diverterId,
                        Direction = request.Direction,
                        IsSuccess = false,
                        Message = $"摆轮 {diverterId} 执行异常: {ex.Message}",
                        ConnectionInfo = connectionInfo,
                        CommandSent = commandSent
                    };
                    _logger.LogError(ex, "测试{VendorName}摆轮转向异常: DiverterId={DiverterId}", vendorName, diverterId);
                }

                results.Add(result);
            }

            var response = new WheelDiverterTestResponse
            {
                TotalCount = results.Count,
                SuccessCount = results.Count(r => r.IsSuccess),
                FailedCount = results.Count(r => !r.IsSuccess),
                Results = results
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试{VendorName}摆轮转向失败", vendorName);
            return StatusCode(500, new { message = $"测试{vendorName}摆轮转向失败" });
        }
    }

    #endregion
}

#region DTOs for HardwareConfigController

/// <summary>
/// IO驱动器配置响应模型
/// </summary>
public class IoDriverConfiguration
{
    /// <summary>
    /// 配置唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 是否使用硬件驱动器
    /// </summary>
    public bool UseHardwareDriver { get; set; }

    /// <summary>
    /// IO驱动器厂商类型
    /// </summary>
    public DriverVendorType VendorType { get; set; }

    /// <summary>
    /// 厂商显示名称
    /// </summary>
    public string VendorDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 雷赛运动控制卡连接配置
    /// </summary>
    public LeadshineIoConnectionConfig? Leadshine { get; set; }

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

/// <summary>
/// 雷赛IO驱动器连接配置
/// </summary>
public class LeadshineIoConnectionConfig
{
    /// <summary>
    /// 控制器IP地址（以太网模式）
    /// </summary>
    public string? ControllerIp { get; set; } = "192.168.5.11";

    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; set; } = 8;

    /// <summary>
    /// 端口号
    /// </summary>
    public ushort PortNo { get; set; } = 2;
}

/// <summary>
/// 雷赛IO驱动器重启结果
/// </summary>
public record LeadshineRestartResult
{
    /// <summary>
    /// 重启操作是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 重置类型
    /// </summary>
    public string ResetType { get; init; } = string.Empty;

    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; init; }

    /// <summary>
    /// 操作结果消息
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// 更新数递鸟摆轮配置请求
/// </summary>
public record class UpdateShuDiNiaoConfigRequest
{
    /// <summary>
    /// 通信模式（Client=客户端模式，Server=服务端模式）
    /// </summary>
    /// <remarks>
    /// - Client（默认）: 系统作为客户端，主动连接到摆轮设备
    /// - Server: 系统作为服务端，监听设备连接
    /// 注意：此配置为全局配置，所有数递鸟设备使用同一种模式
    /// </remarks>
    public ShuDiNiaoMode Mode { get; init; } = ShuDiNiaoMode.Client;
    
    /// <summary>
    /// 数递鸟摆轮设备列表
    /// </summary>
    [Required(ErrorMessage = "设备列表不能为空")]
    public required List<ShuDiNiaoDeviceRequest> Devices { get; init; }
}

/// <summary>
/// 数递鸟摆轮设备请求
/// </summary>
public record class ShuDiNiaoDeviceRequest
{
    /// <summary>
    /// 摆轮标识符
    /// </summary>
    [Required(ErrorMessage = "摆轮标识符不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "摆轮标识符必须大于0")]
    public required long DiverterId { get; init; }

    /// <summary>
    /// TCP连接主机地址
    /// </summary>
    [Required(ErrorMessage = "主机地址不能为空")]
    [StringLength(255, ErrorMessage = "主机地址长度不能超过255个字符")]
    public required string Host { get; init; }

    /// <summary>
    /// TCP连接端口
    /// </summary>
    [Range(1, 65535, ErrorMessage = "端口号必须在1到65535之间")]
    public required int Port { get; init; }

    /// <summary>
    /// 设备地址
    /// </summary>
    [Range(0x51, 0xFF, ErrorMessage = "设备地址必须在0x51到0xFF之间")]
    public required byte DeviceAddress { get; init; }

    /// <summary>
    /// 是否启用该设备
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// 摆轮测试请求
/// </summary>
public record class WheelDiverterTestRequest
{
    /// <summary>
    /// 摆轮ID列表
    /// </summary>
    [Required(ErrorMessage = "摆轮ID列表不能为空")]
    public required List<long> DiverterIds { get; init; }

    /// <summary>
    /// 转向方向
    /// </summary>
    [Required(ErrorMessage = "转向方向不能为空")]
    public required DiverterDirection Direction { get; init; }
}

/// <summary>
/// 摆轮测试响应
/// </summary>
public record class WheelDiverterTestResponse
{
    /// <summary>
    /// 测试摆轮总数
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 成功数量
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// 失败数量
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// 各摆轮测试结果详情
    /// </summary>
    public required List<WheelDiverterTestResult> Results { get; init; }
}

/// <summary>
/// 单个摆轮测试结果
/// </summary>
public record class WheelDiverterTestResult
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public required long DiverterId { get; init; }

    /// <summary>
    /// 测试的转向方向
    /// </summary>
    public DiverterDirection Direction { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 结果消息
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// 连接信息（IP:端口）
    /// </summary>
    /// <example>192.168.0.200:200</example>
    public string? ConnectionInfo { get; init; }
    
    /// <summary>
    /// 发送的指令（十六进制格式）
    /// </summary>
    /// <example>51 52 57 51 52 51 FE</example>
    public string? CommandSent { get; init; }
}

#endregion

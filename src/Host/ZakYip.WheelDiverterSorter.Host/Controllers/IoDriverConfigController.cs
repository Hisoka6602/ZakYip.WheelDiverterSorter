using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Utilities;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// IO驱动器配置管理API控制器
/// IO driver configuration management API controller
/// </summary>
/// <remarks>
/// 提供IO驱动器配置和感应IO配置的查询和更新功能，支持热更新。
/// 
/// **重要说明**：
/// 此API用于配置【IO驱动器】和【感应IO】，而非【摆轮驱动器】。概念区分：
/// 
/// - **IO驱动器**（本API）: 控制IO端点（输入/输出位），用于传感器信号读取和继电器控制
///   - 雷赛（Leadshine）: 雷赛运动控制卡的IO接口
///   - 西门子（Siemens）: S7系列PLC的IO模块
///   - 三菱（Mitsubishi）: 三菱PLC的IO模块
///   - 欧姆龙（Omron）: 欧姆龙PLC的IO模块
/// 
/// - **感应IO**（本API /sensors 子路径）: 配置感应IO的业务类型和绑定关系
///   - ParcelCreation: 创建包裹感应IO
///   - WheelFront: 摆轮前感应IO
///   - ChuteLock: 锁格感应IO
/// 
/// - **摆轮驱动器**（/api/config/wheeldiverter/* API）: 控制摆轮转向（左转、右转、回中）
///   - 数递鸟（ShuDiNiao）: 通过TCP协议直接控制
///   - 莫迪（Modi）: 通过TCP协议直接控制
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。正在运行的分拣任务不受影响，只对新的分拣任务生效。
/// </remarks>
[ApiController]
[Route("api/config/io-driver/leadshine")]
[Produces("application/json")]
public class IoDriverConfigController : ControllerBase
{
    private readonly IDriverConfigurationRepository _driverRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly ILogger<IoDriverConfigController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="driverRepository">IO驱动器配置仓储</param>
    /// <param name="sensorRepository">感应IO配置仓储</param>
    /// <param name="logger">日志记录器</param>
    public IoDriverConfigController(
        IDriverConfigurationRepository driverRepository,
        ISensorConfigurationRepository sensorRepository,
        ILogger<IoDriverConfigController> logger)
    {
        _driverRepository = driverRepository;
        _sensorRepository = sensorRepository;
        _logger = logger;
    }

    #region IO驱动器配置

    /// <summary>
    /// 获取IO驱动器配置
    /// </summary>
    /// <returns>当前IO驱动器配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前系统的IO驱动器连接配置，包括：
    /// - 是否使用硬件驱动
    /// - 厂商类型（雷赛、西门子等）
    /// - 厂商显示名称（中文）
    /// - 厂商特定连接参数（仅连接参数，不含摆轮映射）
    /// 
    /// **注意**：此API仅返回IO驱动器连接配置，不包含摆轮映射（diverters）信息。
    /// 摆轮映射配置请使用 /api/config/wheeldiverter/* 相关API。
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "id": 1,
    ///   "useHardwareDriver": false,
    ///   "vendorType": "Leadshine",
    ///   "vendorDisplayName": "雷赛",
    ///   "leadshine": { "cardNo": 0 },
    ///   "version": 1
    /// }
    /// ```
    /// </remarks>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取IO驱动器配置（雷赛等）",
        Description = "返回当前系统的IO驱动器连接配置，包括是否使用硬件驱动、厂商类型和连接参数。注意：此API仅返回连接配置，不包含摆轮映射（diverters）信息。",
        OperationId = "GetIoDriverConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(IoDriverConfiguration))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<IoDriverConfiguration> GetIoDriverConfig()
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
    /// **示例请求**:
    /// ```json
    /// {
    ///     "useHardwareDriver": false,
    ///     "vendorType": "Leadshine",
    ///     "leadshine": {
    ///         "cardNo": 0,
    ///         "diverters": [
    ///             { "diverterId": 1, "diverterName": "D1", "outputStartBit": 0, "feedbackInputBit": 10 }
    ///         ]
    ///     }
    /// }
    /// ```
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 
    /// **支持的IO驱动厂商类型（vendorType）**：
    /// - Mock: 模拟驱动器（用于测试）
    /// - Leadshine: 雷赛运动控制卡
    /// - Siemens: 西门子PLC
    /// - Mitsubishi: 三菱PLC
    /// - Omron: 欧姆龙PLC
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新IO驱动器配置",
        Description = "更新系统IO驱动器配置，配置立即生效无需重启。支持配置硬件/模拟驱动器切换、厂商选择和厂商特定参数。",
        OperationId = "UpdateIoDriverConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(IoDriverConfiguration))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<IoDriverConfiguration> UpdateIoDriverConfig(
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

            // 验证配置
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

            // 重新获取更新后的配置
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
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(IoDriverConfiguration), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<IoDriverConfiguration> ResetIoDriverConfig()
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

    #endregion

    #region 感应IO配置

    /// <summary>
    /// 获取感应IO配置
    /// </summary>
    /// <returns>感应IO配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前系统的感应IO配置，包括所有感应IO的业务类型和绑定关系。
    /// 
    /// **感应IO类型**：
    /// - ParcelCreation: 创建包裹感应IO（只能有一个激活）
    /// - WheelFront: 摆轮前感应IO（必须绑定摆轮节点）
    /// - ChuteLock: 锁格感应IO（必须绑定格口）
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "success": true,
    ///   "code": "Ok",
    ///   "data": {
    ///     "sensors": [
    ///       { "sensorId": 1, "sensorName": "创建包裹IO", "ioType": "ParcelCreation", "ioPointId": 0 },
    ///       { "sensorId": 2, "sensorName": "摆轮1前IO", "ioType": "WheelFront", "ioPointId": 1, "boundWheelNodeId": "WHEEL-1" }
    ///     ],
    ///     "version": 1
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpGet("sensors")]
    [SwaggerOperation(
        Summary = "获取感应IO配置",
        Description = "返回当前系统的感应IO配置，包括所有感应IO的业务类型、关联的IO点位和绑定关系",
        OperationId = "GetSensorIoConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> GetSensorIoConfig()
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
    /// <param name="request">感应IO配置请求，包含感应IO列表及其业务类型和绑定关系</param>
    /// <returns>更新后的感应IO配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 更新系统感应IO配置，配置立即生效无需重启。
    /// 
    /// **配置规则**：
    /// - 只能有一个激活的 ParcelCreation 类型感应IO
    /// - WheelFront 类型必须绑定摆轮节点（boundWheelNodeId）
    /// - ChuteLock 类型必须绑定格口（boundChuteId）
    /// - SensorId 不能重复
    /// 
    /// **示例请求**：
    /// ```json
    /// {
    ///   "sensors": [
    ///     { "sensorId": 1, "sensorName": "创建包裹IO", "ioType": "ParcelCreation", "ioPointId": 0, "isEnabled": true },
    ///     { "sensorId": 2, "sensorName": "摆轮1前IO", "ioType": "WheelFront", "ioPointId": 1, "boundWheelNodeId": "WHEEL-1", "isEnabled": true }
    ///   ]
    /// }
    /// ```
    /// </remarks>
    [HttpPut("sensors")]
    [SwaggerOperation(
        Summary = "更新感应IO配置",
        Description = "更新系统感应IO配置，配置立即生效无需重启。支持配置感应IO的业务类型、IO点位和绑定关系。",
        OperationId = "UpdateSensorIoConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> UpdateSensorIoConfig(
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

            // 验证配置
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
    /// <remarks>
    /// 将感应IO配置重置为系统默认值：
    /// - 创建包裹感应IO (SensorId=1)
    /// - 摆轮1前感应IO (SensorId=2)
    /// - 格口1锁格感应IO (SensorId=3)
    /// 
    /// 重置操作会立即生效，适用于：
    /// - 配置出错需要恢复默认
    /// - 快速初始化测试环境
    /// - 清除不正确的配置参数
    /// </remarks>
    [HttpPost("sensors/reset")]
    [SwaggerOperation(
        Summary = "重置感应IO配置",
        Description = "将感应IO配置重置为系统默认值",
        OperationId = "ResetSensorIoConfig",
        Tags = new[] { "IO驱动器配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(ApiResponse<SensorConfiguration>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<SensorConfiguration>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<SensorConfiguration>> ResetSensorIoConfig()
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

    #region 私有方法
    
    /// <summary>
    /// 将 DriverConfiguration 映射到 IoDriverConfiguration（仅连接配置，不含摆轮映射）
    /// </summary>
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

    /// <summary>
    /// 获取厂商显示名称（中文）
    /// </summary>
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

    #endregion
}

/// <summary>
/// IO驱动器配置响应模型
/// IO driver configuration response model
/// </summary>
/// <remarks>
/// IO驱动器配置用于连接配置，不包含摆轮映射（diverters）信息。
/// 摆轮映射配置请使用 /api/config/wheeldiverter/* 相关API。
/// 
/// 当前支持的厂商：
/// - Leadshine（雷赛）: 雷赛运动控制卡IO接口
/// - Siemens（西门子）: S7系列PLC的IO模块
/// - Mitsubishi（三菱）: 三菱PLC的IO模块
/// - Omron（欧姆龙）: 欧姆龙PLC的IO模块
/// </remarks>
public class IoDriverConfiguration
{
    /// <summary>
    /// 配置唯一标识
    /// Unique identifier of the configuration
    /// </summary>
    /// <example>1</example>
    public int Id { get; set; }

    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// Whether to use hardware driver (false uses simulated driver)
    /// </summary>
    /// <example>true</example>
    public bool UseHardwareDriver { get; set; }

    /// <summary>
    /// IO驱动器厂商类型
    /// IO driver vendor type
    /// </summary>
    /// <example>Leadshine</example>
    public DriverVendorType VendorType { get; set; }

    /// <summary>
    /// 厂商显示名称（中文）
    /// Vendor display name in Chinese
    /// </summary>
    /// <example>雷赛</example>
    public string VendorDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 雷赛运动控制卡连接配置
    /// Leadshine motion controller connection configuration
    /// </summary>
    /// <remarks>
    /// 包含与雷赛运动控制卡建立连接所需的参数（ControllerIp, CardNo, PortNo）。
    /// 摆轮IO映射（输出位、反馈输入位）等信息请参考摆轮配置API。
    /// </remarks>
    public LeadshineIoConnectionConfig? Leadshine { get; set; }

    /// <summary>
    /// 配置版本号
    /// Configuration version number
    /// </summary>
    /// <example>1</example>
    public int Version { get; set; }

    /// <summary>
    /// 配置创建时间
    /// Configuration creation time
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 配置最后更新时间
    /// Configuration last update time
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 雷赛IO驱动器连接配置（仅连接参数，不含摆轮映射）
/// Leadshine IO driver connection configuration (connection parameters only, no diverter mappings)
/// </summary>
/// <remarks>
/// 此配置基于 ZakYip.Singulation 项目的 LeadshineLtdmcBusAdapter 实现。
/// 包含与雷赛运动控制卡建立连接所需的参数。
/// 支持以太网模式（需要 ControllerIp）和本地 PCI 模式（ControllerIp 为空）。
/// 摆轮IO映射（输出位、反馈输入位）等信息请参考摆轮配置API。
/// </remarks>
public class LeadshineIoConnectionConfig
{
    /// <summary>
    /// 控制器IP地址（以太网模式）
    /// Controller IP address for Ethernet mode
    /// </summary>
    /// <remarks>
    /// 以太网模式需要配置控制器的IP地址。
    /// 如果为空或null，则使用本地PCI模式（dmc_board_init）。
    /// 如果配置了IP，则使用以太网模式（dmc_board_init_eth）。
    /// </remarks>
    /// <example>192.168.1.100</example>
    public string? ControllerIp { get; set; }

    /// <summary>
    /// 控制器卡号
    /// Controller card number
    /// </summary>
    /// <example>0</example>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 端口号（CAN/EtherCAT端口编号）
    /// Port number (CAN/EtherCAT port number)
    /// </summary>
    /// <example>0</example>
    public ushort PortNo { get; set; } = 0;
}

using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Validation;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// IO 联动配置管理API控制器
/// </summary>
/// <remarks>
/// 提供中段皮带等设备的IO联动配置管理功能。
/// 
/// **功能概述**：
/// - IO联动用于根据系统运行状态自动控制外部设备的IO点位
/// - 支持配置系统运行时和停止时的IO输出状态
/// - 支持单点和批量IO操作，便于测试和手动控制
/// 
/// **IO点标识方式**：
/// - 使用 BitNumber（0-1023）标识IO端口
/// - 使用 Level（ActiveHigh/ActiveLow）指定电平有效性
/// - BitNumber 对应物理硬件的输入/输出端口号
/// 
/// **使用场景**：
/// - 配置管理：通过 GET/PUT 端点管理IO联动配置
/// - 自动联动：系统状态改变时自动应用配置的IO输出
/// - 手动控制：通过 set 端点手动控制单个或多个IO点
/// - 状态查询：通过 status 端点查询IO点当前状态
/// 
/// **注意事项**：
/// - IO操作会直接影响物理硬件输出
/// - 生产环境请谨慎使用手动控制功能
/// - 建议先在仿真环境测试IO配置
/// </remarks>
[ApiController]
[Route("api/config/io-linkage")]
[Produces("application/json")]
public class IoLinkageController : ControllerBase
{
    private readonly IIoLinkageConfigurationRepository _repository;
    private readonly IIoLinkageDriver _ioLinkageDriver;
    private readonly IIoLinkageCoordinator _ioLinkageCoordinator;
    private readonly ISystemStateManager _systemStateManager;
    private readonly ISystemClock _systemClock;
    private readonly ILogger<IoLinkageController> _logger;

    public IoLinkageController(
        IIoLinkageConfigurationRepository repository,
        IIoLinkageDriver ioLinkageDriver,
        IIoLinkageCoordinator ioLinkageCoordinator,
        ISystemStateManager systemStateManager,
        ISystemClock systemClock,
        ILogger<IoLinkageController> logger)
    {
        _repository = repository;
        _ioLinkageDriver = ioLinkageDriver;
        _ioLinkageCoordinator = ioLinkageCoordinator;
        _systemStateManager = systemStateManager;
        _systemClock = systemClock;
        _logger = logger;
    }

    /// <summary>
    /// 获取IO联动配置
    /// </summary>
    /// <returns>IO联动配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取IO联动配置",
        Description = "返回当前的IO联动配置，包括启用状态、运行/停止时的IO点位设置等",
        OperationId = "GetIoLinkageConfig",
        Tags = new[] { "IO联动配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(IoLinkageConfigResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(IoLinkageConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IoLinkageConfigResponse> GetIoLinkageConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 IO 联动配置失败");
            return StatusCode(500, new { message = "获取 IO 联动配置失败" });
        }
    }

    /// <summary>
    /// 更新IO联动配置（支持热更新）
    /// </summary>
    /// <param name="request">IO联动配置请求，包含启用状态和各种状态下的IO点位设置</param>
    /// <returns>更新后的IO联动配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/io-linkage
    ///     {
    ///         "enabled": true,
    ///         "runningStateIos": [
    ///             { "bitNumber": 3, "level": "ActiveLow" },
    ///             { "bitNumber": 5, "level": "ActiveLow" }
    ///         ],
    ///         "stoppedStateIos": [
    ///             { "bitNumber": 3, "level": "ActiveHigh" },
    ///             { "bitNumber": 5, "level": "ActiveHigh" }
    ///         ],
    ///         "emergencyStopStateIos": [
    ///             { "bitNumber": 10, "level": "ActiveHigh" }
    ///         ],
    ///         "upstreamConnectionExceptionStateIos": [
    ///             { "bitNumber": 11, "level": "ActiveHigh" }
    ///         ],
    ///         "diverterExceptionStateIos": [
    ///             { "bitNumber": 12, "level": "ActiveHigh" }
    ///         ],
    ///         "postPreStartWarningStateIos": [
    ///             { "bitNumber": 13, "level": "ActiveHigh" }
    ///         ],
    ///         "wheelDiverterDisconnectedStateIos": [
    ///             { "bitNumber": 14, "level": "ActiveHigh" }
    ///         ]
    ///     }
    /// 
    /// IO电平说明：
    /// - ActiveLow：低电平有效（输出0时设备工作）
    /// - ActiveHigh：高电平有效（输出1时设备工作）
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 系统根据不同状态自动应用对应的IO联动配置：
    /// - runningStateIos：系统正常运行时
    /// - stoppedStateIos：系统停止时
    /// - emergencyStopStateIos：急停时
    /// - upstreamConnectionExceptionStateIos：上游连接异常时
    /// - diverterExceptionStateIos：摆轮异常时
    /// - postPreStartWarningStateIos：运行前预警结束后（preStartWarning durationSeconds 等待完成后触发）
    /// - wheelDiverterDisconnectedStateIos：摆轮断联/异常时（首次连接成功后，摆轮断联或异常触发）
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新IO联动配置",
        Description = "更新IO联动配置，配置立即生效。根据系统运行状态自动控制IO点位输出",
        OperationId = "UpdateIoLinkageConfig",
        Tags = new[] { "IO联动配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(IoLinkageConfigResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(IoLinkageConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<IoLinkageConfigResponse>> UpdateIoLinkageConfig([FromBody] IoLinkageConfigRequest request)
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

            // 将请求映射到域配置模型
            var config = MapToConfiguration(request);
            
            // 保存配置
            _repository.Update(config);

            _logger.LogInformation(
                "IO 联动配置已更新: Enabled={Enabled}, RunningIos={RunningCount}, StoppedIos={StoppedCount}, " + 
                "EmergencyStopIos={EmergencyStopCount}, UpstreamExceptionIos={UpstreamExceptionCount}, DiverterExceptionIos={DiverterExceptionCount}, " +
                "PostPreStartWarningIos={PostPreStartWarningCount}, WheelDiverterDisconnectedIos={WheelDiverterDisconnectedCount}",
                config.Enabled,
                config.RunningStateIos.Count,
                config.StoppedStateIos.Count,
                config.EmergencyStopStateIos.Count,
                config.UpstreamConnectionExceptionStateIos.Count,
                config.DiverterExceptionStateIos.Count,
                config.PostPreStartWarningStateIos.Count,
                config.WheelDiverterDisconnectedStateIos.Count);

            // 热更新：立即应用IO联动配置到当前系统状态
            if (config.Enabled)
            {
                var currentState = _systemStateManager.CurrentState;
                var options = ConvertToOptions(config);
                var ioPoints = _ioLinkageCoordinator.DetermineIoLinkagePoints(currentState, options);
                
                if (ioPoints.Count > 0)
                {
                    await _ioLinkageDriver.SetIoPointsAsync(ioPoints);
                    _logger.LogInformation(
                        "IO 联动配置热更新成功：已将 {Count} 个IO点应用到当前系统状态 {SystemState}",
                        ioPoints.Count,
                        currentState);
                }
                else
                {
                    _logger.LogInformation(
                        "IO 联动配置热更新：当前系统状态 {SystemState} 无需设置IO点",
                        currentState);
                }
            }

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 IO 联动配置失败");
            return StatusCode(500, new { message = "更新 IO 联动配置失败" });
        }
    }

    /// <summary>
    /// 手动触发IO联动
    /// </summary>
    /// <param name="systemState">目标系统状态，支持的值：Running（运行）、Stopped（停止）、Standby（待机）</param>
    /// <returns>触发结果</returns>
    /// <response code="200">触发成功</response>
    /// <response code="400">参数无效或IO联动未启用</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     POST /api/config/io-linkage/trigger?systemState=Running
    /// 
    /// 系统状态说明：
    /// - Running：系统运行状态，应用运行时IO配置
    /// - Stopped：系统停止状态，应用停止时IO配置
    /// - Standby：系统待机状态
    /// 
    /// 此接口用于测试或手动控制IO联动，通常由系统状态机自动触发。
    /// </remarks>
    [HttpPost("trigger")]
    [SwaggerOperation(
        Summary = "手动触发IO联动",
        Description = "根据指定的系统状态手动触发IO联动，应用对应的IO点位配置",
        OperationId = "TriggerIoLinkage",
        Tags = new[] { "IO联动配置" }
    )]
    [SwaggerResponse(200, "触发成功", typeof(object))]
    [SwaggerResponse(400, "参数无效或IO联动未启用")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> TriggerIoLinkage([FromQuery] SystemState systemState)
    {
        try
        {
            var config = _repository.Get();

            if (!config.Enabled)
            {
                return BadRequest(new { message = "IO 联动功能未启用" });
            }

            // 转换为 IoLinkageOptions 用于协调器
            var options = ConvertToOptions(config);
            
            // 确定需要设置的 IO 点
            var ioPoints = _ioLinkageCoordinator.DetermineIoLinkagePoints(systemState, options);

            if (ioPoints.Count == 0)
            {
                return BadRequest(new 
                { 
                    message = $"系统状态 {systemState} 下无 IO 联动配置" 
                });
            }

            // 执行 IO 联动
            await _ioLinkageDriver.SetIoPointsAsync(ioPoints);

            _logger.LogInformation(
                "手动触发 IO 联动成功: SystemState={SystemState}, IoPointsCount={Count}",
                systemState,
                ioPoints.Count);

            return Ok(new
            {
                message = "IO 联动触发成功",
                systemState = systemState.ToString(),
                triggeredIoPoints = ioPoints.Select(p => new
                {
                    bitNumber = p.BitNumber,
                    level = p.Level.ToString()
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发 IO 联动失败: SystemState={SystemState}", systemState);
            return StatusCode(500, new { message = "触发 IO 联动失败" });
        }
    }

    /// <summary>
    /// 查询指定 IO 点的状态
    /// </summary>
    /// <param name="bitNumber">IO 端口编号（0-1023）</param>
    /// <returns>IO 点状态</returns>
    /// <response code="200">查询成功</response>
    /// <response code="400">参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **功能说明**：
    /// 
    /// 读取单个IO端口的当前电平状态（true=高电平，false=低电平）。
    /// 
    /// **IO点标识**：
    /// - BitNumber: IO端口编号，范围 0-1023
    /// - 对应物理硬件的实际IO端口
    /// 
    /// **使用场景**：
    /// - 验证IO联动配置是否生效
    /// - 诊断硬件连接问题
    /// - 监控外部设备状态
    /// 
    /// **示例**：
    /// ```
    /// GET /api/config/io-linkage/status/3
    /// 
    /// 响应：
    /// {
    ///   "bitNumber": 3,
    ///   "state": true,
    ///   "level": "High"
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 返回的state是原始电平状态（true=高，false=低）
    /// - 不考虑 ActiveLevel 配置，仅返回物理电平
    /// - 读取操作不影响IO输出状态
    /// </remarks>
    [HttpGet("status/{bitNumber}")]
    [SwaggerOperation(
        Summary = "查询指定IO点状态",
        Description = "读取单个IO端口的当前电平状态",
        OperationId = "GetIoPointStatus",
        Tags = new[] { "IO联动配置" }
    )]
    [SwaggerResponse(200, "查询成功", typeof(object))]
    [SwaggerResponse(400, "参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetIoPointStatus(int bitNumber)
    {
        try
        {
            // 验证 IO 端点有效性
            if (!IoEndpointValidator.IsValidEndpoint(bitNumber))
            {
                var errorMsg = IoEndpointValidator.GetValidationError(bitNumber);
                _logger.LogWarning("尝试读取无效 IO 端点: BitNumber={BitNumber}, Error={Error}", bitNumber, errorMsg);
                return BadRequest(new { message = errorMsg });
            }

            var state = await _ioLinkageDriver.ReadIoPointAsync(bitNumber);

            return Ok(new
            {
                bitNumber,
                state,
                level = state ? "High" : "Low"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 IO 点状态失败: BitNumber={BitNumber}", bitNumber);
            return StatusCode(500, new { message = "查询 IO 点状态失败" });
        }
    }

    /// <summary>
    /// 批量查询多个 IO 点的状态
    /// </summary>
    /// <param name="bitNumbers">IO 端口编号列表（逗号分隔），例如: "3,5,7"</param>
    /// <returns>IO 点状态列表</returns>
    /// <response code="200">查询成功</response>
    /// <response code="400">参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **功能说明**：
    /// 
    /// 一次性读取多个IO端口的当前电平状态，提高查询效率。
    /// 
    /// **参数格式**：
    /// - bitNumbers: 逗号分隔的IO端口号，例如 "3,5,7,10"
    /// - 每个端口号必须在 0-1023 范围内
    /// - 支持查询任意数量的IO点
    /// 
    /// **使用场景**：
    /// - 批量验证IO联动配置
    /// - 一次性查询相关IO组的状态
    /// - 监控仪表板数据采集
    /// 
    /// **示例**：
    /// ```
    /// GET /api/config/io-linkage/status/batch?bitNumbers=3,5,7
    /// 
    /// 响应：
    /// {
    ///   "count": 3,
    ///   "ioPoints": [
    ///     { "bitNumber": 3, "state": true, "level": "High" },
    ///     { "bitNumber": 5, "state": false, "level": "Low" },
    ///     { "bitNumber": 7, "state": true, "level": "High" }
    ///   ]
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 返回顺序与请求顺序一致
    /// - 任一端口号无效将导致整个请求失败
    /// - 读取操作不影响IO输出状态
    /// </remarks>
    [HttpGet("status/batch")]
    [SwaggerOperation(
        Summary = "批量查询IO点状态",
        Description = "读取多个IO端口的当前电平状态",
        OperationId = "GetBatchIoPointStatus",
        Tags = new[] { "IO联动配置" }
    )]
    [SwaggerResponse(200, "查询成功", typeof(object))]
    [SwaggerResponse(400, "参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetBatchIoPointStatus([FromQuery] string bitNumbers)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bitNumbers))
            {
                return BadRequest(new { message = "bitNumbers 参数不能为空" });
            }

            var bits = bitNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Select(s => int.TryParse(s, out var bit) ? bit : IoEndpointValidator.InvalidEndpoint)
                .ToList();

            // 验证所有 IO 点编号
            var invalidBits = bits.Where(b => !IoEndpointValidator.IsValidEndpoint(b)).ToList();
            if (invalidBits.Count > 0)
            {
                _logger.LogWarning(
                    "批量查询包含 {Count} 个无效 IO 端点: [{InvalidEndpoints}]",
                    invalidBits.Count,
                    string.Join(", ", invalidBits));
                return BadRequest(new
                {
                    message = $"IO 端口编号必须在 {IoEndpointValidator.MinValidEndpoint}-{IoEndpointValidator.MaxValidEndpoint} 之间",
                    invalidEndpoints = invalidBits
                });
            }

            var results = new List<object>();
            foreach (var bitNumber in bits)
            {
                var state = await _ioLinkageDriver.ReadIoPointAsync(bitNumber);
                results.Add(new
                {
                    bitNumber,
                    state,
                    level = state ? "High" : "Low"
                });
            }

            return Ok(new
            {
                count = results.Count,
                ioPoints = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量查询 IO 点状态失败");
            return StatusCode(500, new { message = "批量查询 IO 点状态失败" });
        }
    }

    /// <summary>
    /// 设置指定 IO 点的电平状态
    /// </summary>
    /// <param name="bitNumber">IO 端口编号（0-1023）</param>
    /// <param name="request">IO 点设置请求，指定目标电平</param>
    /// <returns>设置结果</returns>
    /// <response code="200">设置成功</response>
    /// <response code="400">参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **功能说明**：
    /// 
    /// 手动控制单个IO端口的输出电平，用于测试或特殊场景控制。
    /// 
    /// **IO点标识**：
    /// - BitNumber: IO端口编号，范围 0-1023
    /// - Level: 目标电平（ActiveHigh=输出1，ActiveLow=输出0）
    /// 
    /// **电平语义**：
    /// - ActiveHigh: 高电平有效，输出1（设备工作）
    /// - ActiveLow: 低电平有效，输出0（设备工作）
    /// 
    /// **使用场景**：
    /// - 测试IO硬件连接是否正常
    /// - 手动控制外部设备开关
    /// - 调试IO联动配置
    /// - 应急情况下的手动干预
    /// 
    /// **示例**：
    /// ```
    /// PUT /api/config/io-linkage/set/3
    /// {
    ///   "level": "ActiveHigh"
    /// }
    /// 
    /// 响应：
    /// {
    ///   "message": "IO 点设置成功",
    ///   "bitNumber": 3,
    ///   "level": "ActiveHigh"
    /// }
    /// ```
    /// 
    /// **⚠️ 重要警告**：
    /// - 此操作直接控制物理IO输出，可能影响连接的设备
    /// - 生产环境请谨慎使用，建议先在仿真环境测试
    /// - 手动设置可能覆盖系统自动联动的输出
    /// - 设置后的状态将持续，直到系统状态改变触发新的联动
    /// </remarks>
    [HttpPut("set/{bitNumber}")]
    [SwaggerOperation(
        Summary = "设置指定IO点电平",
        Description = "设置单个IO端口的电平状态（高电平或低电平）",
        OperationId = "SetIoPoint",
        Tags = new[] { "IO联动配置" }
    )]
    [SwaggerResponse(200, "设置成功", typeof(object))]
    [SwaggerResponse(400, "参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> SetIoPoint(int bitNumber, [FromBody] SetIoPointRequest request)
    {
        try
        {
            // 验证 IO 端点有效性
            if (!IoEndpointValidator.IsValidEndpoint(bitNumber))
            {
                var errorMsg = IoEndpointValidator.GetValidationError(bitNumber);
                _logger.LogWarning("尝试设置无效 IO 端点: BitNumber={BitNumber}, Error={Error}", bitNumber, errorMsg);
                return BadRequest(new { message = errorMsg });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效", errors });
            }

            var ioPoint = new IoLinkagePoint
            {
                BitNumber = bitNumber,
                Level = request.Level
            };

            await _ioLinkageDriver.SetIoPointAsync(ioPoint);

            _logger.LogInformation(
                "IO 点设置成功: BitNumber={BitNumber}, Level={Level}",
                bitNumber,
                request.Level);

            return Ok(new
            {
                message = "IO 点设置成功",
                bitNumber,
                level = request.Level.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置 IO 点失败: BitNumber={BitNumber}", bitNumber);
            return StatusCode(500, new { message = "设置 IO 点失败" });
        }
    }

    /// <summary>
    /// 批量设置多个 IO 点的电平状态
    /// </summary>
    /// <param name="request">批量 IO 点设置请求，包含IO点配置列表</param>
    /// <returns>设置结果</returns>
    /// <response code="200">设置成功</response>
    /// <response code="400">参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **功能说明**：
    /// 
    /// 一次性设置多个IO端口的输出电平，适用于需要同时控制多个设备的场景。
    /// 
    /// **请求格式**：
    /// - ioPoints: IO点配置数组，每个元素包含 bitNumber 和 level
    /// - 所有IO点在同一操作中批量设置，提高效率
    /// 
    /// **电平语义**：
    /// - ActiveHigh: 高电平有效，输出1
    /// - ActiveLow: 低电平有效，输出0
    /// 
    /// **使用场景**：
    /// - 批量测试多个IO端口
    /// - 同时控制一组相关设备
    /// - 快速应用预定义的IO配置
    /// - 测试IO联动配置的完整场景
    /// 
    /// **示例**：
    /// ```
    /// PUT /api/config/io-linkage/set/batch
    /// {
    ///   "ioPoints": [
    ///     { "bitNumber": 3, "level": "ActiveHigh" },
    ///     { "bitNumber": 5, "level": "ActiveLow" },
    ///     { "bitNumber": 7, "level": "ActiveHigh" }
    ///   ]
    /// }
    /// 
    /// 响应：
    /// {
    ///   "message": "批量设置 IO 点成功",
    ///   "count": 3,
    ///   "ioPoints": [
    ///     { "bitNumber": 3, "level": "ActiveHigh" },
    ///     { "bitNumber": 5, "level": "ActiveLow" },
    ///     { "bitNumber": 7, "level": "ActiveHigh" }
    ///   ]
    /// }
    /// ```
    /// 
    /// **⚠️ 重要警告**：
    /// - 此操作直接控制物理IO输出，可能影响多个设备
    /// - 所有IO点设置操作是原子性的（全部成功或全部失败）
    /// - 生产环境请谨慎使用，建议先在仿真环境测试
    /// - 批量设置可能覆盖系统自动联动的输出
    /// - 确保IO点配置正确，错误配置可能导致设备异常
    /// </remarks>
    [HttpPut("set/batch")]
    [SwaggerOperation(
        Summary = "批量设置IO点电平",
        Description = "批量设置多个IO端口的电平状态",
        OperationId = "SetBatchIoPoints",
        Tags = new[] { "IO联动配置" }
    )]
    [SwaggerResponse(200, "设置成功", typeof(object))]
    [SwaggerResponse(400, "参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> SetBatchIoPoints([FromBody] SetBatchIoPointsRequest request)
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

            if (request.IoPoints == null || request.IoPoints.Count == 0)
            {
                return BadRequest(new { message = "ioPoints 不能为空" });
            }

            // 验证所有 IO 点编号并过滤无效端点
            var allPoints = request.IoPoints
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList();

            var validPoints = IoEndpointValidator.FilterAndLogInvalidEndpoints(allPoints, _logger).ToList();

            if (validPoints.Count == 0)
            {
                return BadRequest(new
                {
                    message = "所有 IO 端点均无效，未执行任何操作",
                    validRange = $"{IoEndpointValidator.MinValidEndpoint}-{IoEndpointValidator.MaxValidEndpoint}"
                });
            }

            await _ioLinkageDriver.SetIoPointsAsync(validPoints);

            _logger.LogInformation(
                "批量设置 IO 点成功: TotalRequested={TotalRequested}, ValidCount={ValidCount}",
                request.IoPoints.Count,
                validPoints.Count);

            return Ok(new
            {
                message = "批量设置 IO 点成功",
                totalRequested = request.IoPoints.Count,
                validCount = validPoints.Count,
                skippedCount = request.IoPoints.Count - validPoints.Count,
                ioPoints = validPoints.Select(p => new
                {
                    bitNumber = p.BitNumber,
                    level = p.Level.ToString()
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量设置 IO 点失败");
            return StatusCode(500, new { message = "批量设置 IO 点失败" });
        }
    }

    /// <summary>
    /// 将请求模型映射到域配置模型
    /// </summary>
    private IoLinkageConfiguration MapToConfiguration(IoLinkageConfigRequest request)
    {
        return new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = request.Enabled,
            RunningStateIos = request.RunningStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            StoppedStateIos = request.StoppedStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            EmergencyStopStateIos = request.EmergencyStopStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            UpstreamConnectionExceptionStateIos = request.UpstreamConnectionExceptionStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            DiverterExceptionStateIos = request.DiverterExceptionStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            PostPreStartWarningStateIos = request.PostPreStartWarningStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            WheelDiverterDisconnectedStateIos = request.WheelDiverterDisconnectedStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            CreatedAt = _systemClock.LocalNow,
            UpdatedAt = _systemClock.LocalNow
        };
    }

    /// <summary>
    /// 将域配置模型映射到响应模型
    /// </summary>
    private IoLinkageConfigResponse MapToResponse(IoLinkageConfiguration config)
    {
        return new IoLinkageConfigResponse
        {
            Enabled = config.Enabled,
            RunningStateIos = config.RunningStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList(),
            StoppedStateIos = config.StoppedStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList(),
            EmergencyStopStateIos = config.EmergencyStopStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList(),
            UpstreamConnectionExceptionStateIos = config.UpstreamConnectionExceptionStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList(),
            DiverterExceptionStateIos = config.DiverterExceptionStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList(),
            PostPreStartWarningStateIos = config.PostPreStartWarningStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList(),
            WheelDiverterDisconnectedStateIos = config.WheelDiverterDisconnectedStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList()
        };
    }

    /// <summary>
    /// 将域配置模型转换为 IoLinkageOptions（用于协调器）
    /// </summary>
    private IoLinkageOptions ConvertToOptions(IoLinkageConfiguration config)
    {
        return new IoLinkageOptions
        {
            Enabled = config.Enabled,
            RunningStateIos = config.RunningStateIos.ToList(),
            StoppedStateIos = config.StoppedStateIos.ToList(),
            EmergencyStopStateIos = config.EmergencyStopStateIos.ToList(),
            UpstreamConnectionExceptionStateIos = config.UpstreamConnectionExceptionStateIos.ToList(),
            DiverterExceptionStateIos = config.DiverterExceptionStateIos.ToList(),
            PostPreStartWarningStateIos = config.PostPreStartWarningStateIos.ToList(),
            WheelDiverterDisconnectedStateIos = config.WheelDiverterDisconnectedStateIos.ToList()
        };
    }
}

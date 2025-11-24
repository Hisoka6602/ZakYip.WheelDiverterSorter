using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 面板配置管理 API 控制器
/// </summary>
/// <remarks>
/// 提供电柜操作面板的配置查询和更新功能。
/// 
/// 面板配置包括：
/// - 按钮映射和行为设置
/// - 信号塔指示灯行为
/// - 轮询和防抖参数
/// - 仿真/硬件模式切换
/// 
/// 配置更新立即生效，无需重启服务。
/// 配置通过LiteDB持久化存储，服务重启后配置保持不变。
/// </remarks>
[ApiController]
[Route("api/config/panel")]
[Produces("application/json")]
public class PanelConfigController : ControllerBase
{
    private readonly ILogger<PanelConfigController> _logger;
    private readonly IPanelConfigurationRepository _repository;
    private readonly ISystemClock _clock;

    public PanelConfigController(
        ILogger<PanelConfigController> logger,
        IPanelConfigurationRepository repository,
        ISystemClock clock)
    {
        _logger = logger;
        _repository = repository;
        _clock = clock;
    }

    /// <summary>
    /// 获取当前面板配置
    /// </summary>
    /// <returns>面板配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前的面板配置，包括：
    /// - 是否启用面板功能
    /// - 是否使用仿真模式
    /// - 按钮轮询间隔
    /// - 按钮防抖时间
    /// - IO 绑定配置（按钮输入位、指示灯输出位）
    /// - IO 电平配置（高电平有效/低电平有效）
    /// 
    /// 示例响应：
    /// ```json
    /// {
    ///   "enabled": true,
    ///   "useSimulation": true,
    ///   "pollingIntervalMs": 100,
    ///   "debounceMs": 50,
    ///   "startButtonInputBit": 0,
    ///   "startButtonTriggerLevel": "ActiveHigh",
    ///   "stopButtonInputBit": 1,
    ///   "stopButtonTriggerLevel": "ActiveHigh",
    ///   "emergencyStopButtonInputBit": 2,
    ///   "emergencyStopButtonTriggerLevel": "ActiveHigh",
    ///   "startLightOutputBit": 0,
    ///   "startLightOutputLevel": "ActiveHigh",
    ///   "stopLightOutputBit": 1,
    ///   "stopLightOutputLevel": "ActiveHigh",
    ///   "connectionLightOutputBit": 2,
    ///   "connectionLightOutputLevel": "ActiveHigh",
    ///   "signalTowerRedOutputBit": 3,
    ///   "signalTowerRedOutputLevel": "ActiveHigh",
    ///   "signalTowerYellowOutputBit": 4,
    ///   "signalTowerYellowOutputLevel": "ActiveHigh",
    ///   "signalTowerGreenOutputBit": 5,
    ///   "signalTowerGreenOutputLevel": "ActiveHigh"
    /// }
    /// ```
    /// 
    /// **字段说明**：
    /// - **enabled**: 是否启用面板功能
    /// - **useSimulation**: true 表示使用仿真驱动，false 表示使用真实硬件
    /// - **pollingIntervalMs**: 按钮状态轮询间隔（毫秒）
    /// - **debounceMs**: 按钮防抖时间（毫秒），防止误触
    /// - **xxxInputBit**: 输入 IO 位地址（0-1023）
    /// - **xxxOutputBit**: 输出 IO 位地址（0-1023）
    /// - **xxxTriggerLevel / xxxOutputLevel**: IO 电平配置
    ///   - ActiveHigh: 高电平有效（常开按键/输出1点亮）
    ///   - ActiveLow: 低电平有效（常闭按键/输出0点亮）
    /// </remarks>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取面板配置",
        Description = "返回当前电柜操作面板的配置参数",
        OperationId = "GetPanelConfig",
        Tags = new[] { "面板配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(PanelConfigResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(PanelConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<PanelConfigResponse> GetPanelConfig()
    {
        try
        {
            _logger.LogInformation("查询面板配置");
            var config = _repository.Get();
            return Ok(MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取面板配置失败");
            return StatusCode(500, new { message = "获取面板配置失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 更新面板配置
    /// </summary>
    /// <param name="request">面板配置请求</param>
    /// <returns>更新后的面板配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 更新面板配置，配置立即生效。
    /// 
    /// 示例请求：
    /// ```json
    /// {
    ///   "enabled": true,
    ///   "useSimulation": true,
    ///   "pollingIntervalMs": 100,
    ///   "debounceMs": 50
    /// }
    /// ```
    /// 
    /// 参数说明：
    /// - **enabled**: 是否启用面板功能
    /// - **useSimulation**: 是否使用仿真模式（true=仿真，false=硬件）
    /// - **pollingIntervalMs**: 按钮轮询间隔（50-1000毫秒）
    /// - **debounceMs**: 按钮防抖时间（10-500毫秒）
    /// 
    /// 注意事项：
    /// - 轮询间隔不宜过小，避免CPU占用过高
    /// - 防抖时间应根据实际按钮特性调整
    /// - 切换仿真/硬件模式可能需要重启服务才能完全生效
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新面板配置",
        Description = "更新电柜操作面板的配置参数，配置立即生效",
        OperationId = "UpdatePanelConfig",
        Tags = new[] { "面板配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(PanelConfigResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(PanelConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<PanelConfigResponse> UpdatePanelConfig([FromBody] PanelConfigRequest request)
    {
        try
        {
            // 参数验证
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();  // 物化集合避免多次枚举
                return BadRequest(new { message = "请求参数无效", errors });
            }

            // 业务规则验证
            if (request.PollingIntervalMs < 50 || request.PollingIntervalMs > 1000)
            {
                return BadRequest(new { message = "轮询间隔必须在 50-1000 毫秒之间" });
            }

            if (request.DebounceMs < 10 || request.DebounceMs > 500)
            {
                return BadRequest(new { message = "防抖时间必须在 10-500 毫秒之间" });
            }

            if (request.DebounceMs >= request.PollingIntervalMs)
            {
                return BadRequest(new { message = "防抖时间必须小于轮询间隔" });
            }

            // 从请求映射到域模型
            var currentTime = _clock.LocalNow;
            var config = MapToConfiguration(request, currentTime);
            
            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            // 持久化保存
            _repository.Update(config);

            _logger.LogInformation(
                "面板配置已更新并持久化: Enabled={Enabled}, UseSimulation={UseSimulation}, Polling={PollingMs}ms, Debounce={DebounceMs}ms",
                request.Enabled,
                request.UseSimulation,
                request.PollingIntervalMs,
                request.DebounceMs);

            // 重新读取并返回
            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新面板配置失败");
            return StatusCode(500, new { message = "更新面板配置失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 重置面板配置为默认值
    /// </summary>
    /// <returns>重置后的面板配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 将面板配置重置为系统默认值：
    /// - 启用状态：false
    /// - 仿真模式：true
    /// - 轮询间隔：100ms
    /// - 防抖时间：50ms
    /// </remarks>
    [HttpPost("reset")]
    [SwaggerOperation(
        Summary = "重置面板配置",
        Description = "将面板配置重置为系统默认值",
        OperationId = "ResetPanelConfig",
        Tags = new[] { "面板配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(PanelConfigResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(PanelConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<PanelConfigResponse> ResetPanelConfig()
    {
        try
        {
            var currentTime = _clock.LocalNow;
            var defaultConfig = PanelConfiguration.GetDefault() with
            {
                CreatedAt = currentTime,
                UpdatedAt = currentTime
            };
            
            _repository.Update(defaultConfig);
            
            _logger.LogInformation("面板配置已重置为默认值");
            
            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置面板配置失败");
            return StatusCode(500, new { message = "重置面板配置失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 将请求模型映射到域配置模型
    /// </summary>
    private static PanelConfiguration MapToConfiguration(PanelConfigRequest request, DateTime currentTime)
    {
        return new PanelConfiguration
        {
            ConfigName = "panel",
            Version = 1,
            Enabled = request.Enabled,
            UseSimulation = request.UseSimulation,
            PollingIntervalMs = request.PollingIntervalMs,
            DebounceMs = request.DebounceMs,
            StartButtonInputBit = request.StartButtonInputBit,
            StartButtonTriggerLevel = request.StartButtonTriggerLevel,
            StopButtonInputBit = request.StopButtonInputBit,
            StopButtonTriggerLevel = request.StopButtonTriggerLevel,
            EmergencyStopButtonInputBit = request.EmergencyStopButtonInputBit,
            EmergencyStopButtonTriggerLevel = request.EmergencyStopButtonTriggerLevel,
            StartLightOutputBit = request.StartLightOutputBit,
            StartLightOutputLevel = request.StartLightOutputLevel,
            StopLightOutputBit = request.StopLightOutputBit,
            StopLightOutputLevel = request.StopLightOutputLevel,
            ConnectionLightOutputBit = request.ConnectionLightOutputBit,
            ConnectionLightOutputLevel = request.ConnectionLightOutputLevel,
            SignalTowerRedOutputBit = request.SignalTowerRedOutputBit,
            SignalTowerRedOutputLevel = request.SignalTowerRedOutputLevel,
            SignalTowerYellowOutputBit = request.SignalTowerYellowOutputBit,
            SignalTowerYellowOutputLevel = request.SignalTowerYellowOutputLevel,
            SignalTowerGreenOutputBit = request.SignalTowerGreenOutputBit,
            SignalTowerGreenOutputLevel = request.SignalTowerGreenOutputLevel,
            CreatedAt = currentTime,
            UpdatedAt = currentTime
        };
    }

    /// <summary>
    /// 将域配置模型映射到响应模型
    /// </summary>
    private static PanelConfigResponse MapToResponse(PanelConfiguration config)
    {
        return new PanelConfigResponse
        {
            Enabled = config.Enabled,
            UseSimulation = config.UseSimulation,
            PollingIntervalMs = config.PollingIntervalMs,
            DebounceMs = config.DebounceMs,
            StartButtonInputBit = config.StartButtonInputBit,
            StartButtonTriggerLevel = config.StartButtonTriggerLevel,
            StopButtonInputBit = config.StopButtonInputBit,
            StopButtonTriggerLevel = config.StopButtonTriggerLevel,
            EmergencyStopButtonInputBit = config.EmergencyStopButtonInputBit,
            EmergencyStopButtonTriggerLevel = config.EmergencyStopButtonTriggerLevel,
            StartLightOutputBit = config.StartLightOutputBit,
            StartLightOutputLevel = config.StartLightOutputLevel,
            StopLightOutputBit = config.StopLightOutputBit,
            StopLightOutputLevel = config.StopLightOutputLevel,
            ConnectionLightOutputBit = config.ConnectionLightOutputBit,
            ConnectionLightOutputLevel = config.ConnectionLightOutputLevel,
            SignalTowerRedOutputBit = config.SignalTowerRedOutputBit,
            SignalTowerRedOutputLevel = config.SignalTowerRedOutputLevel,
            SignalTowerYellowOutputBit = config.SignalTowerYellowOutputBit,
            SignalTowerYellowOutputLevel = config.SignalTowerYellowOutputLevel,
            SignalTowerGreenOutputBit = config.SignalTowerGreenOutputBit,
            SignalTowerGreenOutputLevel = config.SignalTowerGreenOutputLevel
        };
    }
}

/// <summary>
/// 面板配置请求模型
/// </summary>
/// <remarks>
/// 用于更新面板配置的请求数据传输对象，包括面板按钮和指示灯的 IO 绑定配置
/// </remarks>
public sealed record PanelConfigRequest
{
    /// <summary>
    /// 是否启用面板功能
    /// </summary>
    /// <example>true</example>
    [Required]
    public required bool Enabled { get; init; }

    /// <summary>
    /// 是否使用仿真模式
    /// </summary>
    /// <remarks>
    /// true: 使用仿真驱动（SimulatedPanelInputReader / SimulatedSignalTowerOutput）
    /// false: 使用真实硬件驱动
    /// </remarks>
    /// <example>true</example>
    [Required]
    public required bool UseSimulation { get; init; }

    /// <summary>
    /// 面板按钮轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 有效范围：50-1000 毫秒
    /// 建议值：100 毫秒
    /// </remarks>
    /// <example>100</example>
    [Required]
    [Range(50, 1000, ErrorMessage = "轮询间隔必须在 50-1000 毫秒之间")]
    public required int PollingIntervalMs { get; init; }

    /// <summary>
    /// 按钮防抖时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 有效范围：10-500 毫秒
    /// 建议值：50 毫秒
    /// 在此时间内的重复触发将被忽略
    /// </remarks>
    /// <example>50</example>
    [Required]
    [Range(10, 500, ErrorMessage = "防抖时间必须在 10-500 毫秒之间")]
    public required int DebounceMs { get; init; }

    /// <summary>
    /// 开始按钮 IO 绑定（输入位）
    /// </summary>
    /// <remarks>
    /// 用于绑定启动按钮的输入 IO 地址
    /// 例如：0 表示第0个输入位
    /// </remarks>
    /// <example>0</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? StartButtonInputBit { get; init; }

    /// <summary>
    /// 开始按钮 IO 触发电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平有效（常开按键）
    /// - ActiveLow: 低电平有效（常闭按键）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StartButtonTriggerLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 停止按钮 IO 绑定（输入位）
    /// </summary>
    /// <remarks>
    /// 用于绑定停止按钮的输入 IO 地址
    /// </remarks>
    /// <example>1</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? StopButtonInputBit { get; init; }

    /// <summary>
    /// 停止按钮 IO 触发电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平有效（常开按键）
    /// - ActiveLow: 低电平有效（常闭按键）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StopButtonTriggerLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 急停按钮 IO 绑定（输入位）
    /// </summary>
    /// <remarks>
    /// 用于绑定急停按钮的输入 IO 地址
    /// </remarks>
    /// <example>2</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? EmergencyStopButtonInputBit { get; init; }

    /// <summary>
    /// 急停按钮 IO 触发电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平有效（常开按键）
    /// - ActiveLow: 低电平有效（常闭按键）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel EmergencyStopButtonTriggerLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 开始按钮灯 IO 绑定（输出位）
    /// </summary>
    /// <remarks>
    /// 用于绑定启动按钮指示灯的输出 IO 地址
    /// </remarks>
    /// <example>0</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? StartLightOutputBit { get; init; }

    /// <summary>
    /// 开始按钮灯 IO 输出电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StartLightOutputLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 停止按钮灯 IO 绑定（输出位）
    /// </summary>
    /// <remarks>
    /// 用于绑定停止按钮指示灯的输出 IO 地址
    /// </remarks>
    /// <example>1</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? StopLightOutputBit { get; init; }

    /// <summary>
    /// 停止按钮灯 IO 输出电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StopLightOutputLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 连接按钮灯 IO 绑定（输出位）
    /// </summary>
    /// <remarks>
    /// 用于绑定连接状态指示灯的输出 IO 地址
    /// </remarks>
    /// <example>2</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? ConnectionLightOutputBit { get; init; }

    /// <summary>
    /// 连接按钮灯 IO 输出电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel ConnectionLightOutputLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 三色灯红色 IO 绑定（输出位）
    /// </summary>
    /// <remarks>
    /// 用于绑定三色信号塔红色灯的输出 IO 地址
    /// </remarks>
    /// <example>3</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? SignalTowerRedOutputBit { get; init; }

    /// <summary>
    /// 三色灯红色 IO 输出电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel SignalTowerRedOutputLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 三色灯黄色 IO 绑定（输出位）
    /// </summary>
    /// <remarks>
    /// 用于绑定三色信号塔黄色灯的输出 IO 地址
    /// </remarks>
    /// <example>4</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? SignalTowerYellowOutputBit { get; init; }

    /// <summary>
    /// 三色灯黄色 IO 输出电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel SignalTowerYellowOutputLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;

    /// <summary>
    /// 三色灯绿色 IO 绑定（输出位）
    /// </summary>
    /// <remarks>
    /// 用于绑定三色信号塔绿色灯的输出 IO 地址
    /// </remarks>
    /// <example>5</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? SignalTowerGreenOutputBit { get; init; }

    /// <summary>
    /// 三色灯绿色 IO 输出电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel SignalTowerGreenOutputLevel { get; init; } = 
        ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel.ActiveHigh;
}

/// <summary>
/// 面板配置响应模型
/// </summary>
/// <remarks>
/// 面板配置的数据传输对象，用于查询返回，包括面板按钮和指示灯的 IO 绑定配置
/// </remarks>
public sealed record PanelConfigResponse
{
    /// <summary>
    /// 是否启用面板功能
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// 是否使用仿真模式
    /// </summary>
    public required bool UseSimulation { get; init; }

    /// <summary>
    /// 面板按钮轮询间隔（毫秒）
    /// </summary>
    public required int PollingIntervalMs { get; init; }

    /// <summary>
    /// 按钮防抖时间（毫秒）
    /// </summary>
    public required int DebounceMs { get; init; }

    /// <summary>
    /// 开始按钮 IO 绑定（输入位）
    /// </summary>
    public int? StartButtonInputBit { get; init; }

    /// <summary>
    /// 开始按钮 IO 触发电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StartButtonTriggerLevel { get; init; }

    /// <summary>
    /// 停止按钮 IO 绑定（输入位）
    /// </summary>
    public int? StopButtonInputBit { get; init; }

    /// <summary>
    /// 停止按钮 IO 触发电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StopButtonTriggerLevel { get; init; }

    /// <summary>
    /// 急停按钮 IO 绑定（输入位）
    /// </summary>
    public int? EmergencyStopButtonInputBit { get; init; }

    /// <summary>
    /// 急停按钮 IO 触发电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel EmergencyStopButtonTriggerLevel { get; init; }

    /// <summary>
    /// 开始按钮灯 IO 绑定（输出位）
    /// </summary>
    public int? StartLightOutputBit { get; init; }

    /// <summary>
    /// 开始按钮灯 IO 输出电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StartLightOutputLevel { get; init; }

    /// <summary>
    /// 停止按钮灯 IO 绑定（输出位）
    /// </summary>
    public int? StopLightOutputBit { get; init; }

    /// <summary>
    /// 停止按钮灯 IO 输出电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel StopLightOutputLevel { get; init; }

    /// <summary>
    /// 连接按钮灯 IO 绑定（输出位）
    /// </summary>
    public int? ConnectionLightOutputBit { get; init; }

    /// <summary>
    /// 连接按钮灯 IO 输出电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel ConnectionLightOutputLevel { get; init; }

    /// <summary>
    /// 三色灯红色 IO 绑定（输出位）
    /// </summary>
    public int? SignalTowerRedOutputBit { get; init; }

    /// <summary>
    /// 三色灯红色 IO 输出电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel SignalTowerRedOutputLevel { get; init; }

    /// <summary>
    /// 三色灯黄色 IO 绑定（输出位）
    /// </summary>
    public int? SignalTowerYellowOutputBit { get; init; }

    /// <summary>
    /// 三色灯黄色 IO 输出电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel SignalTowerYellowOutputLevel { get; init; }

    /// <summary>
    /// 三色灯绿色 IO 绑定（输出位）
    /// </summary>
    public int? SignalTowerGreenOutputBit { get; init; }

    /// <summary>
    /// 三色灯绿色 IO 输出电平配置
    /// </summary>
    public ZakYip.WheelDiverterSorter.Core.Enums.Sensors.TriggerLevel SignalTowerGreenOutputLevel { get; init; }
}

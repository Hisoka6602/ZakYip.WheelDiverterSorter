using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Host.Models.Panel;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

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
    /// 返回当前的面板配置，参数按功能分类组织：
    /// - 基础配置：enabled, useSimulation, pollingIntervalMs, debounceMs
    /// - 开始按钮：startButton (inputBit, inputTriggerLevel, lightOutputBit, lightOutputLevel)
    /// - 停止按钮：stopButton (inputBit, inputTriggerLevel, lightOutputBit, lightOutputLevel)
    /// - 急停按钮：emergencyStopButtons (数组，每项包含 inputBit, inputTriggerLevel)
    /// - 连接指示灯：connectionIndicator (outputBit, outputLevel)
    /// - 三色信号塔：signalTower (redOutputBit, redOutputLevel, yellowOutputBit, yellowOutputLevel, greenOutputBit, greenOutputLevel)
    /// - 运行前预警：preStartWarning (durationSeconds, outputBit, outputLevel)
    ///
    /// 示例响应：
    /// ```json
    /// {
    ///   "enabled": true,
    ///   "useSimulation": true,
    ///   "pollingIntervalMs": 100,
    ///   "debounceMs": 50,
    ///   "startButton": {
    ///     "inputBit": 0,
    ///     "inputTriggerLevel": "ActiveHigh",
    ///     "lightOutputBit": 0,
    ///     "lightOutputLevel": "ActiveHigh"
    ///   },
    ///   "stopButton": {
    ///     "inputBit": 1,
    ///     "inputTriggerLevel": "ActiveHigh",
    ///     "lightOutputBit": 1,
    ///     "lightOutputLevel": "ActiveHigh"
    ///   },
    ///   "emergencyStopButtons": [
    ///     {
    ///       "inputBit": 2,
    ///       "inputTriggerLevel": "ActiveHigh"
    ///     },
    ///     {
    ///       "inputBit": 3,
    ///       "inputTriggerLevel": "ActiveLow"
    ///     }
    ///   ],
    ///   "connectionIndicator": {
    ///     "outputBit": 2,
    ///     "outputLevel": "ActiveHigh"
    ///   },
    ///   "signalTower": {
    ///     "redOutputBit": 3,
    ///     "redOutputLevel": "ActiveHigh",
    ///     "yellowOutputBit": 4,
    ///     "yellowOutputLevel": "ActiveHigh",
    ///     "greenOutputBit": 5,
    ///     "greenOutputLevel": "ActiveHigh"
    ///   },
    ///   "preStartWarning": {
    ///     "durationSeconds": 5,
    ///     "outputBit": 6,
    ///     "outputLevel": "ActiveHigh"
    ///   }
    /// }
    /// ```
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
    /// 更新面板配置，参数按功能分类组织，配置立即生效。
    ///
    /// 示例请求：
    /// ```json
    /// {
    ///   "enabled": true,
    ///   "useSimulation": true,
    ///   "pollingIntervalMs": 100,
    ///   "debounceMs": 50,
    ///   "startButton": {
    ///     "inputBit": 0,
    ///     "inputTriggerLevel": "ActiveHigh",
    ///     "lightOutputBit": 0,
    ///     "lightOutputLevel": "ActiveHigh"
    ///   },
    ///   "stopButton": {
    ///     "inputBit": 1,
    ///     "inputTriggerLevel": "ActiveHigh",
    ///     "lightOutputBit": 1,
    ///     "lightOutputLevel": "ActiveHigh"
    ///   },
    ///   "signalTower": {
    ///     "redOutputBit": 3,
    ///     "redOutputLevel": "ActiveHigh",
    ///     "yellowOutputBit": 4,
    ///     "yellowOutputLevel": "ActiveHigh",
    ///     "greenOutputBit": 5,
    ///     "greenOutputLevel": "ActiveHigh"
    ///   }
    /// }
    /// ```
    ///
    /// 参数分组说明：
    /// - **基础配置**: enabled, useSimulation, pollingIntervalMs, debounceMs
    /// - **startButton**: 开始按钮的输入和指示灯配置
    /// - **stopButton**: 停止按钮的输入和指示灯配置
    /// - **emergencyStopButtons**: 急停按钮数组的输入配置（系统只有在所有急停按钮都解除时才视为解除急停状态）
    /// - **connectionIndicator**: 连接状态指示灯配置
    /// - **signalTower**: 三色信号塔配置
    /// - **preStartWarning**: 运行前预警配置
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
            StartButtonInputBit = request.StartButton?.InputBit,
            StartButtonTriggerLevel = request.StartButton?.InputTriggerLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            StopButtonInputBit = request.StopButton?.InputBit,
            StopButtonTriggerLevel = request.StopButton?.InputTriggerLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            EmergencyStopButtons = request.EmergencyStopButtons?.Select(esb => new EmergencyStopButtonConfig
            {
                InputBit = esb.InputBit ?? 0,
                InputTriggerLevel = esb.InputTriggerLevel
            }).ToList() ?? new List<EmergencyStopButtonConfig>(),
            StartLightOutputBit = request.StartButton?.LightOutputBit,
            StartLightOutputLevel = request.StartButton?.LightOutputLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            StopLightOutputBit = request.StopButton?.LightOutputBit,
            StopLightOutputLevel = request.StopButton?.LightOutputLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            ConnectionLightOutputBit = request.ConnectionIndicator?.OutputBit,
            ConnectionLightOutputLevel = request.ConnectionIndicator?.OutputLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            SignalTowerRedOutputBit = request.SignalTower?.RedOutputBit,
            SignalTowerRedOutputLevel = request.SignalTower?.RedOutputLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            SignalTowerYellowOutputBit = request.SignalTower?.YellowOutputBit,
            SignalTowerYellowOutputLevel = request.SignalTower?.YellowOutputLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            SignalTowerGreenOutputBit = request.SignalTower?.GreenOutputBit,
            SignalTowerGreenOutputLevel = request.SignalTower?.GreenOutputLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
            PreStartWarningDurationSeconds = request.PreStartWarning?.DurationSeconds,
            PreStartWarningOutputBit = request.PreStartWarning?.OutputBit,
            PreStartWarningOutputLevel = request.PreStartWarning?.OutputLevel ?? Core.Enums.Hardware.TriggerLevel.ActiveHigh,
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
            StartButton = new StartButtonConfigDto
            {
                InputBit = config.StartButtonInputBit,
                InputTriggerLevel = config.StartButtonTriggerLevel,
                LightOutputBit = config.StartLightOutputBit,
                LightOutputLevel = config.StartLightOutputLevel
            },
            StopButton = new StopButtonConfigDto
            {
                InputBit = config.StopButtonInputBit,
                InputTriggerLevel = config.StopButtonTriggerLevel,
                LightOutputBit = config.StopLightOutputBit,
                LightOutputLevel = config.StopLightOutputLevel
            },
            EmergencyStopButtons = config.EmergencyStopButtons.Select(esb => new EmergencyStopButtonConfigDto
            {
                InputBit = esb.InputBit,
                InputTriggerLevel = esb.InputTriggerLevel
            }).ToList(),
            ConnectionIndicator = new ConnectionIndicatorConfigDto
            {
                OutputBit = config.ConnectionLightOutputBit,
                OutputLevel = config.ConnectionLightOutputLevel
            },
            SignalTower = new SignalTowerConfigDto
            {
                RedOutputBit = config.SignalTowerRedOutputBit,
                RedOutputLevel = config.SignalTowerRedOutputLevel,
                YellowOutputBit = config.SignalTowerYellowOutputBit,
                YellowOutputLevel = config.SignalTowerYellowOutputLevel,
                GreenOutputBit = config.SignalTowerGreenOutputBit,
                GreenOutputLevel = config.SignalTowerGreenOutputLevel
            },
            PreStartWarning = new PreStartWarningConfigDto
            {
                DurationSeconds = config.PreStartWarningDurationSeconds,
                OutputBit = config.PreStartWarningOutputBit,
                OutputLevel = config.PreStartWarningOutputLevel
            }
        };
    }
}
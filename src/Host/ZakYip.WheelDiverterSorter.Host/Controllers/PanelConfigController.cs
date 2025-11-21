using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;

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
/// 
/// **注意**：当前实现使用内存存储，重启后配置会丢失。
/// 未来版本将实现持久化存储（通过 IPanelConfigurationRepository）。
/// </remarks>
[ApiController]
[Route("api/config/panel")]
[Produces("application/json")]
public class PanelConfigController : ControllerBase
{
    private readonly ILogger<PanelConfigController> _logger;
    // TODO: 需要添加持久化层 - IPanelConfigurationRepository
    // 使用 ConcurrentDictionary 确保线程安全的配置存储
    private static readonly ConcurrentDictionary<string, PanelConfigResponse> _configStore = 
        new(new[] { new KeyValuePair<string, PanelConfigResponse>("current", GetDefaultConfig()) });

    public PanelConfigController(ILogger<PanelConfigController> logger)
    {
        _logger = logger;
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
    /// 
    /// 示例响应：
    /// ```json
    /// {
    ///   "enabled": true,
    ///   "useSimulation": true,
    ///   "pollingIntervalMs": 100,
    ///   "debounceMs": 50
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
            return Ok(_configStore["current"]);
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

            // 创建新配置对象
            var newConfig = new PanelConfigResponse
            {
                Enabled = request.Enabled,
                UseSimulation = request.UseSimulation,
                PollingIntervalMs = request.PollingIntervalMs,
                DebounceMs = request.DebounceMs
            };

            // 原子更新配置（线程安全）
            _configStore["current"] = newConfig;

            _logger.LogInformation(
                "面板配置已更新: Enabled={Enabled}, UseSimulation={UseSimulation}, Polling={PollingMs}ms, Debounce={DebounceMs}ms",
                request.Enabled,
                request.UseSimulation,
                request.PollingIntervalMs,
                request.DebounceMs);

            return Ok(newConfig);
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
            var defaultConfig = GetDefaultConfig();
            // 原子更新配置（线程安全）
            _configStore["current"] = defaultConfig;
            
            _logger.LogInformation("面板配置已重置为默认值");
            return Ok(defaultConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置面板配置失败");
            return StatusCode(500, new { message = "重置面板配置失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取面板配置模板
    /// </summary>
    /// <returns>默认配置模板</returns>
    /// <response code="200">成功返回模板</response>
    /// <remarks>
    /// 返回面板配置的默认模板，可用作参考或初始配置。
    /// </remarks>
    [HttpGet("template")]
    [SwaggerOperation(
        Summary = "获取面板配置模板",
        Description = "返回面板配置的默认模板",
        OperationId = "GetPanelConfigTemplate",
        Tags = new[] { "面板配置" }
    )]
    [SwaggerResponse(200, "成功返回模板", typeof(PanelConfigRequest))]
    [ProducesResponseType(typeof(PanelConfigRequest), 200)]
    public ActionResult<PanelConfigRequest> GetPanelConfigTemplate()
    {
        var defaultConfig = GetDefaultConfig();
        return Ok(new PanelConfigRequest
        {
            Enabled = defaultConfig.Enabled,
            UseSimulation = defaultConfig.UseSimulation,
            PollingIntervalMs = defaultConfig.PollingIntervalMs,
            DebounceMs = defaultConfig.DebounceMs
        });
    }

    private static PanelConfigResponse GetDefaultConfig()
    {
        return new PanelConfigResponse
        {
            Enabled = false,
            UseSimulation = true,
            PollingIntervalMs = 100,
            DebounceMs = 50
        };
    }
}

/// <summary>
/// 面板配置请求模型
/// </summary>
/// <remarks>
/// 用于更新面板配置的请求数据传输对象
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
}

/// <summary>
/// 面板配置响应模型
/// </summary>
/// <remarks>
/// 面板配置的数据传输对象，用于查询返回
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
}

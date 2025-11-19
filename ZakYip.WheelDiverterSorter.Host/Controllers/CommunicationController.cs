using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 通信配置和监控API控制器 - Communication Configuration and Monitoring API Controller
/// </summary>
/// <remarks>
/// 提供通信配置管理、连接测试和状态监控功能
/// </remarks>
[ApiController]
[Route("api/communication")]
[Produces("application/json")]
public class CommunicationController : ControllerBase {
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly IOptions<RuleEngineConnectionOptions> _connectionOptions;
    private readonly ICommunicationConfigurationRepository _configRepository;
    private readonly CommunicationStatsService _statsService;
    private readonly ILogger<CommunicationController> _logger;

    public CommunicationController(
        IRuleEngineClient ruleEngineClient,
        IOptions<RuleEngineConnectionOptions> connectionOptions,
        ICommunicationConfigurationRepository configRepository,
        CommunicationStatsService statsService,
        ILogger<CommunicationController> logger) {
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _connectionOptions = connectionOptions ?? throw new ArgumentNullException(nameof(connectionOptions));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取当前通信配置 - Get current communication configuration
    /// </summary>
    /// <returns>通信配置信息 - Communication configuration</returns>
    /// <response code="200">成功返回配置 - Successfully returned configuration</response>
    /// <response code="500">服务器内部错误 - Internal server error</response>
    [HttpGet("config")]
    [ProducesResponseType(typeof(RuleEngineConnectionOptions), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<RuleEngineConnectionOptions> GetConfiguration() {
        try {
            var config = _connectionOptions.Value;
            return Ok(config);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "获取通信配置失败 - Failed to get communication configuration");
            return StatusCode(500, new { message = "获取通信配置失败 - Failed to get communication configuration" });
        }
    }

    /// <summary>
    /// 测试连接 - Test connection
    /// </summary>
    /// <returns>连接测试结果 - Connection test result</returns>
    /// <response code="200">测试完成（包括成功和失败） - Test completed (includes success and failure)</response>
    /// <response code="500">服务器内部错误 - Internal server error</response>
    /// <remarks>
    /// 尝试连接到配置的RuleEngine服务器并返回测试结果
    /// </remarks>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ConnectionTestResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<ConnectionTestResponse>> TestConnection(CancellationToken cancellationToken = default) {
        var sw = Stopwatch.StartNew();
        var testedAt = DateTimeOffset.UtcNow;

        try {
            _logger.LogInformation("开始测试通信连接 - Starting communication connection test");

            var connected = await _ruleEngineClient.ConnectAsync(cancellationToken);
            sw.Stop();

            if (connected) {
                _logger.LogInformation("连接测试成功，响应时间: {ResponseTimeMs}ms - Connection test succeeded, response time: {ResponseTimeMs}ms",
                    sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);

                return Ok(new ConnectionTestResponse {
                    Success = true,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Message = "连接成功 - Connection successful",
                    TestedAt = testedAt
                });
            }
            else {
                _logger.LogWarning("连接测试失败 - Connection test failed");

                return Ok(new ConnectionTestResponse {
                    Success = false,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Message = "连接失败 - Connection failed",
                    ErrorDetails = "无法建立连接 - Unable to establish connection",
                    TestedAt = testedAt
                });
            }
        }
        catch (Exception ex) {
            sw.Stop();
            _logger.LogError(ex, "连接测试时发生异常 - Exception occurred during connection test");

            return Ok(new ConnectionTestResponse {
                Success = false,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = "连接测试失败 - Connection test failed",
                ErrorDetails = ex.Message,
                TestedAt = testedAt
            });
        }
    }

    /// <summary>
    /// 获取当前通信状态 - Get current communication status
    /// </summary>
    /// <returns>通信状态信息（包含收发计数器） - Communication status (includes send/receive counters)</returns>
    /// <response code="200">成功返回状态 - Successfully returned status</response>
    /// <response code="500">服务器内部错误 - Internal server error</response>
    /// <remarks>
    /// 返回连接状态、消息收发次数等统计信息
    /// </remarks>
    [HttpGet("status")]
    [ProducesResponseType(typeof(CommunicationStatusResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationStatusResponse> GetStatus() {
        try {
            var config = _connectionOptions.Value;
            var isConnected = _ruleEngineClient.IsConnected;

            string? serverAddress = config.Mode switch {
                CommunicationMode.Tcp => config.TcpServer,
                CommunicationMode.Http => config.HttpApi,
                CommunicationMode.Mqtt => config.MqttBroker,
                CommunicationMode.SignalR => config.SignalRHub,
                _ => null
            };

            var response = new CommunicationStatusResponse {
                Mode = config.Mode.ToString(),
                IsConnected = isConnected,
                MessagesSent = _statsService.MessagesSent,
                MessagesReceived = _statsService.MessagesReceived,
                ConnectionDurationSeconds = _statsService.ConnectionDurationSeconds,
                LastConnectedAt = _statsService.LastConnectedAt,
                LastDisconnectedAt = _statsService.LastDisconnectedAt,
                ServerAddress = serverAddress,
                ErrorMessage = isConnected ? null : "当前未连接 - Currently not connected"
            };

            return Ok(response);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "获取通信状态失败 - Failed to get communication status");
            return StatusCode(500, new { message = "获取通信状态失败 - Failed to get communication status" });
        }
    }

    /// <summary>
    /// 重置统计计数器 - Reset statistics counters
    /// </summary>
    /// <returns>操作结果 - Operation result</returns>
    /// <response code="200">重置成功 - Reset successful</response>
    /// <response code="500">服务器内部错误 - Internal server error</response>
    /// <remarks>
    /// 将消息收发计数器重置为零
    /// </remarks>
    [HttpPost("reset-stats")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult ResetStatistics() {
        try {
            _statsService.Reset();
            _logger.LogInformation("通信统计已重置 - Communication statistics reset");
            return Ok(new { message = "统计计数器已重置 - Statistics counters reset", resetAt = DateTimeOffset.UtcNow });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "重置统计失败 - Failed to reset statistics");
            return StatusCode(500, new { message = "重置统计失败 - Failed to reset statistics" });
        }
    }

    /// <summary>
    /// 获取持久化通信配置 - Get persisted communication configuration
    /// </summary>
    /// <returns>通信配置信息 - Communication configuration</returns>
    /// <response code="200">成功返回配置 - Successfully returned configuration</response>
    /// <response code="500">服务器内部错误 - Internal server error</response>
    [HttpGet("config/persisted")]
    [ProducesResponseType(typeof(CommunicationConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfiguration> GetPersistedConfiguration() {
        try {
            var config = _configRepository.Get();
            return Ok(config);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "获取持久化通信配置失败 - Failed to get persisted communication configuration");
            return StatusCode(500, new { message = "获取持久化通信配置失败 - Failed to get persisted communication configuration" });
        }
    }

    /// <summary>
    /// 更新持久化通信配置（支持热更新）- Update persisted communication configuration (supports hot reload)
    /// </summary>
    /// <param name="request">通信配置请求 - Communication configuration request</param>
    /// <returns>更新后的通信配置 - Updated communication configuration</returns>
    /// <response code="200">更新成功 - Update successful</response>
    /// <response code="400">请求参数无效 - Invalid request parameters</response>
    /// <response code="500">服务器内部错误 - Internal server error</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     PUT /api/communication/config/persisted
    ///     {
    ///         "mode": 1,
    ///         "tcpServer": "192.168.1.100:8000",
    ///         "timeoutMs": 5000,
    ///         "retryCount": 3
    ///     }
    ///
    /// 通信模式: Http=0, Tcp=1, SignalR=2, Mqtt=3
    /// 配置更新后立即生效，无需重启服务
    /// </remarks>
    [HttpPut("config/persisted")]
    [ProducesResponseType(typeof(CommunicationConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfiguration> UpdatePersistedConfiguration([FromBody] CommunicationConfiguration request) {
        try {
            if (!ModelState.IsValid) {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效 - Invalid request parameters", errors });
            }

            // 验证配置
            var (isValid, errorMessage) = request.Validate();
            if (!isValid) {
                return BadRequest(new { message = errorMessage });
            }

            _configRepository.Update(request);

            _logger.LogInformation(
                "通信配置已更新 - Communication configuration updated: Mode={Mode}, Version={Version}",
                request.Mode,
                request.Version);

            var updatedConfig = _configRepository.Get();
            return Ok(updatedConfig);
        }
        catch (ArgumentException ex) {
            _logger.LogWarning(ex, "通信配置验证失败 - Communication configuration validation failed");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "更新通信配置失败 - Failed to update communication configuration");
            return StatusCode(500, new { message = "更新通信配置失败 - Failed to update communication configuration" });
        }
    }

    /// <summary>
    /// 重置持久化通信配置为默认值 - Reset persisted communication configuration to defaults
    /// </summary>
    /// <returns>重置后的通信配置 - Reset communication configuration</returns>
    /// <response code="200">重置成功 - Reset successful</response>
    /// <response code="500">服务器内部错误 - Internal server error</response>
    [HttpPost("config/persisted/reset")]
    [ProducesResponseType(typeof(CommunicationConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfiguration> ResetPersistedConfiguration() {
        try {
            var defaultConfig = CommunicationConfiguration.GetDefault();
            _configRepository.Update(defaultConfig);

            _logger.LogInformation("通信配置已重置为默认值 - Communication configuration reset to defaults");

            var updatedConfig = _configRepository.Get();
            return Ok(updatedConfig);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "重置通信配置失败 - Failed to reset communication configuration");
            return StatusCode(500, new { message = "重置通信配置失败 - Failed to reset communication configuration" });
        }
    }
}
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 通信配置和监控API控制器
/// </summary>
/// <remarks>
/// 提供与上游RuleEngine的通信配置管理、连接测试和状态监控功能。
/// 支持多种通信协议：TCP、HTTP、SignalR、MQTT。
/// 支持客户端/服务端模式和重试策略配置。
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
    private readonly ISystemClock _systemClock;

    public CommunicationController(
        IRuleEngineClient ruleEngineClient,
        IOptions<RuleEngineConnectionOptions> connectionOptions,
        ICommunicationConfigurationRepository configRepository,
        CommunicationStatsService statsService,
        ILogger<CommunicationController> logger,
        ISystemClock systemClock) {
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _connectionOptions = connectionOptions ?? throw new ArgumentNullException(nameof(connectionOptions));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <summary>
    /// 获取当前通信配置
    /// </summary>
    /// <returns>通信配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("config")]
    [SwaggerOperation(
        Summary = "获取当前通信配置",
        Description = "返回当前生效的RuleEngine通信配置，包括通信模式、服务器地址、超时时间等参数",
        OperationId = "GetConfiguration",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(RuleEngineConnectionOptions))]
    [SwaggerResponse(500, "服务器内部错误")]
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
    /// 测试与RuleEngine的连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌，用于取消长时间运行的连接测试操作</param>
    /// <returns>连接测试结果，包括是否成功、响应时间、错误详情等</returns>
    /// <response code="200">测试完成（返回成功或失败的详细信息）</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 尝试连接到配置的RuleEngine服务器并返回测试结果。
    /// 测试结果包括：
    /// - 连接是否成功
    /// - 响应时间（毫秒）
    /// - 错误详情（如果失败）
    /// - 测试时间戳
    /// 
    /// 注意：此操作可能需要较长时间完成，取决于网络状况和配置的超时时间。
    /// </remarks>
    [HttpPost("test")]
    [SwaggerOperation(
        Summary = "测试RuleEngine连接",
        Description = "测试与配置的RuleEngine服务器的连接，返回连接结果和响应时间",
        OperationId = "TestConnection",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "测试完成", typeof(ConnectionTestResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ConnectionTestResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<ConnectionTestResponse>> TestConnection(CancellationToken cancellationToken = default) {
        var sw = Stopwatch.StartNew();
        var testedAt = _systemClock.LocalNowOffset;

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
    /// 获取当前通信状态
    /// </summary>
    /// <returns>通信状态信息，包含收发计数器、连接时长等统计数据</returns>
    /// <response code="200">成功返回状态</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回通信连接的实时状态信息，包括：
    /// - 当前通信模式（TCP/HTTP/SignalR/MQTT）
    /// - 是否已连接
    /// - 消息发送/接收次数
    /// - 连接持续时间
    /// - 最后连接/断开时间
    /// - 服务器地址
    /// </remarks>
    [HttpGet("status")]
    [SwaggerOperation(
        Summary = "获取通信状态",
        Description = "返回与RuleEngine的连接状态和统计信息，包括消息收发次数、连接时长等",
        OperationId = "GetStatus",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "成功返回状态", typeof(CommunicationStatusResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
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
    /// 重置通信统计计数器
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 将消息收发计数器重置为零，用于开始新的统计周期。
    /// 不影响当前连接状态。
    /// </remarks>
    [HttpPost("reset-stats")]
    [SwaggerOperation(
        Summary = "重置统计计数器",
        Description = "重置通信消息收发统计计数器，用于开始新的统计周期",
        OperationId = "ResetStatistics",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(object))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult ResetStatistics() {
        try {
            _statsService.Reset();
            _logger.LogInformation("通信统计已重置 - Communication statistics reset");
            return Ok(new { message = "统计计数器已重置 - Statistics counters reset", resetAt = _systemClock.LocalNowOffset });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "重置统计失败 - Failed to reset statistics");
            return StatusCode(500, new { message = "重置统计失败 - Failed to reset statistics" });
        }
    }

    /// <summary>
    /// 获取持久化通信配置
    /// </summary>
    /// <returns>持久化的通信配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 获取存储在数据库中的通信配置，与内存中的运行时配置可能不同
    /// </remarks>
    [HttpGet("config/persisted")]
    [SwaggerOperation(
        Summary = "获取持久化通信配置",
        Description = "返回存储在数据库中的通信配置",
        OperationId = "GetPersistedConfiguration",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(CommunicationConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
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
    /// 更新持久化通信配置（支持热更新）
    /// </summary>
    /// <param name="request">通信配置请求，包含通信模式、服务器地址、超时时间等参数</param>
    /// <returns>更新后的通信配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
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
    /// 通信模式说明（mode字段）：
    /// - 0: Http - HTTP REST API通信
    /// - 1: Tcp - TCP Socket通信
    /// - 2: SignalR - SignalR实时通信
    /// - 3: Mqtt - MQTT消息队列通信
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// </remarks>
    [HttpPut("config/persisted")]
    [SwaggerOperation(
        Summary = "更新持久化通信配置",
        Description = "更新通信配置并持久化到数据库，配置立即生效无需重启",
        OperationId = "UpdatePersistedConfiguration",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(CommunicationConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
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
    /// 重置持久化通信配置为默认值
    /// </summary>
    /// <returns>重置后的通信配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 将通信配置恢复为系统默认值，立即生效
    /// </remarks>
    [HttpPost("config/persisted/reset")]
    [SwaggerOperation(
        Summary = "重置通信配置为默认值",
        Description = "将持久化的通信配置重置为系统默认值",
        OperationId = "ResetPersistedConfiguration",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(CommunicationConfiguration))]
    [SwaggerResponse(500, "服务器内部错误")]
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
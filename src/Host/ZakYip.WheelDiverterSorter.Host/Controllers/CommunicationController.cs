using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Utilities;

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
    /// 获取存储在数据库中的通信配置，与内存中的运行时配置可能不同。
    /// 配置参数按协议分组，便于理解和管理：
    /// - TCP配置：包括服务器地址、缓冲区大小、Keep-Alive等
    /// - HTTP配置：包括API地址、连接池设置、HTTP/2支持等
    /// - MQTT配置：包括Broker地址、主题、QoS等级等
    /// - SignalR配置：包括Hub地址、超时设置等
    /// </remarks>
    [HttpGet("config/persisted")]
    [SwaggerOperation(
        Summary = "获取持久化通信配置",
        Description = "返回存储在数据库中的通信配置，参数按协议分组显示",
        OperationId = "GetPersistedConfiguration",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(CommunicationConfigurationResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(CommunicationConfigurationResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfigurationResponse> GetPersistedConfiguration() {
        try {
            var config = _configRepository.Get();
            
            var response = new CommunicationConfigurationResponse
            {
                Mode = config.Mode,
                ConnectionMode = config.ConnectionMode,
                TimeoutMs = config.TimeoutMs,
                RetryCount = config.RetryCount,
                RetryDelayMs = config.RetryDelayMs,
                EnableAutoReconnect = config.EnableAutoReconnect,
                Tcp = new TcpConfigDto
                {
                    TcpServer = config.TcpServer,
                    ReceiveBufferSize = config.Tcp.ReceiveBufferSize,
                    SendBufferSize = config.Tcp.SendBufferSize,
                    NoDelay = config.Tcp.NoDelay,
                    KeepAliveInterval = config.Tcp.KeepAliveInterval
                },
                Http = new HttpConfigDto
                {
                    HttpApi = config.HttpApi,
                    MaxConnectionsPerServer = config.Http.MaxConnectionsPerServer,
                    PooledConnectionIdleTimeout = config.Http.PooledConnectionIdleTimeout,
                    PooledConnectionLifetime = config.Http.PooledConnectionLifetime,
                    UseHttp2 = config.Http.UseHttp2
                },
                Mqtt = new MqttConfigDto
                {
                    MqttBroker = config.MqttBroker,
                    MqttTopic = config.MqttTopic,
                    QualityOfServiceLevel = config.Mqtt.QualityOfServiceLevel,
                    CleanSession = config.Mqtt.CleanSession,
                    SessionExpiryInterval = config.Mqtt.SessionExpiryInterval,
                    MessageExpiryInterval = config.Mqtt.MessageExpiryInterval,
                    ClientIdPrefix = config.Mqtt.ClientIdPrefix
                },
                SignalR = new SignalRConfigDto
                {
                    SignalRHub = config.SignalRHub,
                    HandshakeTimeout = config.SignalR.HandshakeTimeout,
                    KeepAliveInterval = config.SignalR.KeepAliveInterval,
                    ServerTimeout = config.SignalR.ServerTimeout,
                    SkipNegotiation = config.SignalR.SkipNegotiation
                },
                Version = config.Version,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };
            
            return Ok(response);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "获取持久化通信配置失败 - Failed to get persisted communication configuration");
            return StatusCode(500, new { message = "获取持久化通信配置失败 - Failed to get persisted communication configuration" });
        }
    }

    /// <summary>
    /// 更新持久化通信配置（支持热更新）
    /// </summary>
    /// <param name="request">通信配置请求，包含按协议分组的参数</param>
    /// <returns>更新后的通信配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求 - TCP模式:
    ///
    ///     PUT /api/communication/config/persisted
    ///     {
    ///         "mode": 1,
    ///         "connectionMode": 0,
    ///         "timeoutMs": 5000,
    ///         "retryCount": 3,
    ///         "retryDelayMs": 1000,
    ///         "enableAutoReconnect": true,
    ///         "tcp": {
    ///             "tcpServer": "192.168.1.100:8000",
    ///             "receiveBufferSize": 8192,
    ///             "sendBufferSize": 8192,
    ///             "noDelay": true,
    ///             "keepAliveInterval": 60
    ///         }
    ///     }
    ///
    /// 示例请求 - MQTT模式:
    ///
    ///     PUT /api/communication/config/persisted
    ///     {
    ///         "mode": 3,
    ///         "connectionMode": 0,
    ///         "timeoutMs": 5000,
    ///         "retryCount": 3,
    ///         "mqtt": {
    ///             "mqttBroker": "mqtt://192.168.1.100:1883",
    ///             "mqttTopic": "sorting/chute/assignment",
    ///             "qualityOfServiceLevel": 1,
    ///             "cleanSession": true,
    ///             "clientIdPrefix": "WheelDiverter"
    ///         }
    ///     }
    ///
    /// 通信模式说明（mode字段）：
    /// - 0: Http - HTTP REST API通信
    /// - 1: Tcp - TCP Socket通信
    /// - 2: SignalR - SignalR实时通信
    /// - 3: Mqtt - MQTT消息队列通信
    /// 
    /// 连接模式说明（connectionMode字段）：
    /// - 0: Client - 本程序作为客户端，主动连接上游
    /// - 1: Server - 本程序作为服务端，监听上游连接
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 只需填写对应通信模式的配置项，其他协议的配置可以省略或保留默认值。
    /// </remarks>
    [HttpPut("config/persisted")]
    [SwaggerOperation(
        Summary = "更新持久化通信配置",
        Description = "更新通信配置并持久化到数据库，参数按协议分组。配置立即生效无需重启。",
        OperationId = "UpdatePersistedConfiguration",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(CommunicationConfigurationResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(CommunicationConfigurationResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfigurationResponse> UpdatePersistedConfiguration([FromBody] CommunicationConfigurationRequest request) {
        try {
            if (!ModelState.IsValid) {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效 - Invalid request parameters", errors });
            }

            // 将请求转换为 CommunicationConfiguration
            var config = new CommunicationConfiguration
            {
                Mode = request.Mode,
                ConnectionMode = request.ConnectionMode,
                TimeoutMs = request.TimeoutMs,
                RetryCount = request.RetryCount,
                RetryDelayMs = request.RetryDelayMs,
                EnableAutoReconnect = request.EnableAutoReconnect,
                TcpServer = request.Tcp?.TcpServer,
                HttpApi = request.Http?.HttpApi,
                MqttBroker = request.Mqtt?.MqttBroker,
                MqttTopic = request.Mqtt?.MqttTopic ?? "sorting/chute/assignment",
                SignalRHub = request.SignalR?.SignalRHub,
                Tcp = new Core.LineModel.Configuration.TcpConfig
                {
                    ReceiveBufferSize = request.Tcp?.ReceiveBufferSize ?? 8192,
                    SendBufferSize = request.Tcp?.SendBufferSize ?? 8192,
                    NoDelay = request.Tcp?.NoDelay ?? true,
                    KeepAliveInterval = request.Tcp?.KeepAliveInterval ?? 60
                },
                Http = new Core.LineModel.Configuration.HttpConfig
                {
                    MaxConnectionsPerServer = request.Http?.MaxConnectionsPerServer ?? 10,
                    PooledConnectionIdleTimeout = request.Http?.PooledConnectionIdleTimeout ?? 60,
                    PooledConnectionLifetime = request.Http?.PooledConnectionLifetime ?? 0,
                    UseHttp2 = request.Http?.UseHttp2 ?? false
                },
                Mqtt = new Core.LineModel.Configuration.MqttConfig
                {
                    QualityOfServiceLevel = request.Mqtt?.QualityOfServiceLevel ?? 1,
                    CleanSession = request.Mqtt?.CleanSession ?? true,
                    SessionExpiryInterval = request.Mqtt?.SessionExpiryInterval ?? 3600,
                    MessageExpiryInterval = request.Mqtt?.MessageExpiryInterval ?? 0,
                    ClientIdPrefix = request.Mqtt?.ClientIdPrefix ?? "WheelDiverter"
                },
                SignalR = new Core.LineModel.Configuration.SignalRConfig
                {
                    HandshakeTimeout = request.SignalR?.HandshakeTimeout ?? 15,
                    KeepAliveInterval = request.SignalR?.KeepAliveInterval ?? 30,
                    ServerTimeout = request.SignalR?.ServerTimeout ?? 60,
                    SkipNegotiation = request.SignalR?.SkipNegotiation ?? false
                }
            };

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid) {
                return BadRequest(new { message = errorMessage });
            }

            _configRepository.Update(config);

            _logger.LogInformation(
                "通信配置已更新 - Communication configuration updated: Mode={Mode}, Version={Version}",
                config.Mode,
                config.Version);

            // 返回更新后的配置（重新获取以确保返回持久化后的值）
            return GetPersistedConfiguration();
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
    [SwaggerResponse(200, "重置成功", typeof(CommunicationConfigurationResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(CommunicationConfigurationResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfigurationResponse> ResetPersistedConfiguration() {
        try {
            var defaultConfig = CommunicationConfiguration.GetDefault();
            _configRepository.Update(defaultConfig);

            _logger.LogInformation("通信配置已重置为默认值 - Communication configuration reset to defaults");

            return GetPersistedConfiguration();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "重置通信配置失败 - Failed to reset communication configuration");
            return StatusCode(500, new { message = "重置通信配置失败 - Failed to reset communication configuration" });
        }
    }
}
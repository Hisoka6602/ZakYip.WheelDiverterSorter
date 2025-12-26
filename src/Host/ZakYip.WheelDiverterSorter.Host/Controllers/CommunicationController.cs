using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Host.Models.Communication;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.System;


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
    private readonly IUpstreamRoutingClient _upstreamClient;
    private readonly IOptions<UpstreamConnectionOptions> _connectionOptions;
    private readonly ICommunicationConfigurationRepository _configRepository;
    private readonly ICommunicationStatsService _statsService;
    private readonly ILogger<CommunicationController> _logger;
    private readonly ISystemClock _systemClock;
    private readonly ISystemStateManager _stateManager;
    private readonly IUpstreamConnectionManager? _connectionManager;
    private readonly UpstreamServerBackgroundService? _serverBackgroundService;

    public CommunicationController(
        IUpstreamRoutingClient upstreamClient,
        IOptions<UpstreamConnectionOptions> connectionOptions,
        ICommunicationConfigurationRepository configRepository,
        ICommunicationStatsService statsService,
        ILogger<CommunicationController> logger,
        ISystemClock systemClock,
        ISystemStateManager stateManager,
        IUpstreamConnectionManager? connectionManager = null,
        UpstreamServerBackgroundService? serverBackgroundService = null) {
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
        _connectionOptions = connectionOptions ?? throw new ArgumentNullException(nameof(connectionOptions));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _connectionManager = connectionManager;
        _serverBackgroundService = serverBackgroundService;
    }

    #region 通信状态

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
            // 从持久化配置读取通信模式，确保与其他端点一致
            var persistedConfig = _configRepository.Get();
            
            // 检查实际连接状态（无论是 Client 还是 Server 模式）
            var isConnected = _upstreamClient.IsConnected;
            
            // 根据连接模式生成适当的消息
            string? errorMessage = null;
            List<ConnectedClientDto>? connectedClients = null;
            
            // Server模式：获取已连接客户端信息
            if (persistedConfig.ConnectionMode == ConnectionMode.Server)
            {
                if (_serverBackgroundService?.CurrentServer != null)
                {
                    var server = _serverBackgroundService.CurrentServer;
                    isConnected = server.IsRunning;
                    
                    if (isConnected)
                    {
                        // 获取所有已连接的客户端
                        var clients = server.GetConnectedClients();
                        if (clients.Any())
                        {
                            connectedClients = clients.Select(c => new ConnectedClientDto
                            {
                                ClientId = c.ClientId,
                                ClientAddress = c.ClientAddress,
                                ConnectedAt = c.ConnectedAt,
                                ConnectionDurationSeconds = (long)(_systemClock.LocalNowOffset - c.ConnectedAt).TotalSeconds
                            }).ToList();
                            errorMessage = null;
                        }
                        else
                        {
                            errorMessage = "Server 模式下尚无上游客户端连接。等待上游系统连接。";
                        }
                    }
                    else
                    {
                        errorMessage = "Server 模式下服务器未运行。";
                    }
                }
                else
                {
                    errorMessage = "Server 模式下服务器未初始化。";
                    isConnected = false;
                }
            }
            else // Client模式
            {
                if (!isConnected)
                {
                    errorMessage = "当前未连接到上游。Client 模式下会自动重试连接。";
                }
            }

            var response = new CommunicationStatusResponse {
                Mode = persistedConfig.Mode.ToString(),
                ConnectionMode = persistedConfig.ConnectionMode.ToString(),
                IsConnected = isConnected,
                MessagesSent = _statsService.MessagesSent,
                MessagesReceived = _statsService.MessagesReceived,
                ConnectionDurationSeconds = _statsService.ConnectionDurationSeconds,
                LastConnectedAt = _statsService.LastConnectedAt,
                LastDisconnectedAt = _statsService.LastDisconnectedAt,
                ServerAddress = GetServerAddress(persistedConfig),
                ErrorMessage = errorMessage,
                ConnectedClients = connectedClients
            };

            return Ok(response);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "获取通信状态失败 - Failed to get communication status");
            return StatusCode(500, new { message = "获取通信状态失败 - Failed to get communication status" });
        }
    }

    /// <summary>
    /// 获取服务器地址
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: HTTP 模式已移除。
    /// </remarks>
    private static string? GetServerAddress(CommunicationConfiguration config)
    {
        return config.Mode switch
        {
            CommunicationMode.Tcp => config.TcpServer,
            CommunicationMode.Mqtt => config.MqttBroker,
            CommunicationMode.SignalR => config.SignalRHub,
            _ => null
        };
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
    /// 获取通信配置（别名端点，指向持久化配置）
    /// </summary>
    /// <returns>通信配置响应</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 此端点是 /api/communication/config/persisted 的别名，用于向后兼容。
    /// 推荐使用 /api/communication/config/persisted 端点。
    /// </remarks>
    [HttpGet("config")]
    [SwaggerOperation(
        Summary = "获取通信配置（别名）",
        Description = "返回持久化的通信配置，此端点是config/persisted的别名",
        OperationId = "GetConfiguration",
        Tags = new[] { "通信管理" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(CommunicationConfigurationResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(CommunicationConfigurationResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfigurationResponse> GetConfiguration()
    {
        // 直接调用持久化配置端点
        return GetPersistedConfiguration();
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
            
            // PR-UPSTREAM01: HTTP 配置已移除
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
                    NoDelay = config.Tcp.NoDelay
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
    ///             "noDelay": true
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
    public async Task<ActionResult<CommunicationConfigurationResponse>> UpdatePersistedConfiguration([FromBody] CommunicationConfigurationRequest request) {
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
                MqttBroker = request.Mqtt?.MqttBroker,
                MqttTopic = request.Mqtt?.MqttTopic ?? "sorting/chute/assignment",
                SignalRHub = request.SignalR?.SignalRHub,
                Tcp = new Core.LineModel.Configuration.Models.TcpConfig
                {
                    ReceiveBufferSize = request.Tcp?.ReceiveBufferSize ?? 8192,
                    SendBufferSize = request.Tcp?.SendBufferSize ?? 8192,
                    NoDelay = request.Tcp?.NoDelay ?? true
                },
                Mqtt = new Core.LineModel.Configuration.Models.MqttConfig
                {
                    QualityOfServiceLevel = request.Mqtt?.QualityOfServiceLevel ?? 1,
                    CleanSession = request.Mqtt?.CleanSession ?? true,
                    SessionExpiryInterval = request.Mqtt?.SessionExpiryInterval ?? 3600,
                    MessageExpiryInterval = request.Mqtt?.MessageExpiryInterval ?? 0,
                    ClientIdPrefix = request.Mqtt?.ClientIdPrefix ?? "WheelDiverter"
                },
                SignalR = new Core.LineModel.Configuration.Models.SignalRConfig
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
                "通信配置已更新 - Communication configuration updated: Mode={Mode}, ConnectionMode={ConnectionMode}, Version={Version}",
                config.Mode,
                config.ConnectionMode,
                config.Version);

            // 根据连接模式触发对应的重新连接/重启
            var updatedOptions = MapToUpstreamConnectionOptions(config);
            
            // Client模式：通过 UpstreamConnectionManager 触发重新连接
            if (config.ConnectionMode == ConnectionMode.Client && _connectionManager != null)
            {
                try
                {
                    await _connectionManager.UpdateConnectionOptionsAsync(updatedOptions);
                    
                    _logger.LogInformation(
                        "Client模式：UpstreamConnectionManager 已更新配置并将使用新参数重新连接 - " +
                        "Client mode: UpstreamConnectionManager updated configuration and will reconnect with new parameters");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "更新 UpstreamConnectionManager 配置时出错 - Error updating UpstreamConnectionManager configuration");
                }
            }
            // Server模式：通过 UpstreamServerBackgroundService 触发服务器重启
            else if (config.ConnectionMode == ConnectionMode.Server && _serverBackgroundService != null)
            {
                try
                {
                    await _serverBackgroundService.UpdateServerConfigurationAsync(updatedOptions);
                    
                    _logger.LogInformation(
                        "Server模式：UpstreamServerBackgroundService 已重启服务器并使用新配置 - " +
                        "Server mode: UpstreamServerBackgroundService restarted server with new configuration");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "重启 UpstreamServerBackgroundService 时出错 - Error restarting UpstreamServerBackgroundService");
                }
            }
            else
            {
                _logger.LogWarning(
                    "配置已更新但无法触发重新连接/重启：ConnectionMode={ConnectionMode}, " +
                    "ConnectionManager={HasConnectionManager}, ServerBackgroundService={HasServerService}",
                    config.ConnectionMode,
                    _connectionManager != null,
                    _serverBackgroundService != null);
            }

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
    /// 将 CommunicationConfiguration 映射到 UpstreamConnectionOptions
    /// Map CommunicationConfiguration to UpstreamConnectionOptions
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: HTTP 配置已移除。
    /// PR-CONFIG-HOTRELOAD02: 使用 Core 层的配置类型，移除已废弃的 RetryCount/RetryDelayMs 字段
    /// </remarks>
    private static UpstreamConnectionOptions MapToUpstreamConnectionOptions(CommunicationConfiguration config)
    {
        return new UpstreamConnectionOptions
        {
            Mode = config.Mode,
            ConnectionMode = config.ConnectionMode,
            TcpServer = config.TcpServer,
            SignalRHub = config.SignalRHub,
            MqttBroker = config.MqttBroker,
            MqttTopic = config.MqttTopic,
            TimeoutMs = config.TimeoutMs,
            EnableAutoReconnect = config.EnableAutoReconnect,
            InitialBackoffMs = config.InitialBackoffMs,
            MaxBackoffMs = config.MaxBackoffMs,
            EnableInfiniteRetry = config.EnableInfiniteRetry,
            Tcp = new TcpConnectionOptions
            {
                ReceiveBufferSize = config.Tcp.ReceiveBufferSize,
                SendBufferSize = config.Tcp.SendBufferSize,
                NoDelay = config.Tcp.NoDelay
            },
            Mqtt = new MqttConnectionOptions
            {
                QualityOfServiceLevel = config.Mqtt.QualityOfServiceLevel,
                CleanSession = config.Mqtt.CleanSession,
                SessionExpiryInterval = config.Mqtt.SessionExpiryInterval,
                MessageExpiryInterval = config.Mqtt.MessageExpiryInterval,
                ClientIdPrefix = config.Mqtt.ClientIdPrefix
            },
            SignalR = new SignalRConnectionOptions
            {
                HandshakeTimeout = config.SignalR.HandshakeTimeout,
                KeepAliveInterval = config.SignalR.KeepAliveInterval,
                ServerTimeout = config.SignalR.ServerTimeout,
                SkipNegotiation = config.SignalR.SkipNegotiation
            }
        };
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

    #endregion
}
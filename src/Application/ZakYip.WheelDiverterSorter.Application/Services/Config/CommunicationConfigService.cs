using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 通信配置服务实现
/// PR-U1: 使用 IUpstreamRoutingClient 替代 IRuleEngineClient
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public class CommunicationConfigService : ICommunicationConfigService
{
    private static readonly object CommunicationConfigCacheKey = new();

    private readonly IUpstreamRoutingClient _upstreamClient;
    private readonly ICommunicationConfigurationRepository _configRepository;
    private readonly ICommunicationStatsService _statsService;
    private readonly ISlidingConfigCache _configCache;
    private readonly ISystemClock _systemClock;
    private readonly IUpstreamConnectionManager? _connectionManager;
    private readonly UpstreamServerBackgroundService? _serverBackgroundService;
    private readonly ILogger<CommunicationConfigService> _logger;
    private readonly IConfigurationAuditLogger _auditLogger;

    public CommunicationConfigService(
        IUpstreamRoutingClient upstreamClient,
        ICommunicationConfigurationRepository configRepository,
        ICommunicationStatsService statsService,
        ISlidingConfigCache configCache,
        ISystemClock systemClock,
        ILogger<CommunicationConfigService> logger,
        IConfigurationAuditLogger auditLogger,
        IUpstreamConnectionManager? connectionManager = null,
        UpstreamServerBackgroundService? serverBackgroundService = null)
    {
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _connectionManager = connectionManager;
        _serverBackgroundService = serverBackgroundService;
    }

    /// <inheritdoc />
    public CommunicationConfiguration GetCommunicationConfiguration()
    {
        return _configCache.GetOrAdd(CommunicationConfigCacheKey, () => _configRepository.Get());
    }

    /// <inheritdoc />
    public async Task<CommunicationConfigUpdateResult> UpdateConfigurationAsync(UpdateCommunicationConfigCommand command)
    {
        try
        {
            // 获取修改前的配置
            var beforeConfig = _configRepository.Get();

            // 将命令转换为 CommunicationConfiguration
            var config = MapToConfiguration(command);

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return new CommunicationConfigUpdateResult(false, errorMessage, null);
            }

            _configRepository.Update(config);

            // 热更新：立即刷新缓存
            var updatedConfig = _configRepository.Get();
            _configCache.Set(CommunicationConfigCacheKey, updatedConfig);

            // 记录配置审计日志
            _auditLogger.LogConfigurationChange(
                configName: "CommunicationConfiguration",
                operationType: "Update",
                beforeConfig: beforeConfig,
                afterConfig: updatedConfig);

            _logger.LogInformation(
                "通信配置已更新（热更新生效） - Communication configuration updated: Mode={Mode}, ConnectionMode={ConnectionMode}, Version={Version}",
                updatedConfig.Mode,
                updatedConfig.ConnectionMode,
                updatedConfig.Version);

            // 根据连接模式触发对应的重新连接/重启
            var updatedOptions = MapToUpstreamConnectionOptions(updatedConfig);

            // Client模式：通过 UpstreamConnectionManager 触发重新连接
            if (updatedConfig.ConnectionMode == ConnectionMode.Client && _connectionManager != null)
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
            else if (updatedConfig.ConnectionMode == ConnectionMode.Server && _serverBackgroundService != null)
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
                    updatedConfig.ConnectionMode,
                    _connectionManager != null,
                    _serverBackgroundService != null);
            }

            return new CommunicationConfigUpdateResult(true, null, updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "通信配置验证失败 - Communication configuration validation failed");
            return new CommunicationConfigUpdateResult(false, ex.Message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新通信配置失败 - Failed to update communication configuration");
            return new CommunicationConfigUpdateResult(false, "更新通信配置失败 - Failed to update communication configuration", null);
        }
    }

    /// <inheritdoc />
    public CommunicationConfiguration ResetConfiguration()
    {
        // 获取重置前的配置
        var beforeConfig = _configRepository.Get();

        var defaultConfig = CommunicationConfiguration.GetDefault();
        _configRepository.Update(defaultConfig);

        // 热更新：立即刷新缓存
        var updatedConfig = _configRepository.Get();
        _configCache.Set(CommunicationConfigCacheKey, updatedConfig);

        // 记录配置审计日志
        _auditLogger.LogConfigurationChange(
            configName: "CommunicationConfiguration",
            operationType: "Reset",
            beforeConfig: beforeConfig,
            afterConfig: updatedConfig);

        _logger.LogInformation("通信配置已重置为默认值（热更新生效） - Communication configuration reset to defaults");

        return updatedConfig;
    }

    /// <inheritdoc />
    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var testedAt = _systemClock.LocalNowOffset;

        try
        {
            _logger.LogInformation("开始测试通信连接 - Starting communication connection test");

            // 检查配置的连接模式
            var persistedConfig = _configRepository.Get();

            // 如果是 Server 模式，提供明确说明
            if (persistedConfig.ConnectionMode == ConnectionMode.Server)
            {
                sw.Stop();
                _logger.LogWarning(
                    "尝试测试 Server 模式连接。Server 模式需要上游主动连接，无法从本端测试 - " +
                    "Attempted to test Server mode connection. Server mode requires upstream to connect, cannot test from this side.");

                return new ConnectionTestResult
                {
                    Success = false,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Message = "Server 模式无法测试连接 - Cannot test connection in Server mode",
                    ErrorDetails = "系统配置为 Server 模式，需要上游主动连接到本端。" +
                                   "请确认已启动监听服务，并从上游发起连接。" +
                                   "当前版本仅完全支持 Client 模式的连接测试。",
                    TestedAt = testedAt
                };
            }

            // Client 模式：尝试实际连接（连接测试改用PingAsync）
            var connected = await _upstreamClient.PingAsync(cancellationToken);
            sw.Stop();

            if (connected)
            {
                _logger.LogInformation("连接测试成功，响应时间: {ResponseTimeMs}ms - Connection test succeeded, response time: {ResponseTimeMs}ms",
                    sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);

                return new ConnectionTestResult
                {
                    Success = true,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Message = "连接成功 - Connection successful",
                    TestedAt = testedAt
                };
            }
            else
            {
                _logger.LogWarning("连接测试失败 - Connection test failed");

                return new ConnectionTestResult
                {
                    Success = false,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Message = "连接失败 - Connection failed",
                    ErrorDetails = $"无法建立到 {GetServerAddress(persistedConfig)} 的连接。请检查：" +
                                   "1. 服务器地址配置是否正确 " +
                                   "2. 网络连接是否正常 " +
                                   "3. 上游服务是否启动 " +
                                   "4. 防火墙设置是否允许连接",
                    TestedAt = testedAt
                };
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "连接测试时发生异常 - Exception occurred during connection test");

            return new ConnectionTestResult
            {
                Success = false,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = "连接测试失败 - Connection test failed",
                ErrorDetails = $"异常详情: {ex.Message}",
                TestedAt = testedAt
            };
        }
    }

    /// <inheritdoc />
    public CommunicationStatus GetStatus()
    {
        // 从持久化配置读取通信模式，确保与其他端点一致
        var persistedConfig = _configRepository.Get();

        // 检查实际连接状态（无论是 Client 还是 Server 模式）
        var isConnected = _upstreamClient.IsConnected;

        // 根据连接模式生成适当的消息
        string? errorMessage = null;
        if (!isConnected)
        {
            if (persistedConfig.ConnectionMode == ConnectionMode.Server)
            {
                errorMessage = "Server 模式下尚无上游客户端连接。等待上游系统连接。";
            }
            else
            {
                errorMessage = "当前未连接到上游。Client 模式下会自动重试连接。";
            }
        }

        return new CommunicationStatus
        {
            Mode = persistedConfig.Mode.ToString(),
            IsConnected = isConnected,
            MessagesSent = _statsService.MessagesSent,
            MessagesReceived = _statsService.MessagesReceived,
            ConnectionDurationSeconds = _statsService.ConnectionDurationSeconds,
            LastConnectedAt = _statsService.LastConnectedAt,
            LastDisconnectedAt = _statsService.LastDisconnectedAt,
            ServerAddress = GetServerAddress(persistedConfig),
            ErrorMessage = errorMessage
        };
    }

    /// <inheritdoc />
    public void ResetStatistics()
    {
        _statsService.Reset();
        _logger.LogInformation("通信统计已重置 - Communication statistics reset");
    }

    /// <summary>
    /// 获取服务器地址（用于日志）
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 地址获取。
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
    /// 映射更新命令到配置对象
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 配置映射。
    /// </remarks>
    private static CommunicationConfiguration MapToConfiguration(UpdateCommunicationConfigCommand command)
    {
        return new CommunicationConfiguration
        {
            Mode = command.Mode,
            ConnectionMode = command.ConnectionMode,
            TimeoutMs = command.TimeoutMs,
            RetryCount = command.RetryCount,
            RetryDelayMs = command.RetryDelayMs,
            EnableAutoReconnect = command.EnableAutoReconnect,
            TcpServer = command.Tcp?.TcpServer,
            MqttBroker = command.Mqtt?.MqttBroker,
            MqttTopic = command.Mqtt?.MqttTopic ?? "sorting/chute/assignment",
            SignalRHub = command.SignalR?.SignalRHub,
            Tcp = new TcpConfig
            {
                ReceiveBufferSize = command.Tcp?.ReceiveBufferSize ?? 8192,
                SendBufferSize = command.Tcp?.SendBufferSize ?? 8192,
                NoDelay = command.Tcp?.NoDelay ?? true
            },
            Mqtt = new MqttConfig
            {
                QualityOfServiceLevel = command.Mqtt?.QualityOfServiceLevel ?? 1,
                CleanSession = command.Mqtt?.CleanSession ?? true,
                SessionExpiryInterval = command.Mqtt?.SessionExpiryInterval ?? 3600,
                MessageExpiryInterval = command.Mqtt?.MessageExpiryInterval ?? 0,
                ClientIdPrefix = command.Mqtt?.ClientIdPrefix ?? "WheelDiverter"
            },
            SignalR = new SignalRConfig
            {
                HandshakeTimeout = command.SignalR?.HandshakeTimeout ?? 15,
                KeepAliveInterval = command.SignalR?.KeepAliveInterval ?? 30,
                ServerTimeout = command.SignalR?.ServerTimeout ?? 60,
                SkipNegotiation = command.SignalR?.SkipNegotiation ?? false
            }
        };
    }

    /// <summary>
    /// 映射配置对象到 UpstreamConnectionOptions
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 配置映射。
    /// PR-CONFIG-HOTRELOAD02: 使用 Core 层的嵌套配置类型，移除对 Communication.Configuration 独立类型的依赖
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
}

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// 通信配置服务实现
/// </summary>
public class CommunicationConfigService : ICommunicationConfigService
{
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ICommunicationConfigurationRepository _configRepository;
    private readonly ICommunicationStatsService _statsService;
    private readonly ISystemClock _systemClock;
    private readonly IUpstreamConnectionManager? _connectionManager;
    private readonly UpstreamServerBackgroundService? _serverBackgroundService;
    private readonly ILogger<CommunicationConfigService> _logger;

    public CommunicationConfigService(
        IRuleEngineClient ruleEngineClient,
        ICommunicationConfigurationRepository configRepository,
        ICommunicationStatsService statsService,
        ISystemClock systemClock,
        ILogger<CommunicationConfigService> logger,
        IUpstreamConnectionManager? connectionManager = null,
        UpstreamServerBackgroundService? serverBackgroundService = null)
    {
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionManager = connectionManager;
        _serverBackgroundService = serverBackgroundService;
    }

    /// <inheritdoc />
    public CommunicationConfiguration GetConfiguration()
    {
        return _configRepository.Get();
    }

    /// <inheritdoc />
    public async Task<CommunicationConfigUpdateResult> UpdateConfigurationAsync(UpdateCommunicationConfigCommand command)
    {
        try
        {
            // 将命令转换为 CommunicationConfiguration
            var config = MapToConfiguration(command);

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return new CommunicationConfigUpdateResult(false, errorMessage, null);
            }

            _configRepository.Update(config);

            _logger.LogInformation(
                "通信配置已更新 - Communication configuration updated: Mode={Mode}, ConnectionMode={ConnectionMode}, Version={Version}",
                config.Mode,
                config.ConnectionMode,
                config.Version);

            // 根据连接模式触发对应的重新连接/重启
            var updatedOptions = MapToRuleEngineConnectionOptions(config);

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

            // 返回更新后的配置
            var updatedConfig = _configRepository.Get();
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
        var defaultConfig = CommunicationConfiguration.GetDefault();
        _configRepository.Update(defaultConfig);

        _logger.LogInformation("通信配置已重置为默认值 - Communication configuration reset to defaults");

        return _configRepository.Get();
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

            // Client 模式：尝试实际连接
            var connected = await _ruleEngineClient.ConnectAsync(cancellationToken);
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
        var isConnected = _ruleEngineClient.IsConnected;

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

    private static string? GetServerAddress(CommunicationConfiguration config)
    {
        return config.Mode switch
        {
            CommunicationMode.Tcp => config.TcpServer,
            CommunicationMode.Http => config.HttpApi,
            CommunicationMode.Mqtt => config.MqttBroker,
            CommunicationMode.SignalR => config.SignalRHub,
            _ => null
        };
    }

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
            HttpApi = command.Http?.HttpApi,
            MqttBroker = command.Mqtt?.MqttBroker,
            MqttTopic = command.Mqtt?.MqttTopic ?? "sorting/chute/assignment",
            SignalRHub = command.SignalR?.SignalRHub,
            Tcp = new TcpConfig
            {
                ReceiveBufferSize = command.Tcp?.ReceiveBufferSize ?? 8192,
                SendBufferSize = command.Tcp?.SendBufferSize ?? 8192,
                NoDelay = command.Tcp?.NoDelay ?? true
            },
            Http = new HttpConfig
            {
                MaxConnectionsPerServer = command.Http?.MaxConnectionsPerServer ?? 10,
                PooledConnectionIdleTimeout = command.Http?.PooledConnectionIdleTimeout ?? 60,
                PooledConnectionLifetime = command.Http?.PooledConnectionLifetime ?? 0,
                UseHttp2 = command.Http?.UseHttp2 ?? false
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

    private static RuleEngineConnectionOptions MapToRuleEngineConnectionOptions(CommunicationConfiguration config)
    {
        return new RuleEngineConnectionOptions
        {
            Mode = config.Mode,
            ConnectionMode = config.ConnectionMode,
            TcpServer = config.TcpServer,
            SignalRHub = config.SignalRHub,
            MqttBroker = config.MqttBroker,
            MqttTopic = config.MqttTopic,
            HttpApi = config.HttpApi,
            TimeoutMs = config.TimeoutMs,
            RetryCount = config.RetryCount,
            RetryDelayMs = config.RetryDelayMs,
            EnableAutoReconnect = config.EnableAutoReconnect,
            Tcp = new Communication.Configuration.TcpOptions
            {
                ReceiveBufferSize = config.Tcp.ReceiveBufferSize,
                SendBufferSize = config.Tcp.SendBufferSize,
                NoDelay = config.Tcp.NoDelay
            },
            Http = new Communication.Configuration.HttpOptions
            {
                MaxConnectionsPerServer = config.Http.MaxConnectionsPerServer,
                PooledConnectionIdleTimeout = config.Http.PooledConnectionIdleTimeout,
                PooledConnectionLifetime = config.Http.PooledConnectionLifetime,
                UseHttp2 = config.Http.UseHttp2
            },
            Mqtt = new Communication.Configuration.MqttOptions
            {
                QualityOfServiceLevel = config.Mqtt.QualityOfServiceLevel,
                CleanSession = config.Mqtt.CleanSession,
                SessionExpiryInterval = config.Mqtt.SessionExpiryInterval,
                MessageExpiryInterval = config.Mqtt.MessageExpiryInterval,
                ClientIdPrefix = config.Mqtt.ClientIdPrefix
            },
            SignalR = new Communication.Configuration.SignalROptions
            {
                HandshakeTimeout = config.SignalR.HandshakeTimeout,
                KeepAliveInterval = config.SignalR.KeepAliveInterval,
                ServerTimeout = config.SignalR.ServerTimeout,
                SkipNegotiation = config.SignalR.SkipNegotiation
            }
        };
    }
}

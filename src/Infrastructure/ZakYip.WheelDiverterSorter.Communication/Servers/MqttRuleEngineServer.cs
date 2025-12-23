using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Server;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Servers;

/// <summary>
/// MQTT服务器实现 - 集成MQTT Broker功能，支持主题订阅/发布
/// MQTT Server implementation - integrated MQTT Broker functionality with topic subscription/publishing
/// </summary>
public sealed class MqttRuleEngineServer : IRuleEngineServer
{
    private readonly ILogger<MqttRuleEngineServer> _logger;
    private readonly UpstreamConnectionOptions _options;
    private readonly ISystemClock _systemClock;
    private readonly IRuleEngineHandler? _handler;
    
    private readonly ConcurrentDictionary<string, ClientInfo> _clients = new();
    private MqttServer? _mqttServer;
    private bool _isRunning;
    private bool _disposed;

    public MqttRuleEngineServer(
        ILogger<MqttRuleEngineServer> logger,
        UpstreamConnectionOptions options,
        ISystemClock systemClock,
        IRuleEngineHandler? handler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _handler = handler;

        ValidateMqttServerOptions(options);
    }

    public bool IsRunning => _isRunning;
    public int ConnectedClientsCount => _clients.Count;

    public event EventHandler<ClientConnectionEventArgs>? ClientConnected;
    public event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;
    
#pragma warning disable CS0067 // Event is never used - Legacy event, kept for interface compatibility
    public event EventHandler<ParcelNotificationReceivedEventArgs>? ParcelNotificationReceived;
#pragma warning restore CS0067
    
    /// <summary>
    /// 格口分配事件（与客户端模式保持一致）
    /// </summary>
    public event EventHandler<Core.Abstractions.Upstream.ChuteAssignmentEventArgs>? ChuteAssigned;

    public IReadOnlyList<ClientConnectionEventArgs> GetConnectedClients()
    {
        return _clients.Values
            .Select(c => new ClientConnectionEventArgs
            {
                ClientId = c.ClientId,
                ConnectedAt = c.ConnectedAt,
                ClientAddress = c.ClientAddress
            })
            .ToList()
            .AsReadOnly();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT服务器已在运行");
            return;
        }

        var parts = _options.MqttBroker!.Split(':');
        var port = int.Parse(parts[1]);

        var mqttFactory = new MqttFactory();
        var mqttServerOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();

        _mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

        // 订阅客户端连接事件
        _mqttServer.ClientConnectedAsync += OnClientConnectedAsync;
        _mqttServer.ClientDisconnectedAsync += OnClientDisconnectedAsync;
        _mqttServer.InterceptingPublishAsync += OnMessageReceivedAsync;

        await _mqttServer.StartAsync();
        _isRunning = true;

        _logger.LogInformation(
            "[{LocalTime}] MQTT服务器已启动，监听端口 {Port}",
            _systemClock.LocalNow,
            port);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        if (_mqttServer != null)
        {
            await _mqttServer.StopAsync();
            
            // 取消订阅事件
            _mqttServer.ClientConnectedAsync -= OnClientConnectedAsync;
            _mqttServer.ClientDisconnectedAsync -= OnClientDisconnectedAsync;
            _mqttServer.InterceptingPublishAsync -= OnMessageReceivedAsync;
        }

        _clients.Clear();

        _logger.LogInformation(
            "[{LocalTime}] MQTT服务器已停止",
            _systemClock.LocalNow);
    }

    public async Task BroadcastChuteAssignmentAsync(
        long parcelId,
        string chuteId,
        CancellationToken cancellationToken = default)
    {
        if (_mqttServer == null || !_isRunning)
        {
            _logger.LogWarning("MQTT服务器未运行，无法广播消息");
            return;
        }

        // PR-UPSTREAM02: 使用 ChuteAssignmentNotification 代替 ChuteAssignmentResponse
        var notification = new ChuteAssignmentNotification
        {
            ParcelId = parcelId,
            ChuteId = int.Parse(chuteId),
            AssignedAt = _systemClock.LocalNowOffset
        };

        var json = JsonSerializer.Serialize(notification);
        var payload = Encoding.UTF8.GetBytes(json);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_options.MqttTopic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttServer.InjectApplicationMessage(
            new InjectedMqttApplicationMessage(message)
            {
                SenderClientId = "RuleEngineServer"
            });

        _logger.LogInformation(
            "[{LocalTime}] 已向主题 {Topic} 广播包裹 {ParcelId} 的格口分配: {ChuteId}",
            _systemClock.LocalNow,
            _options.MqttTopic,
            parcelId,
            chuteId);
    }

    public async Task BroadcastParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        if (_mqttServer == null || !_isRunning)
        {
            _logger.LogWarning("MQTT服务器未运行，无法广播包裹检测通知");
            return;
        }

        var notification = new ParcelDetectionNotification
        {
            ParcelId = parcelId,
            DetectionTime = _systemClock.LocalNowOffset
        };

        var json = JsonSerializer.Serialize(notification);
        var payload = Encoding.UTF8.GetBytes(json);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_options.MqttTopic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttServer.InjectApplicationMessage(
            new InjectedMqttApplicationMessage(message)
            {
                SenderClientId = "RuleEngineServer"
            });

        _logger.LogInformation(
            "[{LocalTime}] 已向主题 {Topic} 广播包裹检测通知: ParcelId={ParcelId}",
            _systemClock.LocalNow,
            _options.MqttTopic,
            parcelId);
    }

    public async Task BroadcastSortingCompletedAsync(
        Core.Abstractions.Upstream.SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!_isRunning || _mqttServer == null)
        {
            _logger.LogWarning("MQTT服务器未运行，无法广播分拣完成通知");
            return;
        }

        var notificationDto = new SortingCompletedNotificationDto
        {
            ParcelId = notification.ParcelId,
            ActualChuteId = notification.ActualChuteId,
            CompletedAt = notification.CompletedAt,
            IsSuccess = notification.IsSuccess,
            FinalStatus = notification.FinalStatus,
            FailureReason = notification.FailureReason
        };

        var json = JsonSerializer.Serialize(notificationDto);
        var payload = Encoding.UTF8.GetBytes(json);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_options.MqttTopic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttServer.InjectApplicationMessage(
            new InjectedMqttApplicationMessage(message)
            {
                SenderClientId = "RuleEngineServer"
            });

        _logger.LogInformation(
            "[{LocalTime}] 已向主题 {Topic} 广播分拣完成通知: ParcelId={ParcelId}",
            _systemClock.LocalNow,
            _options.MqttTopic,
            notification.ParcelId);
    }

    private Task OnClientConnectedAsync(ClientConnectedEventArgs args)
    {
        var clientId = args.ClientId;
        var clientInfo = new ClientInfo
        {
            ClientId = clientId,
            ConnectedAt = _systemClock.LocalNowOffset
        };

        _clients[clientId] = clientInfo;

        _logger.LogInformation(
            "[{LocalTime}] MQTT客户端已连接: {ClientId}",
            _systemClock.LocalNow,
            clientId);

        // 触发客户端连接事件
        ClientConnected.SafeInvoke(this, new ClientConnectionEventArgs
        {
            ClientId = clientId,
            ConnectedAt = clientInfo.ConnectedAt,
            ClientAddress = null
        }, _logger, nameof(ClientConnected));

        return Task.CompletedTask;
    }

    private Task OnClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        var clientId = args.ClientId;

        if (_clients.TryRemove(clientId, out var clientInfo))
        {
            _logger.LogInformation(
                "[{LocalTime}] MQTT客户端已断开: {ClientId}",
                _systemClock.LocalNow,
                clientId);

            // 触发客户端断开事件
            ClientDisconnected.SafeInvoke(this, new ClientConnectionEventArgs
            {
                ClientId = clientId,
                ConnectedAt = clientInfo.ConnectedAt,
                ClientAddress = null
            }, _logger, nameof(ClientDisconnected));
        }

        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(InterceptingPublishEventArgs args)
    {
        try
        {
            // 只处理包裹通知主题的消息
            if (args.ApplicationMessage.Topic != _options.MqttTopic)
            {
                return;
            }

            var json = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            // 尝试解析为格口分配通知（上游主动推送）
            var notification = JsonSerializer.Deserialize<ChuteAssignmentNotification>(json);

            if (notification != null)
            {
                _logger.LogInformation(
                    "[{LocalTime}] 收到MQTT客户端 {ClientId} 的格口分配通知: ParcelId={ParcelId} | ChuteId={ChuteId}",
                    _systemClock.LocalNow,
                    args.ClientId,
                    notification.ParcelId,
                    notification.ChuteId);

                // 转换 DwsPayload from DTO to domain model
                DwsMeasurement? dwsPayload = null;
                if (notification.DwsPayload != null)
                {
                    dwsPayload = new DwsMeasurement
                    {
                        WeightGrams = notification.DwsPayload.WeightGrams,
                        LengthMm = notification.DwsPayload.LengthMm,
                        WidthMm = notification.DwsPayload.WidthMm,
                        HeightMm = notification.DwsPayload.HeightMm,
                        VolumetricWeightGrams = notification.DwsPayload.VolumetricWeightGrams,
                        Barcode = notification.DwsPayload.Barcode,
                        MeasuredAt = notification.DwsPayload.MeasuredAt
                    };
                }

                // 触发格口分配事件（与客户端模式保持一致）
                var eventArgs = new ChuteAssignmentEventArgs
                {
                    ParcelId = notification.ParcelId,
                    ChuteId = notification.ChuteId,
                    AssignedAt = notification.AssignedAt,
                    DwsPayload = dwsPayload,
                    Metadata = notification.Metadata
                };
                
                ChuteAssigned.SafeInvoke(this, eventArgs, _logger, nameof(ChuteAssigned));
                
                // 同时调用 handler（如果存在）以保持向后兼容
                if (_handler != null)
                {
                    await _handler.HandleChuteAssignmentAsync(eventArgs);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "[{LocalTime}] 解析MQTT消息失败",
                _systemClock.LocalNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{LocalTime}] 处理MQTT消息时发生错误: {Message}",
                _systemClock.LocalNow,
                ex.Message);
        }
    }

    private static void ValidateMqttServerOptions(UpstreamConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MqttBroker))
        {
            throw new ArgumentException("MQTT Broker地址不能为空", nameof(options));
        }

        var parts = options.MqttBroker.Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException($"无效的MQTT Broker地址格式: {options.MqttBroker}", nameof(options));
        }

        if (!int.TryParse(parts[1], out var port) || port <= 0 || port > 65535)
        {
            throw new ArgumentException($"无效的端口号: {parts[1]}", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.MqttTopic))
        {
            throw new ArgumentException("MQTT主题不能为空", nameof(options));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _mqttServer?.Dispose();
        _disposed = true;
    }

    private sealed class ClientInfo
    {
        public required string ClientId { get; init; }
        public required DateTimeOffset ConnectedAt { get; init; }
        public string? ClientAddress { get; init; }
    }
}

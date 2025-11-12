using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于MQTT的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供轻量级IoT消息协议，支持QoS保证
/// </remarks>
public class MqttRuleEngineClient : IRuleEngineClient
{
    /// <summary>
    /// MQTT协议默认端口
    /// </summary>
    private const int MqttDefaultPort = 1883;

    private readonly ILogger<MqttRuleEngineClient> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private IMqttClient? _mqttClient;
    private readonly string _detectionTopic;
    private readonly string _assignmentTopic;
    private readonly Dictionary<string, TaskCompletionSource<ChuteAssignmentResponse>> _pendingRequests;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public bool IsConnected => _mqttClient?.IsConnected == true;

    /// <summary>
    /// 格口分配通知事件
    /// </summary>
    public event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    public MqttRuleEngineClient(
        ILogger<MqttRuleEngineClient> logger,
        RuleEngineConnectionOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.MqttBroker))
        {
            throw new ArgumentException("MQTT Broker地址不能为空", nameof(options));
        }

        _detectionTopic = $"{_options.MqttTopic}/detection";
        _assignmentTopic = $"{_options.MqttTopic}/assignment";
        _pendingRequests = new Dictionary<string, TaskCompletionSource<ChuteAssignmentResponse>>();

        InitializeMqttClient();
    }

    /// <summary>
    /// 初始化MQTT客户端
    /// </summary>
    private void InitializeMqttClient()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        _mqttClient.DisconnectedAsync += async args =>
        {
            _logger.LogWarning("MQTT连接已断开: {Reason}", args.Reason);
            if (_options.EnableAutoReconnect)
            {
                await Task.Delay(_options.RetryDelayMs);
                await ConnectAsync();
            }
        };

        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return true;
        }

        try
        {
            var uri = new Uri(_options.MqttBroker!);
            _logger.LogInformation("正在连接到MQTT Broker: {Broker}...", _options.MqttBroker);

            var mqttOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(uri.Host, uri.Port > 0 ? uri.Port : MqttDefaultPort)
                .WithClientId($"{_options.Mqtt.ClientIdPrefix}_{Guid.NewGuid():N}")
                .WithCleanSession(_options.Mqtt.CleanSession)
                .WithSessionExpiryInterval((uint)_options.Mqtt.SessionExpiryInterval)
                .Build();

            await _mqttClient!.ConnectAsync(mqttOptions, cancellationToken);

            // 订阅格口分配主题
            var qosLevel = _options.Mqtt.QualityOfServiceLevel switch
            {
                0 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
                2 => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
            };

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(_assignmentTopic).WithQualityOfServiceLevel(qosLevel))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);

            _logger.LogInformation(
                "成功连接到MQTT Broker并订阅主题: {Topic} (QoS: {Qos}, CleanSession: {Clean})",
                _assignmentTopic,
                _options.Mqtt.QualityOfServiceLevel,
                _options.Mqtt.CleanSession);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到MQTT Broker失败");
            return false;
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_mqttClient != null && IsConnected)
            {
                await _mqttClient.DisconnectAsync();
                _logger.LogInformation("已断开与MQTT Broker的连接");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开连接时发生异常");
        }
    }

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    public async Task<bool> NotifyParcelDetectedAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            throw new ArgumentException("包裹ID不能为空", nameof(parcelId));
        }

        // 尝试连接（如果未连接）
        if (!IsConnected)
        {
            var connected = await ConnectAsync(cancellationToken);
            if (!connected)
            {
                _logger.LogError("无法连接到MQTT Broker，无法发送包裹检测通知");
                return false;
            }
        }

        try
        {
            _logger.LogDebug("向RuleEngine发送包裹检测通知: {ParcelId}", parcelId);

            var notification = new ParcelDetectionNotification { ParcelId = parcelId };
            var notificationJson = JsonSerializer.Serialize(notification);
            
            var qosLevel = _options.Mqtt.QualityOfServiceLevel switch
            {
                0 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
                2 => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
            };

            var messageBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(_detectionTopic)
                .WithPayload(notificationJson)
                .WithQualityOfServiceLevel(qosLevel);

            if (_options.Mqtt.MessageExpiryInterval > 0)
            {
                messageBuilder.WithMessageExpiryInterval((uint)_options.Mqtt.MessageExpiryInterval);
            }

            var message = messageBuilder.Build();

            await _mqttClient!.PublishAsync(message, cancellationToken);

            _logger.LogInformation("成功发送包裹检测通知: {ParcelId}", parcelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送包裹检测通知失败: {ParcelId}", parcelId);
            return false;
        }
    }

    /// <summary>
    /// 请求包裹的格口号（已废弃，保留用于兼容性）
    /// </summary>
    [Obsolete("使用NotifyParcelDetectedAsync配合ChuteAssignmentReceived事件代替")]
    public async Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            throw new ArgumentException("包裹ID不能为空", nameof(parcelId));
        }

        // 尝试连接（如果未连接）
        if (!IsConnected)
        {
            var connected = await ConnectAsync(cancellationToken);
            if (!connected)
            {
                return new ChuteAssignmentResponse
                {
                    ParcelId = parcelId,
                    ChuteNumber = WellKnownChuteIds.DefaultException,
                    IsSuccess = false,
                    ErrorMessage = "无法连接到MQTT Broker"
                };
            }
        }

        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= _options.RetryCount)
        {
            try
            {
                _logger.LogDebug("向RuleEngine请求包裹 {ParcelId} 的格口号（第{Retry}次尝试）", parcelId, retryCount + 1);

                // 创建任务完成源
                var tcs = new TaskCompletionSource<ChuteAssignmentResponse>();
                _pendingRequests[parcelId] = tcs;

                // 构造并发布请求消息（向后兼容，使用detection主题）
                var request = new ChuteAssignmentRequest { ParcelId = parcelId };
                var requestJson = JsonSerializer.Serialize(request);
                
                var qosLevel = _options.Mqtt.QualityOfServiceLevel switch
                {
                    0 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
                    2 => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
                    _ => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
                };

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_detectionTopic)
                    .WithPayload(requestJson)
                    .WithQualityOfServiceLevel(qosLevel)
                    .Build();

                await _mqttClient!.PublishAsync(message, cancellationToken);

                // 等待响应（带超时）
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.TimeoutMs);

                var response = await tcs.Task.WaitAsync(cts.Token);

                _logger.LogInformation(
                    "成功获取包裹 {ParcelId} 的格口号: {ChuteNumber}",
                    parcelId,
                    response.ChuteNumber);

                return response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _logger.LogWarning(ex, "请求格口号失败（第{Retry}次尝试）", retryCount + 1);

                _pendingRequests.Remove(parcelId);

                retryCount++;
                if (retryCount <= _options.RetryCount)
                {
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                }
            }
        }

        _logger.LogError(lastException, "请求格口号失败，已达到最大重试次数");
        return new ChuteAssignmentResponse
        {
            ParcelId = parcelId,
            ChuteNumber = WellKnownChuteIds.DefaultException,
            IsSuccess = false,
            ErrorMessage = $"请求失败: {lastException?.Message}"
        };
    }

    /// <summary>
    /// 处理接收到的MQTT消息
    /// </summary>
    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            
            // 尝试解析为格口分配通知
            var notification = JsonSerializer.Deserialize<ChuteAssignmentNotificationEventArgs>(payload);

            if (notification != null)
            {
                _logger.LogDebug("收到包裹 {ParcelId} 的格口分配: {ChuteNumber}", 
                    notification.ParcelId, notification.ChuteNumber);
                
                // 触发事件
                ChuteAssignmentReceived?.Invoke(this, notification);

                // 如果有待处理的请求（向后兼容）
                if (_pendingRequests.TryGetValue(notification.ParcelId, out var tcs))
                {
                    var response = new ChuteAssignmentResponse
                    {
                        ParcelId = notification.ParcelId,
                        ChuteNumber = notification.ChuteNumber,
                        IsSuccess = true,
                        ResponseTime = notification.NotificationTime
                    };
                    tcs.SetResult(response);
                    _pendingRequests.Remove(notification.ParcelId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理MQTT消息时发生异常");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _mqttClient?.Dispose();
    }
}

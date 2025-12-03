using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于MQTT的上游路由通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供轻量级IoT消息协议，支持QoS保证
/// PR-U1: 直接实现 IUpstreamRoutingClient（通过基类）
/// PR-UPSTREAM02: 添加落格完成通知，更新消息处理以支持新的格口分配通知格式
/// </remarks>
public class MqttRuleEngineClient : RuleEngineClientBase
{
    /// <summary>
    /// MQTT协议默认端口
    /// </summary>
    private const int MqttDefaultPort = 1883;

    private IMqttClient? _mqttClient;
    private readonly string _detectionTopic;
    private readonly string _assignmentTopic;
    private readonly string _completionTopic;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public override bool IsConnected => _mqttClient?.IsConnected == true;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    public MqttRuleEngineClient(
        ILogger<MqttRuleEngineClient> logger,
        UpstreamConnectionOptions options,
        ISystemClock systemClock) : base(logger, options, systemClock)
    {
        if (string.IsNullOrWhiteSpace(options.MqttBroker))
        {
            throw new ArgumentException("MQTT Broker地址不能为空", nameof(options));
        }

        _detectionTopic = $"{options.MqttTopic}/detection";
        _assignmentTopic = $"{options.MqttTopic}/assignment";
        _completionTopic = $"{options.MqttTopic}/completion";

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
            Logger.LogWarning("MQTT连接已断开: {Reason}", args.Reason);
            if (Options.EnableAutoReconnect)
            {
                await Task.Delay(Options.RetryDelayMs);
                await ConnectAsync();
            }
        };

        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return true;
        }

        try
        {
            var uri = new Uri(Options.MqttBroker!);
            Logger.LogInformation("正在连接到MQTT Broker: {Broker}...", Options.MqttBroker);

            var mqttOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(uri.Host, uri.Port > 0 ? uri.Port : MqttDefaultPort)
                .WithClientId($"{Options.Mqtt.ClientIdPrefix}_{Guid.NewGuid():N}")
                .WithCleanSession(Options.Mqtt.CleanSession)
                .WithSessionExpiryInterval((uint)Options.Mqtt.SessionExpiryInterval)
                .Build();

            await _mqttClient!.ConnectAsync(mqttOptions, cancellationToken);

            // 订阅格口分配主题
            var qosLevel = GetQosLevel();

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(_assignmentTopic).WithQualityOfServiceLevel(qosLevel))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);

            Logger.LogInformation(
                "成功连接到MQTT Broker并订阅主题: {Topic} (QoS: {Qos}, CleanSession: {Clean})",
                _assignmentTopic,
                Options.Mqtt.QualityOfServiceLevel,
                Options.Mqtt.CleanSession);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "连接到MQTT Broker失败");
            return false;
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public override async Task DisconnectAsync()
    {
        try
        {
            if (_mqttClient != null && IsConnected)
            {
                await _mqttClient.DisconnectAsync();
                Logger.LogInformation("已断开与MQTT Broker的连接");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "断开连接时发生异常");
        }
    }

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    public override async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        ValidateParcelId(parcelId);

        // 尝试连接（如果未连接）
        if (!await EnsureConnectedAsync(cancellationToken))
        {
            Logger.LogError("[上游通信-发送] MQTT通道无法连接 | ParcelId={ParcelId}", parcelId);
            return false;
        }

        try
        {
            var notification = new ParcelDetectionNotification 
            { 
                ParcelId = parcelId,
                DetectionTime = SystemClock.LocalNowOffset
            };
            var notificationJson = JsonSerializer.Serialize(notification);
            
            var qosLevel = GetQosLevel();

            // 记录发送的完整消息内容（日志级别检查以避免不必要的字符串操作）
            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(
                    "[上游通信-发送] MQTT通道发送包裹检测通知 | ParcelId={ParcelId} | Topic={Topic} | QoS={QoS} | 消息内容={MessageContent}",
                    parcelId,
                    _detectionTopic,
                    Options.Mqtt.QualityOfServiceLevel,
                    notificationJson);
            }

            var messageBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(_detectionTopic)
                .WithPayload(notificationJson)
                .WithQualityOfServiceLevel(qosLevel);

            if (Options.Mqtt.MessageExpiryInterval > 0)
            {
                messageBuilder.WithMessageExpiryInterval((uint)Options.Mqtt.MessageExpiryInterval);
            }

            var message = messageBuilder.Build();

            await _mqttClient!.PublishAsync(message, cancellationToken);

            Logger.LogInformation(
                "[上游通信-发送完成] MQTT通道成功发送包裹检测通知 | ParcelId={ParcelId} | Topic={Topic}",
                parcelId,
                _detectionTopic);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-发送] MQTT通道发送包裹检测通知失败 | ParcelId={ParcelId}",
                parcelId);
            return false;
        }
    }

    /// <summary>
    /// 通知RuleEngine包裹已完成落格
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 新增方法，发送落格完成通知（fire-and-forget）
    /// </remarks>
    public override async Task<bool> NotifySortingCompletedAsync(
        SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // 尝试连接（如果未连接）
        if (!await EnsureConnectedAsync(cancellationToken))
        {
            Logger.LogError(
                "[上游通信-发送] MQTT通道无法连接 | ParcelId={ParcelId}",
                notification.ParcelId);
            return false;
        }

        try
        {
            var dto = new SortingCompletedNotificationDto
            {
                ParcelId = notification.ParcelId,
                ActualChuteId = notification.ActualChuteId,
                CompletedAt = notification.CompletedAt,
                IsSuccess = notification.IsSuccess,
                FailureReason = notification.FailureReason
            };

            var notificationJson = JsonSerializer.Serialize(dto);
            var qosLevel = GetQosLevel();

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(
                    "[上游通信-发送] MQTT通道发送落格完成通知 | ParcelId={ParcelId} | ChuteId={ChuteId} | Topic={Topic} | IsSuccess={IsSuccess} | 消息内容={MessageContent}",
                    notification.ParcelId,
                    notification.ActualChuteId,
                    _completionTopic,
                    notification.IsSuccess,
                    notificationJson);
            }

            var messageBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(_completionTopic)
                .WithPayload(notificationJson)
                .WithQualityOfServiceLevel(qosLevel);

            if (Options.Mqtt.MessageExpiryInterval > 0)
            {
                messageBuilder.WithMessageExpiryInterval((uint)Options.Mqtt.MessageExpiryInterval);
            }

            var message = messageBuilder.Build();

            await _mqttClient!.PublishAsync(message, cancellationToken);

            Logger.LogInformation(
                "[上游通信-发送完成] MQTT通道成功发送落格完成通知 | ParcelId={ParcelId} | Topic={Topic}",
                notification.ParcelId,
                _completionTopic);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-发送] MQTT通道发送落格完成通知失败 | ParcelId={ParcelId}",
                notification.ParcelId);
            return false;
        }
    }

    /// <summary>
    /// 获取QoS级别
    /// </summary>
    private MQTTnet.Protocol.MqttQualityOfServiceLevel GetQosLevel()
    {
        return Options.Mqtt.QualityOfServiceLevel switch
        {
            0 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
            2 => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
            _ => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
        };
    }

    /// <summary>
    /// 处理接收到的MQTT消息
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 更新以使用新的 AssignedAt 字段和 DWS 数据
    /// </remarks>
    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            
            // 记录接收到的完整消息内容
            Logger.LogInformation(
                "[上游通信-接收] MQTT通道收到消息 | Topic={Topic} | 消息内容={MessageContent}",
                args.ApplicationMessage.Topic,
                payload);

            // 尝试解析为格口分配通知
            var notification = JsonSerializer.Deserialize<ChuteAssignmentEventArgs>(payload);

            if (notification != null)
            {
                Logger.LogInformation(
                    "[上游通信-接收] MQTT通道收到格口分配通知 | ParcelId={ParcelId} | ChuteId={ChuteId} | Topic={Topic} | 消息内容={MessageContent}",
                    notification.ParcelId,
                    notification.ChuteId,
                    args.ApplicationMessage.Topic,
                    payload);

                // PR-UPSTREAM02: 使用共享方法转换 DWS 数据
                OnChuteAssignmentReceived(
                    notification.ParcelId,
                    notification.ChuteId,
                    notification.AssignedAt,
                    notification.DwsPayload,
                    notification.Metadata);
            }
            else
            {
                Logger.LogWarning(
                    "[上游通信-接收] MQTT通道无法解析消息为格口分配通知 | Topic={Topic} | 消息内容={MessageContent}",
                    args.ApplicationMessage.Topic,
                    payload);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-接收] MQTT通道处理消息时发生异常 | Topic={Topic}",
                args.ApplicationMessage.Topic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放托管和非托管资源
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            _mqttClient?.Dispose();
        }

        base.Dispose(disposing);
    }
}

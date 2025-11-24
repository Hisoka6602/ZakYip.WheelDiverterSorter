using ZakYip.WheelDiverterSorter.Communication.Models;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于MQTT的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供轻量级IoT消息协议，支持QoS保证
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
        RuleEngineConnectionOptions options,
        ISystemClock systemClock) : base(logger, options, systemClock)
    {
        if (string.IsNullOrWhiteSpace(options.MqttBroker))
        {
            throw new ArgumentException("MQTT Broker地址不能为空", nameof(options));
        }

        _detectionTopic = $"{options.MqttTopic}/detection";
        _assignmentTopic = $"{options.MqttTopic}/assignment";

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
            Logger.LogError("无法连接到MQTT Broker，无法发送包裹检测通知");
            return false;
        }

        try
        {
            Logger.LogDebug("向RuleEngine发送包裹检测通知: {ParcelId}", parcelId);

            var notification = new ParcelDetectionNotification 
            { 
                ParcelId = parcelId,
                DetectionTime = SystemClock.LocalNowOffset
            };
            var notificationJson = JsonSerializer.Serialize(notification);
            
            var qosLevel = GetQosLevel();

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

            Logger.LogInformation("成功发送包裹检测通知: {ParcelId}", parcelId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "发送包裹检测通知失败: {ParcelId}", parcelId);
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
    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            
            // 尝试解析为格口分配通知
            var notification = JsonSerializer.Deserialize<ChuteAssignmentNotificationEventArgs>(payload);

            if (notification != null)
            {
                Logger.LogDebug("收到包裹 {ParcelId} 的格口分配: {ChuteId}", 
                    notification.ParcelId, notification.ChuteId);
                
                // 触发事件
                OnChuteAssignmentReceived(notification);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "处理MQTT消息时发生异常");
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

namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// MQTT协议配置选项
/// </summary>
public class MqttOptions
{
    /// <summary>
    /// MQTT服务质量等级
    /// </summary>
    /// <remarks>
    /// 0 = At most once (最多一次，可能丢失)
    /// 1 = At least once (至少一次，可能重复) - 默认
    /// 2 = Exactly once (恰好一次，最可靠但最慢)
    /// </remarks>
    public int QualityOfServiceLevel { get; set; } = 1;

    /// <summary>
    /// 是否使用Clean Session
    /// </summary>
    /// <remarks>
    /// true = 不保留会话状态（默认）
    /// false = 保留会话状态和订阅
    /// </remarks>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// 会话保持时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认3600秒（1小时）。0表示连接断开后立即清理会话
    /// </remarks>
    public int SessionExpiryInterval { get; set; } = 3600;

    /// <summary>
    /// 消息保留时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认0（不保留）。用于保留最后一条消息给新订阅者
    /// </remarks>
    public int MessageExpiryInterval { get; set; } = 0;

    /// <summary>
    /// MQTT客户端ID前缀
    /// </summary>
    /// <remarks>
    /// 默认"WheelDiverter"。完整ID为：前缀_GUID
    /// </remarks>
    public string ClientIdPrefix { get; set; } = "WheelDiverter";
}

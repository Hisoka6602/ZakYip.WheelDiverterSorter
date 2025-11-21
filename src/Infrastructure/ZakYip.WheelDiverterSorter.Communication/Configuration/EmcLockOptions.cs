using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// EMC资源锁配置选项
/// </summary>
public class EmcLockOptions
{
    /// <summary>
    /// 是否启用EMC分布式锁
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// 实例ID（如果为空，则自动生成）
    /// </summary>
    public string? InstanceId { get; set; }
    
    /// <summary>
    /// 通信方式（使用与RuleEngine相同的通信配置）
    /// </summary>
    public CommunicationMode CommunicationMode { get; set; } = CommunicationMode.Tcp;
    
    /// <summary>
    /// TCP服务器地址（用于EMC锁协调）
    /// 格式：host:port
    /// </summary>
    public string TcpServer { get; set; } = "localhost:9000";
    
    /// <summary>
    /// SignalR Hub URL（用于EMC锁协调）
    /// </summary>
    public string SignalRHubUrl { get; set; } = "http://localhost:5001/emclock";
    
    /// <summary>
    /// MQTT Broker地址（用于EMC锁协调）
    /// </summary>
    public string MqttBroker { get; set; } = "localhost";
    
    /// <summary>
    /// MQTT端口
    /// </summary>
    public int MqttPort { get; set; } = 1883;
    
    /// <summary>
    /// MQTT主题前缀
    /// </summary>
    public string MqttTopicPrefix { get; set; } = "emc/lock";
    
    /// <summary>
    /// 默认超时时间（毫秒）
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// 心跳间隔（毫秒）- 用于检测实例是否在线
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 3000;
    
    /// <summary>
    /// 自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = true;
    
    /// <summary>
    /// 重连间隔（毫秒）
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;
}

namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// 通信模式
/// </summary>
public enum CommunicationMode
{
    /// <summary>
    /// HTTP REST API（仅用于测试，生产环境禁用）
    /// </summary>
    Http,

    /// <summary>
    /// TCP Socket（推荐生产环境）
    /// </summary>
    Tcp,

    /// <summary>
    /// SignalR（推荐生产环境）
    /// </summary>
    SignalR,

    /// <summary>
    /// MQTT（推荐生产环境）
    /// </summary>
    Mqtt
}

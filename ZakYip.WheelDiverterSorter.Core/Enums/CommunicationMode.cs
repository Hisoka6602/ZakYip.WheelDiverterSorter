using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums;

/// <summary>
/// 通信模式
/// </summary>
public enum CommunicationMode
{
    /// <summary>
    /// HTTP REST API（仅用于测试，生产环境禁用）
    /// </summary>
    [Description("HTTP REST API")]
    Http,

    /// <summary>
    /// TCP Socket（推荐生产环境）
    /// </summary>
    [Description("TCP Socket")]
    Tcp,

    /// <summary>
    /// SignalR（推荐生产环境）
    /// </summary>
    [Description("SignalR")]
    SignalR,

    /// <summary>
    /// MQTT（推荐生产环境）
    /// </summary>
    [Description("MQTT")]
    Mqtt
}

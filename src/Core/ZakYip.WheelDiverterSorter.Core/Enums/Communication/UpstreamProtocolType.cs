using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Communication;

/// <summary>
/// 上游协议类型
/// </summary>
/// <remarks>
/// 用于标识上游通讯协议适配器的类型。
/// 此枚举替代原来的 string ProtocolName 属性，确保类型安全。
/// </remarks>
public enum UpstreamProtocolType
{
    /// <summary>
    /// 默认协议（通用实现）
    /// </summary>
    [Description("默认协议")]
    Default,

    /// <summary>
    /// TCP 协议
    /// </summary>
    [Description("TCP 协议")]
    Tcp,

    /// <summary>
    /// HTTP REST API 协议
    /// </summary>
    [Description("HTTP REST API")]
    Http,

    /// <summary>
    /// SignalR 协议
    /// </summary>
    [Description("SignalR")]
    SignalR,

    /// <summary>
    /// MQTT 协议
    /// </summary>
    [Description("MQTT")]
    Mqtt
}

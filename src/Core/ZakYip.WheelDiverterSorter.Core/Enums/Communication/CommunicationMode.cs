using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Communication;

/// <summary>
/// 通信模式
/// </summary>
/// <remarks>
/// <para>PR-UPSTREAM01: 移除 HTTP 模式，仅保留 TCP/SignalR/MQTT。</para>
/// <para>默认使用 TCP 模式。</para>
/// <para>禁止新增 HTTP 或 REST 风格协议，所有上游通信必须基于 IUpstreamRoutingClient 接口。</para>
/// </remarks>
public enum CommunicationMode
{
    /// <summary>
    /// TCP Socket（默认，推荐生产环境）
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

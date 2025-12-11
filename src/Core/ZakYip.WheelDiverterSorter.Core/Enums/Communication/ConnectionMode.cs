using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Communication;

/// <summary>
/// 连接模式（客户端或服务端）- 通用通信层枚举
/// </summary>
/// <remarks>
/// <para>此枚举定义通用的通信连接模式，用于所有通信协议（TCP, SignalR, MQTT 等）。</para>
/// <para>⚠️ 注意：此枚举与 <see cref="ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors.ShuDiNiaoMode"/> 语义相同但用途不同：</para>
/// <list type="bullet">
/// <item><description><b>ConnectionMode</b>：通用通信层枚举，用于上游通信配置</description></item>
/// <item><description><b>ShuDiNiaoMode</b>：数递鸟厂商专用枚举，用于摆轮设备通信配置</description></item>
/// </list>
/// <para>两个枚举保持独立是为了分离通用通信层与厂商特定实现的关注点。</para>
/// </remarks>
public enum ConnectionMode
{
    /// <summary>
    /// 客户端模式 - 主动连接到服务端
    /// </summary>
    [Description("客户端模式")]
    Client,

    /// <summary>
    /// 服务端模式 - 等待客户端连接
    /// </summary>
    [Description("服务端模式")]
    Server
}

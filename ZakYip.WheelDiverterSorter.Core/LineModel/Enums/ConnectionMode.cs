using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums;

/// <summary>
/// 连接模式（客户端或服务端）
/// </summary>
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

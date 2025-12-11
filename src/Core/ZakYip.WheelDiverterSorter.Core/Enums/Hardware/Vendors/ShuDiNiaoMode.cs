using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

/// <summary>
/// 数递鸟通信模式（厂商专用）
/// </summary>
/// <remarks>
/// <para>此枚举用于配置数递鸟品牌摆轮设备的通信模式。</para>
/// <para>⚠️ 注意：此枚举与 <see cref="ZakYip.WheelDiverterSorter.Core.Enums.Communication.ConnectionMode"/> 语义相同但用途不同：</para>
/// <list type="bullet">
/// <item><description><b>ShuDiNiaoMode</b>：数递鸟厂商专用枚举，用于摆轮设备通信配置</description></item>
/// <item><description><b>ConnectionMode</b>：通用通信层枚举，用于上游通信配置</description></item>
/// </list>
/// <para>两个枚举保持独立是为了分离通用通信层与厂商特定实现的关注点。</para>
/// </remarks>
public enum ShuDiNiaoMode
{
    /// <summary>
    /// 客户端模式：系统主动连接到摆轮设备（默认）
    /// </summary>
    [Description("客户端模式")]
    Client = 0,

    /// <summary>
    /// 服务端模式：系统监听设备连接
    /// </summary>
    [Description("服务端模式")]
    Server = 1
}

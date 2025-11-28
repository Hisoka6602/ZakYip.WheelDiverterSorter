using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// S7-1200/1500 PLC 配置选项
/// </summary>
public class S7Options
{
    /// <summary>
    /// PLC IP地址
    /// </summary>
    public string IpAddress { get; set; } = "192.168.0.1";

    /// <summary>
    /// PLC 机架号（默认0）
    /// </summary>
    public short Rack { get; set; } = 0;

    /// <summary>
    /// PLC 插槽号（S7-1200通常为1，S7-1500通常为1）
    /// </summary>
    public short Slot { get; set; } = 1;

    /// <summary>
    /// CPU 类型
    /// </summary>
    public S7CpuType CpuType { get; set; } = S7CpuType.S71200;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 5000;

    /// <summary>
    /// 读写超时时间（毫秒）
    /// </summary>
    public int ReadWriteTimeout { get; set; } = 2000;

    /// <summary>
    /// 重连尝试次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// 重连延迟时间（毫秒）
    /// </summary>
    public int ReconnectDelay { get; set; } = 1000;

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public List<S7DiverterConfigDto> Diverters { get; set; } = new();
}

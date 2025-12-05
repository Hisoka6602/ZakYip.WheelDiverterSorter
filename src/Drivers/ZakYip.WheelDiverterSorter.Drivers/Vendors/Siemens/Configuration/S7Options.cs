using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

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
    /// 是否启用连接健康监控
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 连接失败阈值（连续失败多少次触发重连）
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// 是否启用性能统计
    /// </summary>
    public bool EnablePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// 是否使用指数退避重连
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// 最大退避延迟（毫秒）
    /// </summary>
    public int MaxBackoffDelay { get; set; } = 30000;
}

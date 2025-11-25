using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 日志配置模型
/// </summary>
/// <remarks>
/// 存储各类日志的开关配置，用于控制哪些日志需要输出
/// 异常日志始终输出，不受开关控制
/// </remarks>
public class LoggingConfiguration
{
    /// <summary>
    /// 配置ID（LiteDB自动生成）
    /// </summary>
    [BsonId]
    public int Id { get; set; }

    /// <summary>
    /// 配置名称（唯一标识符）
    /// </summary>
    /// <remarks>
    /// 使用固定值 "logging" 确保只有一条日志配置记录
    /// </remarks>
    public string ConfigName { get; set; } = "logging";

    /// <summary>
    /// 是否启用包裹生命周期日志
    /// </summary>
    /// <remarks>
    /// 记录包裹从创建到完成的完整生命周期事件
    /// </remarks>
    public bool EnableParcelLifecycleLog { get; set; } = true;

    /// <summary>
    /// 是否启用包裹追踪日志
    /// </summary>
    /// <remarks>
    /// 记录包裹的详细追踪信息，用于调试和分析
    /// </remarks>
    public bool EnableParcelTraceLog { get; set; } = true;

    /// <summary>
    /// 是否启用路径执行日志
    /// </summary>
    /// <remarks>
    /// 记录分拣路径的生成和执行过程
    /// </remarks>
    public bool EnablePathExecutionLog { get; set; } = true;

    /// <summary>
    /// 是否启用通信日志
    /// </summary>
    /// <remarks>
    /// 记录与上游规则引擎的通信过程
    /// </remarks>
    public bool EnableCommunicationLog { get; set; } = true;

    /// <summary>
    /// 是否启用硬件驱动日志
    /// </summary>
    /// <remarks>
    /// 记录硬件设备（摆轮、传感器等）的操作日志
    /// </remarks>
    public bool EnableDriverLog { get; set; } = true;

    /// <summary>
    /// 是否启用性能监控日志
    /// </summary>
    /// <remarks>
    /// 记录系统性能指标和监控数据
    /// </remarks>
    public bool EnablePerformanceLog { get; set; } = true;

    /// <summary>
    /// 是否启用告警日志
    /// </summary>
    /// <remarks>
    /// 记录系统告警事件（非异常级别告警）
    /// </remarks>
    public bool EnableAlarmLog { get; set; } = true;

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    /// <remarks>
    /// 记录调试级别的详细日志信息
    /// </remarks>
    public bool EnableDebugLog { get; set; } = false;

    /// <summary>
    /// 配置版本号
    /// </summary>
    /// <remarks>
    /// 用于跟踪配置变更历史
    /// </remarks>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 创建时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在创建时通过 ISystemClock.LocalNow 设置，使用本地时间存储
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在更新时通过 ISystemClock.LocalNow 设置，使用本地时间存储
    /// </remarks>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 验证配置参数的有效性
    /// </summary>
    /// <returns>验证结果和错误消息</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        // 当前配置均为布尔类型，无需特殊验证
        return (true, null);
    }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static LoggingConfiguration GetDefault()
    {
        return new LoggingConfiguration
        {
            ConfigName = "logging",
            EnableParcelLifecycleLog = true,
            EnableParcelTraceLog = true,
            EnablePathExecutionLog = true,
            EnableCommunicationLog = true,
            EnableDriverLog = true,
            EnablePerformanceLog = true,
            EnableAlarmLog = true,
            EnableDebugLog = false,
            Version = 1
            // CreatedAt 和 UpdatedAt 由仓储在插入时通过 ISystemClock.LocalNow 设置
        };
    }
}

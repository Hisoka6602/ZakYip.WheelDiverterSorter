using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 日志配置服务接口
/// </summary>
/// <remarks>
/// 负责日志配置的业务逻辑，包括查询、更新、重置等操作
/// </remarks>
public interface ILoggingConfigService
{
    /// <summary>
    /// 获取当前日志配置
    /// </summary>
    /// <returns>日志配置</returns>
    LoggingConfiguration GetLoggingConfig();

    /// <summary>
    /// 获取默认日志配置模板
    /// </summary>
    /// <returns>默认配置</returns>
    LoggingConfiguration GetDefaultTemplate();

    /// <summary>
    /// 更新日志配置
    /// </summary>
    /// <param name="request">配置请求</param>
    /// <returns>更新结果</returns>
    Task<LoggingConfigUpdateResult> UpdateLoggingConfigAsync(UpdateLoggingConfigCommand request);

    /// <summary>
    /// 重置日志配置为默认值
    /// </summary>
    /// <returns>重置后的配置</returns>
    Task<LoggingConfiguration> ResetLoggingConfigAsync();
}

/// <summary>
/// 日志配置更新结果
/// </summary>
public record LoggingConfigUpdateResult(
    bool Success,
    string? ErrorMessage,
    LoggingConfiguration? UpdatedConfig);

/// <summary>
/// 日志配置更新命令
/// </summary>
/// <remarks>
/// Application层的命令对象，由Host层映射
/// </remarks>
public record UpdateLoggingConfigCommand
{
    /// <summary>
    /// 是否启用包裹生命周期日志
    /// </summary>
    public bool EnableParcelLifecycleLog { get; init; } = true;

    /// <summary>
    /// 是否启用包裹追踪日志
    /// </summary>
    public bool EnableParcelTraceLog { get; init; } = true;

    /// <summary>
    /// 是否启用路径执行日志
    /// </summary>
    public bool EnablePathExecutionLog { get; init; } = true;

    /// <summary>
    /// 是否启用通信日志
    /// </summary>
    public bool EnableCommunicationLog { get; init; } = true;

    /// <summary>
    /// 是否启用硬件驱动日志
    /// </summary>
    public bool EnableDriverLog { get; init; } = true;

    /// <summary>
    /// 是否启用性能监控日志
    /// </summary>
    public bool EnablePerformanceLog { get; init; } = true;

    /// <summary>
    /// 是否启用告警日志
    /// </summary>
    public bool EnableAlarmLog { get; init; } = true;

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    public bool EnableDebugLog { get; init; } = false;
}

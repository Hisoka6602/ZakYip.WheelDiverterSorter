using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Application.Services;

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
    Task<LoggingConfigUpdateResult> UpdateLoggingConfigAsync(LoggingConfigRequest request);

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

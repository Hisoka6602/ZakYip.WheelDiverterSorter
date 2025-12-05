using System.Text.Json;

namespace ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;

/// <summary>
/// 配置审计日志接口
/// </summary>
/// <remarks>
/// 用于记录所有配置修改操作的审计日志，包括修改前后的内容
/// </remarks>
public interface IConfigurationAuditLogger
{
    /// <summary>
    /// 记录配置更新审计日志
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configName">配置名称</param>
    /// <param name="operationType">操作类型（Update/Reset）</param>
    /// <param name="beforeConfig">修改前的配置</param>
    /// <param name="afterConfig">修改后的配置</param>
    /// <param name="operatorInfo">操作者信息（可选）</param>
    void LogConfigurationChange<T>(
        string configName,
        string operationType,
        T? beforeConfig,
        T? afterConfig,
        string? operatorInfo = null);
}

using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Execution.SelfTest;

/// <summary>
/// 配置验证器接口
/// </summary>
public interface IConfigValidator
{
    /// <summary>
    /// 验证系统配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>配置健康状态</returns>
    Task<ConfigHealthStatus> ValidateAsync(CancellationToken cancellationToken = default);
}

using ZakYip.WheelDiverterSorter.Core.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Core.Runtime.Health;

/// <summary>
/// 驱动器自检接口
/// </summary>
/// <remarks>
/// 为关键驱动提供自检能力，用于系统启动时验证硬件连通性。
/// </remarks>
public interface IDriverSelfTest
{
    /// <summary>
    /// 驱动器名称
    /// </summary>
    string DriverName { get; }

    /// <summary>
    /// 执行自检
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>驱动器健康状态</returns>
    /// <remarks>
    /// 自检应使用"安全读"方式，避免触发实际动作。
    /// 失败时提供清晰的中文错误消息。
    /// </remarks>
    Task<DriverHealthStatus> RunSelfTestAsync(CancellationToken cancellationToken = default);
}

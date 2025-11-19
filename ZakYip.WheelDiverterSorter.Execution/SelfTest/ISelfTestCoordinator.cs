using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Execution.SelfTest;

/// <summary>
/// 系统自检协调器接口
/// </summary>
/// <remarks>
/// 编排驱动器、上游系统和配置的自检流程。
/// </remarks>
public interface ISelfTestCoordinator
{
    /// <summary>
    /// 执行系统自检
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>系统自检报告</returns>
    Task<SystemSelfTestReport> RunAsync(CancellationToken cancellationToken = default);
}

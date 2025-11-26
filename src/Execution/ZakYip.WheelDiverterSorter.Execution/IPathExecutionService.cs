using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 统一的路径执行服务接口
/// </summary>
/// <remarks>
/// <para>此接口定义了分拣系统中路径执行的完整生命周期，包括：</para>
/// <list type="bullet">
/// <item>遍历路径段并调用摆轮驱动</item>
/// <item>执行TTL超时检测</item>
/// <item>统一失败处理（通过IPathFailureHandler）</item>
/// <item>指标采集（路径执行时间、失败次数等）</item>
/// </list>
/// <para><strong>设计目标：</strong>消除重复的路径执行代码，确保生产逻辑和仿真逻辑使用相同的执行管线。</para>
/// <para><strong>失败处理：</strong>所有失败场景必须通过IPathFailureHandler处理，不允许在调用方自行拼装异常路径。</para>
/// </remarks>
public interface IPathExecutionService
{
    /// <summary>
    /// 执行摆轮路径
    /// </summary>
    /// <param name="parcelId">包裹标识</param>
    /// <param name="path">要执行的完整摆轮路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>路径执行结果</returns>
    /// <remarks>
    /// <para>执行流程：</para>
    /// <list type="number">
    /// <item>按Segment顺序调用摆轮驱动</item>
    /// <item>对每个段应用TTL超时检测</item>
    /// <item>失败时调用IPathFailureHandler，返回备用路径</item>
    /// <item>成功时返回目标格口</item>
    /// </list>
    /// <para>此方法不抛出异常，所有异常都会被捕获并转换为失败结果。</para>
    /// </remarks>
    Task<PathExecutionResult> ExecutePathAsync(
        long parcelId,
        SwitchingPath path,
        CancellationToken cancellationToken = default);
}

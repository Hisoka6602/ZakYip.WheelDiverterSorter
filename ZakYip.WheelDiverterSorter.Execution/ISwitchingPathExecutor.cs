using ZakYip.WheelDiverterSorter.Core.LineModel;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 按顺序执行摆轮路径段的执行器接口
/// </summary>
/// <remarks>
/// <para>执行器职责：</para>
/// <list type="number">
/// <item>接收一条完整的 <see cref="SwitchingPath"/>，按段顺序执行</item>
/// <item>在执行过程中强制执行"单段路由极限时间（TTL）"约束：
///     若当前段在 <see cref="SwitchingPathSegment.TtlMilliseconds"/> 
///     指定的时间内未完成，立即判定为执行失败</item>
/// <item>执行失败时，在 <see cref="PathExecutionResult.ActualChuteId"/> 
///     中返回 <see cref="SwitchingPath.FallbackChuteId"/>（最终异常格口）</item>
/// </list>
/// <para><strong>重要：</strong>执行层必须对现场异常做好隔离，不能影响调用方。
/// 所有现场设备通信异常、超时异常等必须在执行器内部捕获并转换为
/// <see cref="PathExecutionResult"/>，确保方法始终正常返回，不抛出异常。</para>
/// <para><strong>异常格口说明：</strong>当路径执行过程中任意一段失败时，
/// 执行器必须将包裹引导到 <see cref="SwitchingPath.FallbackChuteId"/> 指定的异常格口。
/// 这样可以防止包裹滞留造成输送线堵塞，与"单段TTL"机制配合使用，确保系统的高可用性。
/// 注意：如果旧代码中存在其他异常口处理逻辑（如硬编码的异常口ID），应统一使用本字段，删除旧的实现。</para>
/// </remarks>
public interface ISwitchingPathExecutor
{
    /// <summary>
    /// 异步执行摆轮路径
    /// </summary>
    /// <param name="path">要执行的完整摆轮路径</param>
    /// <param name="cancellationToken">用于取消操作的令牌</param>
    /// <returns>
    /// 返回路径执行结果。
    /// 注意：即使执行失败，此方法也应正常返回 <see cref="PathExecutionResult"/>，
    /// 而不是抛出异常。
    /// </returns>
    /// <remarks>
    /// <para>执行流程说明：</para>
    /// <list type="number">
    /// <item>按 <see cref="SwitchingPathSegment.SequenceNumber"/> 顺序遍历所有段</item>
    /// <item>对于每一段：
    ///     <list type="bullet">
    ///     <item>下发摆轮动作指令到现场设备（具体实现由派生类完成）</item>
    ///     <item>等待该段完成，但不超过 <see cref="SwitchingPathSegment.TtlMilliseconds"/></item>
    ///     <item>如果超时未完成，立即判定失败，
    ///         返回 <see cref="SwitchingPath.FallbackChuteId"/> 作为实际格口</item>
    ///     </list>
    /// </item>
    /// <item>所有段都成功完成后，返回成功结果，
    ///     <see cref="PathExecutionResult.ActualChuteId"/> 应等于 
    ///     <see cref="SwitchingPath.TargetChuteId"/></item>
    /// </list>
    /// <para>异常处理：</para>
    /// <para>执行器必须捕获所有现场异常（设备通信失败、网络超时、设备故障等），
    /// 并将其转换为失败的 <see cref="PathExecutionResult"/>，
    /// 其中 <see cref="PathExecutionResult.ActualChuteId"/> 应设置为 
    /// <see cref="SwitchingPath.FallbackChuteId"/>。
    /// 调用方不应需要处理任何异常情况。</para>
    /// </remarks>
    Task<PathExecutionResult> ExecuteAsync(
        SwitchingPath path,
        CancellationToken cancellationToken = default);
}

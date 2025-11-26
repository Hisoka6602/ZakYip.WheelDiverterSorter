using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;

/// <summary>
/// 分拣异常处理器接口
/// </summary>
/// <remarks>
/// 统一处理分拣流程中的所有异常场景，避免重复代码：
/// <list type="bullet">
///   <item>路由超时 → 异常格口</item>
///   <item>路由结果不存在于拓扑 → 异常格口</item>
///   <item>路径生成失败 → 异常格口</item>
///   <item>路径执行失败 → 异常格口</item>
///   <item>路径健康检查失败 → 异常格口</item>
///   <item>二次超载检查失败 → 异常格口</item>
/// </list>
/// </remarks>
public interface ISortingExceptionHandler
{
    /// <summary>
    /// 尝试生成到异常格口的路径
    /// </summary>
    /// <param name="exceptionChuteId">异常格口ID</param>
    /// <param name="parcelId">包裹ID（用于日志）</param>
    /// <param name="reason">路由到异常格口的原因</param>
    /// <returns>异常格口路径，如果连异常格口路径都无法生成则返回 null</returns>
    SwitchingPath? GenerateExceptionPath(long exceptionChuteId, long parcelId, string reason);
    
    /// <summary>
    /// 处理路径生成完全失败的情况（连异常格口路径都无法生成）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="targetChuteId">原始目标格口ID</param>
    /// <param name="exceptionChuteId">异常格口ID</param>
    /// <param name="reason">失败原因</param>
    /// <returns>失败结果</returns>
    SortingResult CreatePathGenerationFailureResult(
        long parcelId, 
        long targetChuteId, 
        long exceptionChuteId, 
        string reason);
}

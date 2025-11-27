using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;

namespace ZakYip.WheelDiverterSorter.Execution.Abstractions;

/// <summary>
/// 拥堵数据收集器接口
/// </summary>
/// <remarks>
/// 收集系统当前拥堵指标快照，用于超载检测和路径规划决策。
/// 
/// <para><b>职责</b>：</para>
/// <list type="bullet">
///   <item>记录包裹进入系统的时间</item>
///   <item>记录包裹完成分拣的时间</item>
///   <item>收集当前拥堵快照（在途包裹数、平均延迟等）</item>
/// </list>
/// </remarks>
public interface ICongestionDataCollector
{
    /// <summary>
    /// 记录包裹进入系统
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="entryTime">进入时间</param>
    void RecordParcelEntry(long parcelId, DateTime entryTime);

    /// <summary>
    /// 记录包裹完成分拣
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="completionTime">完成时间</param>
    /// <param name="isSuccess">是否成功</param>
    void RecordParcelCompletion(long parcelId, DateTime completionTime, bool isSuccess);

    /// <summary>
    /// 收集当前拥堵快照
    /// </summary>
    /// <returns>拥堵快照</returns>
    CongestionSnapshot CollectSnapshot();

    /// <summary>
    /// 获取当前在途包裹数
    /// </summary>
    /// <returns>在途包裹数</returns>
    int GetInFlightCount();
}

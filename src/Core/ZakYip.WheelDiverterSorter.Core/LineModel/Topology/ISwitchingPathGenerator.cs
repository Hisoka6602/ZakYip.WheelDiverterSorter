using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

/// <summary>
/// 摆轮路径生成器接口，负责根据目标格口生成摆轮指令序列
/// </summary>
public interface ISwitchingPathGenerator
{
    /// <summary>
    /// 根据目标格口生成摆轮路径
    /// </summary>
    /// <param name="targetChuteId">目标格口标识（数字ID）</param>
    /// <returns>
    /// 生成的摆轮路径，如果目标格口无法映射到任意摆轮组合则返回null。
    /// 当返回null时，包裹将走异常口处理流程。
    /// </returns>
    SwitchingPath? GeneratePath(long targetChuteId);
    
    /// <summary>
    /// 根据目标格口生成位置索引队列任务列表（Phase 4）
    /// </summary>
    /// <param name="parcelId">包裹ID（长整型）</param>
    /// <param name="targetChuteId">目标格口标识（数字ID）</param>
    /// <param name="createdAt">包裹创建时间</param>
    /// <returns>
    /// 队列任务列表，每个任务对应一个 positionIndex 的摆轮动作。
    /// 如果目标格口无法映射到任意摆轮组合则返回空列表。
    /// </returns>
    /// <remarks>
    /// <para>此方法用于 Position-Index 队列系统，生成的任务包含：</para>
    /// <list type="bullet">
    ///   <item>理论到达时间（基于线段传输时间累加计算）</item>
    ///   <item>超时阈值（从 ConveyorSegmentConfiguration 获取）</item>
    ///   <item>摆轮动作（Left/Right/Straight）</item>
    ///   <item>回退动作（默认 Straight）</item>
    /// </list>
    /// </remarks>
    List<PositionQueueItem> GenerateQueueTasks(
        long parcelId,
        long targetChuteId,
        DateTime createdAt);
}

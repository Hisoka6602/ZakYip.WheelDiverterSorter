using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Execution.Queues;

/// <summary>
/// Position-Index 队列管理器接口
/// </summary>
/// <remarks>
/// 管理每个 positionIndex 的独立 FIFO 队列。
/// 每个 positionIndex 对应一个摆轮位置的触发点（frontSensorId）。
/// </remarks>
public interface IPositionIndexQueueManager
{
    /// <summary>
    /// 将任务加入指定 position 的队列（追加到尾部）
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <param name="task">任务项</param>
    void EnqueueTask(int positionIndex, PositionQueueItem task);
    
    /// <summary>
    /// 将任务优先加入指定 position 的队列（插入到头部）
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <param name="task">任务项</param>
    /// <remarks>
    /// 用于超时包裹的后续 position 插入 Straight 任务。
    /// 因为超时包裹虽然超时，但仍排在后续包裹前面，需要优先处理。
    /// </remarks>
    void EnqueuePriorityTask(int positionIndex, PositionQueueItem task);
    
    /// <summary>
    /// 从指定 position 的队列中取出任务（FIFO）
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <returns>任务项，如果队列为空则返回 null</returns>
    PositionQueueItem? DequeueTask(int positionIndex);
    
    /// <summary>
    /// 查看指定 position 队列的头部任务，但不移除
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <returns>队列头部任务，如果队列为空则返回 null</returns>
    PositionQueueItem? PeekTask(int positionIndex);
    
    /// <summary>
    /// 查看指定 position 队列的第二个任务（下一个任务），但不移除
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <returns>队列第二个任务，如果队列中任务数少于2则返回 null</returns>
    /// <remarks>
    /// 用于超时/丢失判断：基于下一个包裹的 EarliestDequeueTime 来区分超时和丢失
    /// </remarks>
    PositionQueueItem? PeekNextTask(int positionIndex);
    
    /// <summary>
    /// 清空所有 position 的队列
    /// </summary>
    /// <remarks>
    /// 用于面板控制（停止/急停/复位）时清理所有状态。
    /// </remarks>
    void ClearAllQueues();
    
    /// <summary>
    /// 获取指定 position 的队列状态
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <returns>队列状态信息</returns>
    QueueStatus GetQueueStatus(int positionIndex);
    
    /// <summary>
    /// 获取所有 position 的队列状态
    /// </summary>
    /// <returns>所有队列的状态信息（Key: positionIndex, Value: QueueStatus）</returns>
    Dictionary<int, QueueStatus> GetAllQueueStatuses();
    
    /// <summary>
    /// 获取指定 position 队列的任务数量
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <returns>队列中的任务数量</returns>
    int GetQueueCount(int positionIndex);
    
    /// <summary>
    /// 检查指定 position 的队列是否为空
    /// </summary>
    /// <param name="positionIndex">Position Index</param>
    /// <returns>如果队列为空或不存在则返回 true</returns>
    bool IsQueueEmpty(int positionIndex);
    
    /// <summary>
    /// 从所有 position 队列中移除指定包裹的所有任务
    /// </summary>
    /// <param name="parcelId">要移除的包裹ID</param>
    /// <returns>移除的任务数量</returns>
    /// <remarks>
    /// 用于包裹丢失场景：当包裹物理丢失时，需要从所有队列中清理该包裹的任务，避免影响后续包裹分拣。
    /// </remarks>
    int RemoveAllTasksForParcel(long parcelId);
    
    /// <summary>
    /// 将受影响包裹的所有任务方向改为直行（异常处理）
    /// </summary>
    /// <param name="lostParcelCreatedAt">丢失包裹的创建时间</param>
    /// <param name="detectionTime">丢失检测时间</param>
    /// <returns>受影响的包裹ID列表</returns>
    /// <remarks>
    /// 用于包裹丢失场景：当包裹丢失时，需要将"在丢失包裹创建之后、丢失检测之前创建的包裹"的所有任务方向改为直行。
    /// 这样可以确保这些包裹走向异常格口，而不影响在丢失包裹创建之前或丢失检测之后创建的包裹。
    /// </remarks>
    List<long> UpdateAffectedParcelsToStraight(DateTime lostParcelCreatedAt, DateTime detectionTime);
    
    /// <summary>
    /// 原地替换指定包裹在所有队列中的任务（用于上游格口分配响应）
    /// </summary>
    /// <param name="parcelId">要替换任务的包裹ID</param>
    /// <param name="newTasks">新的任务列表（按 PositionIndex 分组）</param>
    /// <returns>替换结果，包含成功替换的任务数和失败的Position索引</returns>
    /// <remarks>
    /// <para><b>核心功能</b>：当收到上游的格口分配通知时，原地替换队列中该包裹的任务，保持队列顺序不变，并清理幽灵任务。</para>
    /// 
    /// <para><b>问题场景</b>：</para>
    /// <list type="bullet">
    ///   <item>包裹P1、P2、P3按顺序创建，初始都使用异常格口999入队</item>
    ///   <item>上游响应顺序可能乱序：P2先返回（格口6），P1后返回（格口3）</item>
    ///   <item>如果简单"删除+重新入队"会导致队列顺序错乱：[P1, P3, P2]</item>
    ///   <item>正确做法：原地替换，保持顺序：[P1-新动作, P2-新动作, P3-异常]</item>
    /// </list>
    /// 
    /// <para><b>替换逻辑</b>：</para>
    /// <list type="number">
    ///   <item>遍历新任务列表中的每个Position，找到目标包裹的任务并替换</item>
    ///   <item>使用新任务的动作替换旧任务的动作（DiverterAction等字段）</item>
    ///   <item>保持任务在队列中的原位置不变</item>
    ///   <item>遍历所有其他Position，移除不在新路径中的该包裹任务（清理幽灵任务）</item>
    /// </list>
    /// 
    /// <para><b>幽灵任务清理</b>：</para>
    /// <example>
    /// 场景：包裹最初创建时目标异常口999（Position 0→5全直通），上游返回后需要在Position 2左转
    /// - 新路径：Position 0、1、2（共3个任务）
    /// - 旧路径：Position 0、1、2、3、4、5（共6个任务）
    /// - 操作：替换Position 0、1、2的任务，移除Position 3、4、5的任务（幽灵任务）
    /// - 原因：包裹在Position 2已落格，Position 3、4、5的任务永不执行，需清理防止队列积累
    /// </example>
    /// 
    /// <para><b>边界情况</b>：</para>
    /// <list type="bullet">
    ///   <item>任务已被出队执行 → 不操作该Position，记录到NotFoundPositions</item>
    ///   <item>新路径Position少于旧路径 → 替换重叠部分，移除多余Position的任务</item>
    ///   <item>新路径Position多于旧路径 → 替换已有部分，记录缺失Position到SkippedPositions</item>
    /// </list>
    /// </remarks>
    TaskReplacementResult ReplaceTasksInPlace(long parcelId, List<PositionQueueItem> newTasks);
    
    /// <summary>
    /// 原地更新指定位置队列中某个包裹的任务时间字段（用于动态时间修正）
    /// </summary>
    /// <param name="positionIndex">位置索引</param>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="updateFunc">更新函数，接收旧任务并返回更新后的任务</param>
    /// <returns>如果成功更新则返回 true，如果任务未找到或已出队则返回 false</returns>
    /// <remarks>
    /// <para><b>核心功能</b>：原地更新队列头部特定包裹的任务，避免"出队→修改→入队"导致的并发风险。</para>
    /// 
    /// <para><b>并发风险</b>：</para>
    /// <list type="bullet">
    ///   <item>如果使用"出队→修改→入队"模式，在出队和入队之间可能有其他包裹入队</item>
    ///   <item>这会导致FIFO顺序被破坏（新入队的包裹被插队到前面）</item>
    ///   <item>原地更新使用细粒度锁保护整个"查找→修改→替换"操作，确保原子性</item>
    /// </list>
    /// 
    /// <para><b>使用场景</b>：</para>
    /// <list type="bullet">
    ///   <item>动态更新下一个position的期望到达时间（避免误差累积）</item>
    ///   <item>修正时间窗口字段（ExpectedArrivalTime、EarliestDequeueTime、LostDetectionDeadline）</item>
    /// </list>
    /// 
    /// <para><b>实现要求</b>：</para>
    /// <list type="number">
    ///   <item>使用与其他队列操作一致的细粒度锁（per-Position locks）</item>
    ///   <item>确保"清空队列→查找并修改→放回队列"操作的原子性</item>
    ///   <item>仅更新队列头部的任务（因为动态时间修正只针对即将触发的包裹）</item>
    ///   <item>如果队列头部任务不是目标包裹，返回 false 不做修改</item>
    /// </list>
    /// </remarks>
    bool UpdateTaskInPlace(int positionIndex, long parcelId, Func<PositionQueueItem, PositionQueueItem> updateFunc);
}

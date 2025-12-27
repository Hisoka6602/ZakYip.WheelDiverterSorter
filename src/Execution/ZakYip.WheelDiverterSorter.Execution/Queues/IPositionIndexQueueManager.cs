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
}

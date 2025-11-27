namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 离散IO端口组接口 - 统一硬件能力层（HAL）
/// Discrete IO Group Interface - Unified Hardware Abstraction Layer (HAL)
/// </summary>
/// <remarks>
/// <para>表示一组离散数字IO端口的集合，通常对应一块IO板卡或模块。</para>
/// <para>此接口只描述"设备能做什么"，不包含拓扑信息。</para>
/// <para>厂商驱动需实现此接口以提供IO组的管理能力。</para>
/// </remarks>
public interface IDiscreteIoGroup
{
    /// <summary>
    /// IO组的设备名称/标识
    /// </summary>
    /// <remarks>
    /// 用于唯一标识此IO组，如 "Card0", "Module1" 等。
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// 此组包含的所有IO端口
    /// </summary>
    IReadOnlyList<IDiscreteIoPort> Ports { get; }

    /// <summary>
    /// 根据端口编号获取端口
    /// </summary>
    /// <param name="portNumber">端口编号</param>
    /// <returns>IO端口，如果不存在则返回null</returns>
    IDiscreteIoPort? GetPort(int portNumber);

    /// <summary>
    /// 批量读取多个端口的状态
    /// </summary>
    /// <param name="portNumbers">要读取的端口编号列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>端口编号与状态的字典</returns>
    Task<IReadOnlyDictionary<int, bool>> ReadBatchAsync(
        IEnumerable<int> portNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量设置多个端口的状态
    /// </summary>
    /// <param name="portStates">端口编号与目标状态的字典</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task WriteBatchAsync(
        IReadOnlyDictionary<int, bool> portStates,
        CancellationToken cancellationToken = default);
}

namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 离散IO端口接口 - 统一硬件能力层（HAL）
/// Discrete IO Port Interface - Unified Hardware Abstraction Layer (HAL)
/// </summary>
/// <remarks>
/// <para>表示单个离散数字IO端口的能力。</para>
/// <para>此接口只描述"设备能做什么"，不包含拓扑信息（如格口号、传感器逻辑名称等）。</para>
/// <para>厂商驱动需实现此接口以提供IO端口的读写能力。</para>
/// </remarks>
public interface IDiscreteIoPort
{
    /// <summary>
    /// 端口编号
    /// </summary>
    /// <remarks>
    /// 在设备级别的端口标识，如 0, 1, 2 等。
    /// 不包含逻辑命名，逻辑映射由拓扑层完成。
    /// </remarks>
    int PortNumber { get; }

    /// <summary>
    /// 设置端口电平状态
    /// </summary>
    /// <param name="isOn">目标电平状态（true=高电平/ON，false=低电平/OFF）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    Task SetAsync(bool isOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取端口当前电平状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前电平状态（true=高电平/ON，false=低电平/OFF）</returns>
    Task<bool> GetAsync(CancellationToken cancellationToken = default);
}

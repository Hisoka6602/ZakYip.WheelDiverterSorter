namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 传感器输入读取器接口
/// Sensor Input Reader Interface - Reads sensor states by logical point
/// </summary>
/// <remarks>
/// 此接口提供传感器状态的读取能力（按逻辑点位）。
/// 厂商驱动将物理传感器映射为逻辑点位后通过此接口暴露。
/// </remarks>
public interface ISensorInputReader
{
    /// <summary>
    /// 读取指定逻辑点位的传感器状态
    /// </summary>
    /// <param name="logicalPoint">逻辑点位编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>传感器状态（true=有信号/触发，false=无信号）</returns>
    Task<bool> ReadSensorAsync(int logicalPoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量读取多个逻辑点位的传感器状态
    /// </summary>
    /// <param name="logicalPoints">逻辑点位编号列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>点位编号与状态的字典</returns>
    Task<IDictionary<int, bool>> ReadSensorsAsync(IEnumerable<int> logicalPoints, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查传感器是否在线/可用
    /// </summary>
    /// <param name="logicalPoint">逻辑点位编号</param>
    /// <returns>传感器是否在线</returns>
    Task<bool> IsSensorOnlineAsync(int logicalPoint);
}

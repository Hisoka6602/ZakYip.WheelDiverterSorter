namespace ZakYip.WheelDiverterSorter.Core.Hardware.Devices;

/// <summary>
/// 心跳检测能力接口
/// </summary>
/// <remarks>
/// 摆轮驱动器可选实现此接口，以提供专用的心跳检测机制。
/// 如果驱动器不实现此接口，心跳监控将使用 Ping 作为后备方案。
/// </remarks>
public interface IHeartbeatCapable
{
    /// <summary>
    /// 执行心跳检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>心跳是否成功</returns>
    /// <remarks>
    /// 此方法应该是轻量级的，快速返回设备是否在线。
    /// 不同厂商可以根据自己的协议实现：
    /// - 发送心跳包并等待响应
    /// - 检查TCP连接状态
    /// - 查询设备状态寄存器
    /// </remarks>
    Task<bool> CheckHeartbeatAsync(CancellationToken cancellationToken = default);
}

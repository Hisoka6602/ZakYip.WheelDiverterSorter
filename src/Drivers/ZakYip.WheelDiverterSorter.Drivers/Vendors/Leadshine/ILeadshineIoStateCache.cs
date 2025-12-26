namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛IO状态缓存服务接口
/// Leadshine IO State Cache Service Interface
/// </summary>
/// <remarks>
/// 这是系统中唯一读取雷赛硬件IO状态的服务。
/// 所有其他组件必须通过此接口读取IO状态，禁止直接调用dmc_read_*函数。
/// 
/// 设计原则：
/// - 单一IO读取点：仅在后台服务中每10ms批量读取一次所有IO端口
/// - 零阻塞访问：其他组件读取缓存状态，不会触发硬件IO调用
/// - 实时性保证：10ms刷新周期确保传感器IO的实时性（这是整个项目的核心）
/// </remarks>
public interface ILeadshineIoStateCache
{
    /// <summary>
    /// 读取单个输入位的缓存状态
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <returns>位的值（true为高电平，false为低电平）</returns>
    /// <remarks>
    /// 此方法从内存缓存读取，不会触发硬件IO调用，因此是非阻塞的。
    /// </remarks>
    bool ReadInputBit(int bitIndex);

    /// <summary>
    /// 批量读取多个输入位的缓存状态
    /// </summary>
    /// <param name="bitIndices">位索引列表</param>
    /// <returns>位索引与状态的字典</returns>
    /// <remarks>
    /// 此方法从内存缓存读取，不会触发硬件IO调用，因此是非阻塞的。
    /// </remarks>
    IDictionary<int, bool> ReadInputBits(IEnumerable<int> bitIndices);

    /// <summary>
    /// 获取最后一次IO刷新的时间
    /// </summary>
    DateTimeOffset LastRefreshTime { get; }

    /// <summary>
    /// 获取IO缓存是否可用
    /// </summary>
    bool IsAvailable { get; }
}

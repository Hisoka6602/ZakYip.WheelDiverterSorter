namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

/// <summary>
/// 输入端口接口
/// </summary>
/// <remarks>
/// 本接口属于 Core 层，定义硬件输入端口的抽象契约。
/// 用于从硬件设备读取信号，由 Drivers 层实现。
/// </remarks>
public interface IInputPort
{
    /// <summary>
    /// 读取单个输入位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <returns>位的值（true为高电平，false为低电平）</returns>
    Task<bool> ReadAsync(int bitIndex);

    /// <summary>
    /// 批量读取多个输入位
    /// </summary>
    /// <param name="startBit">起始位索引</param>
    /// <param name="count">要读取的位数</param>
    /// <returns>位值数组</returns>
    Task<bool[]> ReadBatchAsync(int startBit, int count);
}

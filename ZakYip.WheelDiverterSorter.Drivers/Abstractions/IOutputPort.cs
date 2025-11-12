namespace ZakYip.WheelDiverterSorter.Drivers.Abstractions;

/// <summary>
/// 输出端口接口，用于向硬件设备写入控制信号
/// </summary>
public interface IOutputPort
{
    /// <summary>
    /// 写入单个输出位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <param name="value">值（true为高电平，false为低电平）</param>
    /// <returns>是否成功</returns>
    Task<bool> WriteAsync(int bitIndex, bool value);

    /// <summary>
    /// 批量写入多个输出位
    /// </summary>
    /// <param name="startBit">起始位索引</param>
    /// <param name="values">要写入的值数组</param>
    /// <returns>是否成功</returns>
    Task<bool> WriteBatchAsync(int startBit, bool[] values);
}

using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 输入端口基类，提供批量读取的默认实现
/// </summary>
public abstract class InputPortBase : IInputPort
{
    /// <summary>
    /// 读取单个输入位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <returns>位的值（true为高电平，false为低电平）</returns>
    public abstract Task<bool> ReadAsync(int bitIndex);

    /// <summary>
    /// 批量读取多个输入位（默认实现）
    /// </summary>
    /// <param name="startBit">起始位索引</param>
    /// <param name="count">要读取的位数</param>
    /// <returns>位值数组</returns>
    public virtual async Task<bool[]> ReadBatchAsync(int startBit, int count)
    {
        var results = new bool[count];
        
        for (int i = 0; i < count; i++)
        {
            results[i] = await ReadAsync(startBit + i);
        }

        return results;
    }
}

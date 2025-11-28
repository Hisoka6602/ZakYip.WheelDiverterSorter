using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 输出端口基类，提供批量写入的默认实现
/// </summary>
public abstract class OutputPortBase : IOutputPort
{
    /// <summary>
    /// 写入单个输出位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <param name="value">值（true为高电平，false为低电平）</param>
    /// <returns>是否成功</returns>
    public abstract Task<bool> WriteAsync(int bitIndex, bool value);

    /// <summary>
    /// 批量写入多个输出位（默认实现）
    /// </summary>
    /// <param name="startBit">起始位索引</param>
    /// <param name="values">要写入的值数组</param>
    /// <returns>是否成功</returns>
    public virtual async Task<bool> WriteBatchAsync(int startBit, bool[] values)
    {
        bool allSuccess = true;
        
        for (int i = 0; i < values.Length; i++)
        {
            var success = await WriteAsync(startBit + i, values[i]);
            allSuccess = allSuccess && success;
        }

        return allSuccess;
    }
}

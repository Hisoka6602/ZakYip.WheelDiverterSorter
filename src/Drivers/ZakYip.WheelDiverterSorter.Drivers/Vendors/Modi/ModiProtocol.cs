namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Modi;

/// <summary>
/// 莫迪摆轮控制命令枚举
/// </summary>
public enum ModiControlCommand : byte
{
    /// <summary>
    /// 停止
    /// </summary>
    Stop = 0x00,
    
    /// <summary>
    /// 左转
    /// </summary>
    TurnLeft = 0x01,
    
    /// <summary>
    /// 右转
    /// </summary>
    TurnRight = 0x02,
    
    /// <summary>
    /// 回中（直通）
    /// </summary>
    ReturnCenter = 0x03
}

/// <summary>
/// 莫迪摆轮通信协议
/// </summary>
/// <remarks>
/// 协议格式：
/// - 帧头: 0xAA
/// - 设备编号: 1字节
/// - 命令: 1字节
/// - 校验和: 1字节（设备编号 + 命令）
/// - 帧尾: 0x55
/// </remarks>
public static class ModiProtocol
{
    /// <summary>
    /// 帧头
    /// </summary>
    public const byte FrameHeader = 0xAA;
    
    /// <summary>
    /// 帧尾
    /// </summary>
    public const byte FrameTail = 0x55;
    
    /// <summary>
    /// 构建控制命令帧
    /// </summary>
    /// <param name="deviceId">设备编号</param>
    /// <param name="command">控制命令</param>
    /// <returns>命令帧字节数组</returns>
    public static byte[] BuildCommandFrame(int deviceId, ModiControlCommand command)
    {
        var deviceByte = (byte)(deviceId & 0xFF);
        var commandByte = (byte)command;
        var checksum = (byte)((deviceByte + commandByte) & 0xFF);
        
        return new byte[]
        {
            FrameHeader,
            deviceByte,
            commandByte,
            checksum,
            FrameTail
        };
    }
    
    /// <summary>
    /// 格式化字节数组为十六进制字符串（用于日志）
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <returns>十六进制字符串</returns>
    public static string FormatBytes(byte[] bytes)
    {
        return string.Join(" ", bytes.Select(b => $"0x{b:X2}"));
    }
    
    /// <summary>
    /// 验证响应帧
    /// </summary>
    /// <param name="response">响应字节数组</param>
    /// <param name="expectedDeviceId">期望的设备编号</param>
    /// <returns>是否有效</returns>
    public static bool ValidateResponse(byte[] response, int expectedDeviceId)
    {
        if (response == null || response.Length < 5)
        {
            return false;
        }
        
        // 检查帧头和帧尾
        if (response[0] != FrameHeader || response[^1] != FrameTail)
        {
            return false;
        }
        
        // 检查设备编号
        if (response[1] != (byte)(expectedDeviceId & 0xFF))
        {
            return false;
        }
        
        // 检查校验和
        var expectedChecksum = (byte)((response[1] + response[2]) & 0xFF);
        return response[3] == expectedChecksum;
    }
}

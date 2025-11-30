using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟协议解析测试
/// </summary>
/// <remarks>
/// 验证协议解析能正确处理合法和非法报文
/// </remarks>
public class ShuDiNiaoProtocolParseTests
{
    #region 设备状态解析测试

    [Fact(DisplayName = "解析设备待机状态应成功")]
    public void TryParseDeviceStatus_Standby_ShouldSucceed()
    {
        // Arrange - 51 52 57 51 51 50 FE
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0x50, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out var deviceAddress,
            out var deviceState);

        // Assert
        Assert.True(result);
        Assert.Equal(0x51, deviceAddress);
        Assert.Equal(ShuDiNiaoDeviceState.Standby, deviceState);
    }

    [Fact(DisplayName = "解析设备运行状态应成功")]
    public void TryParseDeviceStatus_Running_ShouldSucceed()
    {
        // Arrange - 51 52 57 52 51 51 FE (设备地址0x52)
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x52, 0x51, 0x51, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out var deviceAddress,
            out var deviceState);

        // Assert
        Assert.True(result);
        Assert.Equal(0x52, deviceAddress);
        Assert.Equal(ShuDiNiaoDeviceState.Running, deviceState);
    }

    [Fact(DisplayName = "解析设备急停状态应成功")]
    public void TryParseDeviceStatus_EmergencyStop_ShouldSucceed()
    {
        // Arrange
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x53, 0x51, 0x52, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out var deviceAddress,
            out var deviceState);

        // Assert
        Assert.True(result);
        Assert.Equal(0x53, deviceAddress);
        Assert.Equal(ShuDiNiaoDeviceState.EmergencyStop, deviceState);
    }

    [Fact(DisplayName = "解析设备故障状态应成功")]
    public void TryParseDeviceStatus_Fault_ShouldSucceed()
    {
        // Arrange
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x54, 0x51, 0x53, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out var deviceAddress,
            out var deviceState);

        // Assert
        Assert.True(result);
        Assert.Equal(0x54, deviceAddress);
        Assert.Equal(ShuDiNiaoDeviceState.Fault, deviceState);
    }

    #endregion

    #region 应答与完成解析测试

    [Theory(DisplayName = "解析各类应答消息应成功")]
    [InlineData(0x51, ShuDiNiaoResponseCode.RunAck)]
    [InlineData(0x52, ShuDiNiaoResponseCode.StopAck)]
    [InlineData(0x53, ShuDiNiaoResponseCode.TurnLeftAck)]
    [InlineData(0x54, ShuDiNiaoResponseCode.ReturnCenterAck)]
    [InlineData(0x55, ShuDiNiaoResponseCode.TurnRightAck)]
    public void TryParseResponse_AckMessages_ShouldSucceed(byte responseByte, ShuDiNiaoResponseCode expected)
    {
        // Arrange - 51 52 57 51 53 [响应码] FE
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, responseByte, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseResponse(
            frame,
            out var deviceAddress,
            out var responseCode);

        // Assert
        Assert.True(result);
        Assert.Equal(0x51, deviceAddress);
        Assert.Equal(expected, responseCode);
    }

    [Theory(DisplayName = "解析各类完成消息应成功")]
    [InlineData(0x56, ShuDiNiaoResponseCode.TurnLeftComplete)]
    [InlineData(0x57, ShuDiNiaoResponseCode.ReturnCenterComplete)]
    [InlineData(0x58, ShuDiNiaoResponseCode.TurnRightComplete)]
    public void TryParseResponse_CompleteMessages_ShouldSucceed(byte responseByte, ShuDiNiaoResponseCode expected)
    {
        // Arrange
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x52, 0x53, responseByte, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseResponse(
            frame,
            out var deviceAddress,
            out var responseCode);

        // Assert
        Assert.True(result);
        Assert.Equal(0x52, deviceAddress);
        Assert.Equal(expected, responseCode);
    }

    #endregion

    #region 非法报文测试

    [Fact(DisplayName = "帧长度错误应返回失败")]
    public void TryParseDeviceStatus_WrongLength_ShouldReturnFalse()
    {
        // Arrange - 只有6字节
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0x50 };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "起始字节1错误应返回失败")]
    public void TryParseDeviceStatus_WrongStartByte1_ShouldReturnFalse()
    {
        // Arrange - 起始字节1错误
        var frame = new byte[] { 0x50, 0x52, 0x57, 0x51, 0x51, 0x50, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "起始字节2错误应返回失败")]
    public void TryParseDeviceStatus_WrongStartByte2_ShouldReturnFalse()
    {
        // Arrange - 起始字节2错误
        var frame = new byte[] { 0x51, 0x53, 0x57, 0x51, 0x51, 0x50, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "长度字节错误应返回失败")]
    public void TryParseDeviceStatus_WrongLengthByte_ShouldReturnFalse()
    {
        // Arrange - 长度字节错误
        var frame = new byte[] { 0x51, 0x52, 0x58, 0x51, 0x51, 0x50, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "结束字节错误应返回失败")]
    public void TryParseDeviceStatus_WrongEndByte_ShouldReturnFalse()
    {
        // Arrange - 结束字节错误
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0x50, 0xFF };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "消息类型不匹配应返回失败")]
    public void TryParseDeviceStatus_WrongMessageType_ShouldReturnFalse()
    {
        // Arrange - 消息类型是0x52（控制命令），而不是0x51（状态上报）
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x52, 0x50, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "未知状态码应返回失败")]
    public void TryParseDeviceStatus_UnknownStatusCode_ShouldReturnFalse()
    {
        // Arrange - 状态码0xFF未定义
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0xFF, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseDeviceStatus(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "未知应答码应返回失败")]
    public void TryParseResponse_UnknownResponseCode_ShouldReturnFalse()
    {
        // Arrange - 应答码0xFF未定义
        var frame = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, 0xFF, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.TryParseResponse(
            frame,
            out _,
            out _);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region 格式化测试

    [Fact(DisplayName = "FormatBytes应返回正确的十六进制字符串")]
    public void FormatBytes_ShouldReturnCorrectHexString()
    {
        // Arrange
        var bytes = new byte[] { 0x51, 0x52, 0x57, 0x51, 0x52, 0x51, 0xFE };

        // Act
        var result = ShuDiNiaoProtocol.FormatBytes(bytes);

        // Assert
        Assert.Equal("51 52 57 51 52 51 FE", result);
    }

    #endregion
}

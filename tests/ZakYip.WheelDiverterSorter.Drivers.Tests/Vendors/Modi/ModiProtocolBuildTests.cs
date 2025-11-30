using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Modi;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.Modi;

/// <summary>
/// 莫迪协议打包测试
/// </summary>
/// <remarks>
/// 验证协议构造的命令帧与厂商文档完全一致
/// </remarks>
public class ModiProtocolBuildTests
{
    [Fact(DisplayName = "构造停止命令帧应符合协议规范")]
    public void BuildCommandFrame_Stop_ShouldMatchProtocolSpec()
    {
        // Arrange
        int deviceId = 1;
        var command = ModiControlCommand.Stop;

        // Act
        var frame = ModiProtocol.BuildCommandFrame(deviceId, command);

        // Assert - 帧格式：帧头(AA) + 设备号(01) + 命令(00) + 校验和(01) + 帧尾(55)
        Assert.Equal(5, frame.Length);
        Assert.Equal(0xAA, frame[0]); // 帧头
        Assert.Equal(0x01, frame[1]); // 设备编号
        Assert.Equal(0x00, frame[2]); // 命令码：停止
        Assert.Equal(0x01, frame[3]); // 校验和：0x01 + 0x00 = 0x01
        Assert.Equal(0x55, frame[4]); // 帧尾
    }

    [Fact(DisplayName = "构造左转命令帧应符合协议规范")]
    public void BuildCommandFrame_TurnLeft_ShouldMatchProtocolSpec()
    {
        // Arrange
        int deviceId = 1;
        var command = ModiControlCommand.TurnLeft;

        // Act
        var frame = ModiProtocol.BuildCommandFrame(deviceId, command);

        // Assert
        Assert.Equal(5, frame.Length);
        Assert.Equal(0xAA, frame[0]); // 帧头
        Assert.Equal(0x01, frame[1]); // 设备编号
        Assert.Equal(0x01, frame[2]); // 命令码：左转
        Assert.Equal(0x02, frame[3]); // 校验和：0x01 + 0x01 = 0x02
        Assert.Equal(0x55, frame[4]); // 帧尾
    }

    [Fact(DisplayName = "构造右转命令帧应符合协议规范")]
    public void BuildCommandFrame_TurnRight_ShouldMatchProtocolSpec()
    {
        // Arrange
        int deviceId = 1;
        var command = ModiControlCommand.TurnRight;

        // Act
        var frame = ModiProtocol.BuildCommandFrame(deviceId, command);

        // Assert
        Assert.Equal(5, frame.Length);
        Assert.Equal(0xAA, frame[0]); // 帧头
        Assert.Equal(0x01, frame[1]); // 设备编号
        Assert.Equal(0x02, frame[2]); // 命令码：右转
        Assert.Equal(0x03, frame[3]); // 校验和：0x01 + 0x02 = 0x03
        Assert.Equal(0x55, frame[4]); // 帧尾
    }

    [Fact(DisplayName = "构造回中命令帧应符合协议规范")]
    public void BuildCommandFrame_ReturnCenter_ShouldMatchProtocolSpec()
    {
        // Arrange
        int deviceId = 1;
        var command = ModiControlCommand.ReturnCenter;

        // Act
        var frame = ModiProtocol.BuildCommandFrame(deviceId, command);

        // Assert
        Assert.Equal(5, frame.Length);
        Assert.Equal(0xAA, frame[0]); // 帧头
        Assert.Equal(0x01, frame[1]); // 设备编号
        Assert.Equal(0x03, frame[2]); // 命令码：回中
        Assert.Equal(0x04, frame[3]); // 校验和：0x01 + 0x03 = 0x04
        Assert.Equal(0x55, frame[4]); // 帧尾
    }

    [Theory(DisplayName = "不同设备编号应正确设置到命令帧")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void BuildCommandFrame_DifferentDeviceIds_ShouldSetCorrectly(int deviceId)
    {
        // Act
        var frame = ModiProtocol.BuildCommandFrame(deviceId, ModiControlCommand.Stop);

        // Assert
        Assert.Equal((byte)deviceId, frame[1]);
    }

    [Fact(DisplayName = "验证响应帧应正确识别有效响应")]
    public void ValidateResponse_ValidResponse_ShouldReturnTrue()
    {
        // Arrange - 构造有效响应帧
        var response = new byte[] { 0xAA, 0x01, 0x00, 0x01, 0x55 };

        // Act
        var isValid = ModiProtocol.ValidateResponse(response, 1);

        // Assert
        Assert.True(isValid);
    }

    [Fact(DisplayName = "验证响应帧应识别无效帧头")]
    public void ValidateResponse_InvalidHeader_ShouldReturnFalse()
    {
        // Arrange - 无效帧头
        var response = new byte[] { 0xBB, 0x01, 0x00, 0x01, 0x55 };

        // Act
        var isValid = ModiProtocol.ValidateResponse(response, 1);

        // Assert
        Assert.False(isValid);
    }

    [Fact(DisplayName = "验证响应帧应识别无效帧尾")]
    public void ValidateResponse_InvalidTail_ShouldReturnFalse()
    {
        // Arrange - 无效帧尾
        var response = new byte[] { 0xAA, 0x01, 0x00, 0x01, 0x66 };

        // Act
        var isValid = ModiProtocol.ValidateResponse(response, 1);

        // Assert
        Assert.False(isValid);
    }

    [Fact(DisplayName = "验证响应帧应识别设备编号不匹配")]
    public void ValidateResponse_DeviceIdMismatch_ShouldReturnFalse()
    {
        // Arrange - 设备编号不匹配
        var response = new byte[] { 0xAA, 0x02, 0x00, 0x02, 0x55 };

        // Act
        var isValid = ModiProtocol.ValidateResponse(response, 1);

        // Assert
        Assert.False(isValid);
    }

    [Fact(DisplayName = "验证响应帧应识别校验和错误")]
    public void ValidateResponse_InvalidChecksum_ShouldReturnFalse()
    {
        // Arrange - 校验和错误
        var response = new byte[] { 0xAA, 0x01, 0x00, 0xFF, 0x55 };

        // Act
        var isValid = ModiProtocol.ValidateResponse(response, 1);

        // Assert
        Assert.False(isValid);
    }

    [Fact(DisplayName = "格式化字节数组应正确输出十六进制字符串")]
    public void FormatBytes_ShouldReturnHexString()
    {
        // Arrange
        var bytes = new byte[] { 0xAA, 0x01, 0x02, 0x55 };

        // Act
        var result = ModiProtocol.FormatBytes(bytes);

        // Assert
        Assert.Equal("0xAA 0x01 0x02 0x55", result);
    }
}

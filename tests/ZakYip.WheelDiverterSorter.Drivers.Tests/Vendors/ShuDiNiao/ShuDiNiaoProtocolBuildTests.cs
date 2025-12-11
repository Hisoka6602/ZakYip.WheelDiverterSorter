using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟协议打包测试
/// </summary>
/// <remarks>
/// 验证协议构造的命令帧与厂商文档完全一致
/// </remarks>
public class ShuDiNiaoProtocolBuildTests
{
    [Fact(DisplayName = "构造运行命令帧应符合协议规范")]
    public void BuildCommandFrame_Run_ShouldMatchProtocolSpec()
    {
        // Arrange
        byte deviceAddress = 0x51;
        var command = ShuDiNiaoControlCommand.Run;

        // Act
        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, command);

        // Assert - 厂商文档示例：51 52 57 51 52 51 FE
        Assert.Equal(7, frame.Length);
        Assert.Equal(0x51, frame[0]); // 起始字节1
        Assert.Equal(0x52, frame[1]); // 起始字节2
        Assert.Equal(0x57, frame[2]); // 长度字节
        Assert.Equal(0x51, frame[3]); // 设备地址
        Assert.Equal(0x52, frame[4]); // 消息类型：控制命令
        Assert.Equal(0x51, frame[5]); // 命令码：运行
        Assert.Equal(0xFE, frame[6]); // 结束字符
    }

    [Fact(DisplayName = "构造停止命令帧应符合协议规范")]
    public void BuildCommandFrame_Stop_ShouldMatchProtocolSpec()
    {
        // Arrange
        byte deviceAddress = 0x51;
        var command = ShuDiNiaoControlCommand.Stop;

        // Act
        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, command);

        // Assert - 厂商文档示例：51 52 57 51 52 52 FE
        Assert.Equal(7, frame.Length);
        Assert.Equal(0x51, frame[0]);
        Assert.Equal(0x52, frame[1]);
        Assert.Equal(0x57, frame[2]);
        Assert.Equal(0x51, frame[3]);
        Assert.Equal(0x52, frame[4]);
        Assert.Equal(0x52, frame[5]); // 命令码：停止
        Assert.Equal(0xFE, frame[6]);
    }

    [Fact(DisplayName = "构造左摆命令帧应符合协议规范")]
    public void BuildCommandFrame_TurnLeft_ShouldMatchProtocolSpec()
    {
        // Arrange
        byte deviceAddress = 0x51;
        var command = ShuDiNiaoControlCommand.TurnLeft;

        // Act
        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, command);

        // Assert - 厂商文档示例：51 52 57 51 52 53 FE
        Assert.Equal(7, frame.Length);
        Assert.Equal(0x51, frame[0]);
        Assert.Equal(0x52, frame[1]);
        Assert.Equal(0x57, frame[2]);
        Assert.Equal(0x51, frame[3]);
        Assert.Equal(0x52, frame[4]);
        Assert.Equal(0x53, frame[5]); // 命令码：左摆
        Assert.Equal(0xFE, frame[6]);
    }

    [Fact(DisplayName = "构造回中命令帧应符合协议规范")]
    public void BuildCommandFrame_ReturnCenter_ShouldMatchProtocolSpec()
    {
        // Arrange
        byte deviceAddress = 0x51;
        var command = ShuDiNiaoControlCommand.ReturnCenter;

        // Act
        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, command);

        // Assert - 厂商文档示例：51 52 57 51 52 54 FE
        Assert.Equal(7, frame.Length);
        Assert.Equal(0x51, frame[0]);
        Assert.Equal(0x52, frame[1]);
        Assert.Equal(0x57, frame[2]);
        Assert.Equal(0x51, frame[3]);
        Assert.Equal(0x52, frame[4]);
        Assert.Equal(0x54, frame[5]); // 命令码：回中
        Assert.Equal(0xFE, frame[6]);
    }

    [Fact(DisplayName = "构造右摆命令帧应符合协议规范")]
    public void BuildCommandFrame_TurnRight_ShouldMatchProtocolSpec()
    {
        // Arrange
        byte deviceAddress = 0x51;
        var command = ShuDiNiaoControlCommand.TurnRight;

        // Act
        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, command);

        // Assert - 厂商文档示例：51 52 57 51 52 55 FE
        Assert.Equal(7, frame.Length);
        Assert.Equal(0x51, frame[0]);
        Assert.Equal(0x52, frame[1]);
        Assert.Equal(0x57, frame[2]);
        Assert.Equal(0x51, frame[3]);
        Assert.Equal(0x52, frame[4]);
        Assert.Equal(0x55, frame[5]); // 命令码：右摆
        Assert.Equal(0xFE, frame[6]);
    }

    [Theory(DisplayName = "不同设备地址应正确设置到命令帧")]
    [InlineData(0x51)]
    [InlineData(0x52)]
    [InlineData(0x53)]
    [InlineData(0x54)]
    [InlineData(0x55)]
    public void BuildCommandFrame_DifferentDeviceAddresses_ShouldSetCorrectly(byte deviceAddress)
    {
        // Act
        var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, ShuDiNiaoControlCommand.Run);

        // Assert
        Assert.Equal(deviceAddress, frame[3]);
    }
}
